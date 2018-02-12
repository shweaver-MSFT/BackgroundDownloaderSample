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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Remote;
using System;
using System.Collections.Generic;

namespace BackgroundDownloaderSample.Tests
{
    public class TestBase
    {
        // Note: append /wd/hub to the URL if you're directing the test at Appium
        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";

        protected static WindowsDriver<WindowsElement> AppSession;
        protected static WindowsElement FileNameTextBox => GetKnownElement("FileNameTextBox");
        protected static WindowsElement ServerAddressTextBox => GetKnownElement("ServerAddressTextBox");
        protected static WindowsElement PriorityToggleSwitch => GetKnownElement("PriorityToggleSwitch");
        protected static WindowsElement UnconstrainedToggleSwitch => GetKnownElement("UnconstrainedToggleSwitch");
        protected static WindowsElement ShowUnconstrainedWarningCheckBox => GetKnownElement("ShowUnconstrainedWarningCheckBox");
        protected static WindowsElement StartButton => GetKnownElement("StartButton");
        protected static WindowsElement PauseButton => GetKnownElement("PauseButton");
        protected static WindowsElement ResumeButton => GetKnownElement("ResumeButton");
        protected static WindowsElement CancelButton => GetKnownElement("CancelButton");
        protected static WindowsElement ClearButton => GetKnownElement("ClearButton");
        protected static WindowsElement OutputField => GetKnownElement("OutputField");
        protected static WindowsElement ClearOutputButton => GetKnownElement("ClearOutputButton");
        protected static WindowsElement VerboseLoggingCheckBox => GetKnownElement("VerboseLoggingCheckBox");
        protected static WindowsElement ResetAppStateButton => GetKnownElement("ResetAppStateButton");

        private static Dictionary<string, WindowsElement> _knownElements;

        public static void BaseSetup(TestContext context)
        {
            if (AppSession == null)
            {
                // Launch the test app
                DesiredCapabilities appCapabilities = new DesiredCapabilities();
                appCapabilities.SetCapability("app", "BackgroundDownloaderSample_80c4904e66sn0!App");
                appCapabilities.SetCapability("deviceName", "WindowsPC");
                AppSession = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), appCapabilities);
                Assert.IsNotNull(AppSession);
                AppSession.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(4);

                _knownElements = new Dictionary<string, WindowsElement>();
            }
        }

        public static void BaseTearDown()
        {
            // Cleanup the WindowsElement objects if initialized
            _knownElements.Clear();
            _knownElements = null;

            // Close the application and delete the session
            if (AppSession != null)
            {
                try
                {
                    AppSession.Close();

                    // This should throw if the window is closed successfully
                    var currentHandle = AppSession.CurrentWindowHandle;
                }
                catch { }

                AppSession.Quit();
                AppSession = null;
            }
        }

        /// <summary>
        /// Get an element from the known elements cache.
        /// If it does not already exist, the element is retreived by elementName,
        /// added to the known elements cache, and returned.
        /// </summary>
        /// <param name="elementName"></param>
        /// <returns></returns>
        protected static WindowsElement GetKnownElement(string elementName)
        {
            if (!_knownElements.TryGetValue(elementName, out WindowsElement knownElement))
            {
                knownElement = AppSession.FindElementByAccessibilityId(elementName);
            }
            return knownElement;
        }

        /// <summary>
        /// Clear the log output field
        /// </summary>
        protected void ClearOutputField()
        {
            ClearOutputButton.Click();
            Assert.IsTrue(string.IsNullOrEmpty(OutputField.Text));
        }

        /// <summary>
        /// Press the Clear Session Files button to clear out any local files from 
        /// this session.
        /// </summary>
        protected void ClearSessionFiles()
        {
            ClearButton.Click();
        }

        /// <summary>
        /// Reset the state of the test app
        /// </summary>
        protected void ResetAppState()
        {
            ResetAppStateButton.Click();
        }

        /// <summary>
        /// Get the last non-empty line from the log.
        /// </summary>
        /// <returns></returns>
        protected string GetLastLog()
        {
            var logLines = OutputField.Text.Split('\r');
            var lastLine = logLines[logLines.Length - 1];
            return (string.IsNullOrWhiteSpace(lastLine)) ? logLines[logLines.Length - 2] : lastLine;
        }
    }
}
