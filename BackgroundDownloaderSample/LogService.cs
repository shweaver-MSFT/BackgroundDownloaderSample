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

namespace BackgroundDownloaderSample
{
    public enum NotifyType
    {
        Log, // Standard log message, not shown to user (unless verbose logging is on)
        StatusMessage, // Status type message to be shown to the user 
        ErrorMessage // Error type message to be shown to the user 
    }

    public class MessageEventArgs : EventArgs
    {
        public string Message { get; }
        public NotifyType Type { get; }
        public MessageEventArgs(string message, NotifyType type)
        {
            Message = message;
            Type = type;
        }
    }

    public class LogService
    {
        // Static instance
        public static LogService Instance = new LogService();

        public static bool UseVerboseLogging = true;

        public event EventHandler<MessageEventArgs> MessageLogged;

        private void OnMessageLogged(MessageEventArgs e)
        {
            MessageLogged?.Invoke(this, e);
        }

        /// <summary>
        /// Logs a message.
        /// Output to the debug console and fire an event so that the UI can respond.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        public void LogMessage(string message, NotifyType type)
        {
            switch(type)
            {
                case NotifyType.Log:
                    System.Diagnostics.Debug.WriteLine($"LOG: {message}");
                    if (UseVerboseLogging)
                    {
                        OnMessageLogged(new MessageEventArgs(message, type));
                    }
                    break;
                case NotifyType.StatusMessage:
                    System.Diagnostics.Debug.WriteLine($"STATUS: {message}");
                    OnMessageLogged(new MessageEventArgs(message, type));
                    break;
                case NotifyType.ErrorMessage:
                    System.Diagnostics.Debug.WriteLine($"ERROR: {message}");
                    OnMessageLogged(new MessageEventArgs(message, type));
                    break;
            }
        }
    }
}
