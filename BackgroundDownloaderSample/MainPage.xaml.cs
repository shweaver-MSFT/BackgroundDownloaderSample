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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace BackgroundDownloaderSample
{
    public sealed partial class MainPage : Page
    {
        private string _fileName => FileNameTextBox.Text?.Trim();
        private string[] _fileNames => _fileName.Split(';');
        private string _serverAddress => ServerAddressTextBox.Text?.Trim();
        private bool _isHighPriority => PriorityToggleSwitch.IsOn;
        private BackgroundTransferPriority _priority => _isHighPriority ? BackgroundTransferPriority.High : BackgroundTransferPriority.Default;
        private bool _isUnconstrained => UnconstrainedToggleSwitch.IsOn;
        private bool _showUnconstrainedWarning => ShowUnconstrainedWarningCheckBox.IsChecked ?? false;
        private bool _doAutoScroll => AutoScrollCheckBox.IsChecked ?? false;

        public MainPage()
        {
            InitializeComponent();
            App.LogService.MessageLogged += LogService_MessageLogged;

            VerboseLoggingCheckBox.IsChecked = LogService.UseVerboseLogging;
            ThemeToggleSwitch.IsOn = (RequestedTheme == ElementTheme.Dark);
        }

        /// <summary>
        /// Start a download or series of downloads using the current proirity and isUnconstrained states.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            var priority = _priority;
            var isUnconstrained = _isUnconstrained;
            var serverAddress = _serverAddress;
            var fileNames = _fileNames;

            var startButtonContent = StartButton.Content;
            StartButton.Content = "Queing Download(s)";
            StartButton.IsEnabled = false;

            Task.Run(async delegate
            {
                foreach (var fileName in fileNames)
                {
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        if (Regex.IsMatch(fileName, ".+\\([0-9]+\\)"))
                        {
                            var fileNameParts = fileName.Split('(');
                            var fileNameString = fileNameParts[0];
                            var count = int.Parse(fileNameParts[1].Replace(")", string.Empty));
                            foreach (var i in Enumerable.Range(0, count))
                            {
                                App.BackgroundDownloadService.StartDownload(fileNameString, serverAddress, priority, isUnconstrained);
                            }
                        }
                        else
                        {
                            App.BackgroundDownloadService.StartDownload(fileName.Trim(), serverAddress, priority, isUnconstrained);
                        }
                    }
                }

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, delegate
                {
                     StartButton.Content = startButtonContent;
                     StartButton.IsEnabled = true;
                });
            });
        }

        /// <summary>
        /// Pause all active downloads
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PauseAll_Click(object sender, RoutedEventArgs e)
        {
            Log("Downloads: " + App.BackgroundDownloadService.ActiveDownloads.Count);

            foreach (DownloadOperation download in App.BackgroundDownloadService.ActiveDownloads)
            {
                if (download.Progress.Status == BackgroundTransferStatus.Running)
                {
                    App.BackgroundDownloadService.PauseActiveDownload(download);
                    Log("Paused: " + download.Guid);
                }
                else
                {
                    Log(String.Format(CultureInfo.CurrentCulture, "Skipped: {0}, Status: {1}", download.Guid,
                        download.Progress.Status));
                }
            }
        }

        /// <summary>
        /// Resume all active downloads
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResumeAll_Click(object sender, RoutedEventArgs e)
        {
            Log("Downloads: " + App.BackgroundDownloadService.ActiveDownloads.Count);

            foreach (DownloadOperation download in App.BackgroundDownloadService.ActiveDownloads)
            {
                if (download.Progress.Status == BackgroundTransferStatus.PausedByApplication)
                {
                    App.BackgroundDownloadService.ResumeActiveDownload(download);
                    Log("Resumed: " + download.Guid);
                }
                else
                {
                    Log(String.Format(CultureInfo.CurrentCulture, "Skipped: {0}, Status: {1}", download.Guid,
                        download.Progress.Status));
                }
            }
        }

        /// <summary>
        /// Cancels all active downloads
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CancelAll_Click(object sender, RoutedEventArgs e)
        {
            Log("Canceling Downloads: " + App.BackgroundDownloadService.ActiveDownloads.Count);

            App.BackgroundDownloadService.CancelAllActiveDownloads();
        }

        /// <summary>
        /// Clear the downloaded files from their save location on disk
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Log("Clearing Session Files from Disk: " + App.BackgroundDownloadService.SessionFiles.Count);

            App.BackgroundDownloadService.ClearSessionFiles();
        }

        /// <summary>
        /// Clear out the OutputField
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearOutput_Click(object sender, RoutedEventArgs e)
        {
            OutputField.Text = string.Empty;
        }

        /// <summary>
        /// Log a message with the LogService
        /// </summary>
        /// <param name="message"></param>
        private void Log(string message, NotifyType type = NotifyType.Log)
        {
            App.LogService.LogMessage(message, type);
        }

        /// <summary>
        /// Handle a MessageLogged event by notifying the user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogService_MessageLogged(object sender, MessageEventArgs e)
        {
            // When operations happen on a background thread we have to marshal UI updates back to the UI thread.
            var ignore = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                NotifyUser(e.Message);
            });
        }

        /// <summary>
        /// Alerts the user with a message
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        private void NotifyUser(string message)
        {
            OutputField.Text += message + "\r\n";
        }

        /// <summary>
        /// Show the Unconstrained mode warning
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UnconstrainedToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isUnconstrained && _showUnconstrainedWarning)
            {
                FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
            }
        }

        private void UnconstrainedToggleSwitch_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            ShowUnconstrainedWarningCheckBox.IsChecked = true;
            UnconstrainedToggleSwitch_Toggled(sender, e);
        }

        private void ShowUnconstrainedWarningCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UnconstrainedWarningFlyout.Hide();
        }

        private void OutputField_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Auto scroll to endwhen content changes
            if (_doAutoScroll)
            {
                var textbox = sender as TextBox;
                var panel = (Panel)VisualTreeHelper.GetChild(textbox, 0);
                var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(panel, 2);
                scrollViewer.ChangeView(0, scrollViewer.ExtentHeight, 1);
            }
        }

        private void ThemeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            RequestedTheme = (ThemeToggleSwitch.IsOn) ? ElementTheme.Dark : ElementTheme.Light;
        }

        private void VerboseLoggingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            LogService.UseVerboseLogging = VerboseLoggingCheckBox.IsChecked ?? true;
        }

        private void VerboseLoggingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            LogService.UseVerboseLogging = VerboseLoggingCheckBox.IsChecked ?? false;
        }
    }
}

