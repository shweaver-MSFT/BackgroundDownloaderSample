//******************************************************************************
//
// Copyright (c) 2018 Microsoft Corporation. All rights reserved.
//
// This code is licensed under the MIT License (MIT).
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//******************************************************************************

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Web;

namespace BackgroundDownloaderSample
{
    public class BackgroundDownloadService : IDisposable
    {
        public static BackgroundDownloadService Instance = new BackgroundDownloadService();

        private CancellationTokenSource _cts;
        private Dictionary<Guid, DownloadOperation> _activeDownloads;
        private LinkedList<StorageFile> _sessionFiles;

        public IReadOnlyList<DownloadOperation> ActiveDownloads => new ReadOnlyCollection<DownloadOperation>(_activeDownloads.Values.ToArray());
        public IReadOnlyList<StorageFile> SessionFiles => new ReadOnlyCollection<StorageFile>(_sessionFiles.ToArray());

        public BackgroundDownloadService()
        {
            _cts = new CancellationTokenSource();
            _sessionFiles = new LinkedList<StorageFile>();
        }

        /// <summary>
        /// Delete all of the files created during this session.
        /// 
        /// If the app is closed before the downloaded files are cleared, 
        /// they must be cleared manually from the user's pictures folder.
        /// </summary>
        public async void ClearSessionFiles()
        {
            while(_sessionFiles.Count > 0)
            {
                await _sessionFiles.First.Value?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                _sessionFiles.RemoveFirst();
            }
        }

        /// <summary>
        /// Pause an active download acquired via ActiveDownloads IReadOnlyList
        /// </summary>
        /// <param name="download"></param>
        public void PauseActiveDownload(DownloadOperation download) => PauseActiveDownload(download.Guid);
        public void PauseActiveDownload(Guid guid)
        {
            _activeDownloads?[guid]?.Pause();
        }

        /// <summary>
        /// Resume an active download acquired via ActiveDownloads IReadOnlyList
        /// </summary>
        /// <param name="download"></param>
        public void ResumeActiveDownload(DownloadOperation download) => ResumeActiveDownload(download.Guid);
        public void ResumeActiveDownload(Guid guid)
        {
            _activeDownloads?[guid]?.Resume();
        }

        /// <summary>
        /// Cancel all active downloads
        /// </summary>
        /// <param name="download"></param>
        public void CancelAllActiveDownloads()
        {
            _cts.Cancel();
            _cts.Dispose();

            // Re-create the CancellationTokenSource and activeDownloads for future downloads.
            _cts = new CancellationTokenSource();
            _activeDownloads = new Dictionary<Guid, DownloadOperation>();
        }

        /// <summary>
        /// Enumerate the downloads that were going on in the background while the app was closed.
        /// </summary>
        /// <returns></returns>
        public async Task DiscoverActiveDownloadsAsync()
        {
            _activeDownloads = new Dictionary<Guid, DownloadOperation>();

            IReadOnlyList<DownloadOperation> downloads = null;
            try
            {
                downloads = await BackgroundDownloader.GetCurrentDownloadsAsync();
            }
            catch (Exception ex)
            {
                if (!IsExceptionHandled("Discovery error", ex))
                {
                    throw;
                }
                return;
            }

            LogStatus("Loading background downloads: " + downloads.Count);

            if (downloads.Count > 0)
            {
                List<Task> tasks = new List<Task>();
                foreach (DownloadOperation download in downloads)
                {
                    LogStatus(String.Format(CultureInfo.CurrentCulture,
                        "Discovered background download: {0}, Status: {1}", download.Guid,
                        download.Progress.Status));

                    // Attach progress and completion handlers.
                    tasks.Add(HandleDownloadAsync(download, false));
                }

                // Don't await HandleDownloadAsync() in the foreach loop since we would attach to the second
                // download only when the first one completed; attach to the third download when the second one
                // completes etc. We want to attach to all downloads immediately.
                // If there are actions that need to be taken once downloads complete, await tasks here, outside
                // the loop.
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// Start downloading a file with the fileName and serverAddress provided
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="serverAddress"></param>
        /// <param name="priority"></param>
        /// <param name="requestUnconstrainedDownload"></param>
        public async void StartDownload(string fileName, string serverAddress, BackgroundTransferPriority priority, bool requestUnconstrainedDownload)
        {
            // Validating the URI is required since it was received from an untrusted source (user input).
            // The URI is validated by calling Uri.TryCreate() that will return 'false' for strings that are not valid URIs.
            // Note that when enabling the text box users may provide URIs to machines on the intrAnet that require
            // the "Home or Work Networking" capability.
            Uri source;
            if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out source))
            {
                LogStatus("Invalid URI.", NotifyType.ErrorMessage);
                return;
            }

            string destination = $"BackgroundDownloaderFiles\\{fileName}";

            if (string.IsNullOrWhiteSpace(destination))
            {
                LogStatus("A local file name is required.", NotifyType.ErrorMessage);
                return;
            }

            StorageFile destinationFile;
            try
            {
                destinationFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                    destination, CreationCollisionOption.GenerateUniqueName);

                _sessionFiles.AddLast(destinationFile);
            }
            catch (FileNotFoundException ex)
            {
                LogStatus("Error while creating file: " + ex.Message, NotifyType.ErrorMessage);
                return;
            }

            BackgroundDownloader downloader = new BackgroundDownloader();
            DownloadOperation download = downloader.CreateDownload(source, destinationFile);

            LogStatus(String.Format(CultureInfo.CurrentCulture, "Downloading {0} to {1} with {2} priority, {3}",
                source.AbsoluteUri, destinationFile.Name, priority, download.Guid));

            download.Priority = priority;

            if (!requestUnconstrainedDownload)
            {
                // Attach progress and completion handlers.
                await HandleDownloadAsync(download, true);
                return;
            }

            List<DownloadOperation> requestOperations = new List<DownloadOperation>();
            requestOperations.Add(download);

            // If the app isn't actively being used, at some point the system may slow down or pause long running
            // downloads. The purpose of this behavior is to increase the device's battery life.
            // By requesting unconstrained downloads, the app can request the system to not suspend any of the
            // downloads in the list for power saving reasons.
            // Use this API with caution since it not only may reduce battery life, but it may show a prompt to
            // the user.
            UnconstrainedTransferRequestResult result;
            try
            {
                result = await BackgroundDownloader.RequestUnconstrainedDownloadsAsync(requestOperations);
            }
            catch (NotImplementedException)
            {
                LogStatus(
                    "BackgroundDownloader.RequestUnconstrainedDownloadsAsync is not supported in Windows Phone.",
                    NotifyType.ErrorMessage);
                return;
            }

            LogStatus(String.Format(CultureInfo.CurrentCulture, "Request for unconstrained downloads has been {0}",
                (result.IsUnconstrained ? "granted" : "denied")));

            await HandleDownloadAsync(download, true);
        }

        /// <summary>
        /// Note that this event is invoked on a background thread, so we cannot access the UI directly.
        /// </summary>
        /// <param name="download"></param>
        private void DownloadProgress(DownloadOperation download)
        {
            LogStatus(String.Format(CultureInfo.CurrentCulture, "Progress: {0}, Status: {1}", download.Guid,
                download.Progress.Status));

            double percent = 100;
            if (download.Progress.TotalBytesToReceive > 0)
            {
                percent = download.Progress.BytesReceived * 100 / download.Progress.TotalBytesToReceive;
            }

            LogStatus(String.Format(CultureInfo.CurrentCulture, " - Transfered bytes: {0} of {1}, {2}%",
                download.Progress.BytesReceived, download.Progress.TotalBytesToReceive, percent));

            if (download.Progress.HasRestarted)
            {
                LogStatus(" - Download restarted");
            }

            if (download.Progress.HasResponseChanged)
            {
                // We've received new response headers from the server.
                LogStatus(" - Response updated; Header count: " + download.GetResponseInformation().Headers.Count);

                // If you want to stream the response data this is a good time to start.
                // download.GetResultStreamAt(0);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="download"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        private async Task HandleDownloadAsync(DownloadOperation download, bool start)
        {
            try
            {
                LogStatus("Running: " + download.Guid, NotifyType.StatusMessage);

                // Store the download so we can pause/resume.
                _activeDownloads.Add(download.Guid, download);

                Progress<DownloadOperation> progressCallback = new Progress<DownloadOperation>(DownloadProgress);
                if (start)
                {
                    // Start the download and attach a progress handler.
                    await download.StartAsync().AsTask(_cts.Token, progressCallback);
                }
                else
                {
                    // The download was already running when the application started, re-attach the progress handler.
                    await download.AttachAsync().AsTask(_cts.Token, progressCallback);
                }

                ResponseInformation response = download.GetResponseInformation();

                LogStatus(String.Format(CultureInfo.CurrentCulture, "Completed: {0}, Status Code: {1}",
                    download.Guid, response.StatusCode), NotifyType.StatusMessage);
            }
            catch (TaskCanceledException)
            {
                LogStatus("Canceled: " + download.Guid, NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                if (!IsExceptionHandled("Execution error", ex, download))
                {
                    throw;
                }
            }
            finally
            {
                _activeDownloads.Remove(download.Guid);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="ex"></param>
        /// <param name="download"></param>
        /// <returns></returns>
        private bool IsExceptionHandled(string title, Exception ex, DownloadOperation download = null)
        {
            WebErrorStatus error = BackgroundTransferError.GetStatus(ex.HResult);
            if (error == WebErrorStatus.Unknown)
            {
                return false;
            }

            if (download == null)
            {
                LogStatus(String.Format(CultureInfo.CurrentCulture, "Error: {0}: {1}", title, error),
                    NotifyType.ErrorMessage);
            }
            else
            {
                LogStatus(String.Format(CultureInfo.CurrentCulture, "Error: {0} - {1}: {2}", download.Guid, title,
                    error), NotifyType.ErrorMessage);
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        private static void LogStatus(string message, NotifyType type = NotifyType.Log)
        {
            LogService.Instance.LogMessage(message, type);
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
