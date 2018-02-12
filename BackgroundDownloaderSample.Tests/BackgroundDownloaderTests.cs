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
using OpenQA.Selenium;
using System;
using System.Threading;

namespace BackgroundDownloaderSample.Tests
{
    [TestClass]
    public class BackgroundDownloaderTests : TestBase
    {
        #region Test lifecycle code
        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            BaseSetup(context);
        }

        [ClassCleanup]
        public static void TearDown()
        {
            BaseTearDown();
        }

        [TestInitialize]
        public void TestInit()
        {
            // Reset the app state
            ResetAppState();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Make sure not to leave files behind after the test
            ClearSessionFiles();
        }
        #endregion

        /// <summary>
        /// Download a file
        /// </summary>
        [TestMethod]
        public void SingleFileDownload()
        {
            FileNameTextBox.SendKeys("dotNetFx45_Full_setup.exe");
            ServerAddressTextBox.SendKeys("https://download.microsoft.com");
            StartButton.Click();

            Thread.Sleep(TimeSpan.FromSeconds(5)); // Wait for file to download

            var lastLog = GetLastLog();
            var status = lastLog.Split(',')[1].Trim();
            Assert.AreEqual("Status Code: 200", status);
        }

        /// <summary>
        /// Download a file, many times
        /// </summary>
        [TestMethod]
        public void BatchFileDownload()
        {
            FileNameTextBox.SendKeys("dotNetFx45_Full_setup.exe(10)");
            ServerAddressTextBox.SendKeys("https://download.microsoft.com");
            StartButton.Click();

            Thread.Sleep(TimeSpan.FromSeconds(5)); // Wait for files to download

            var lastLog = GetLastLog();
            var status = lastLog.Split(',')[1].Trim();
            Assert.AreEqual("Status Code: 200", status);
        }

        /// <summary>
        /// Download a file, many times
        /// </summary>
        [TestMethod]
        public void MultipleFilesDownload()
        {
            FileNameTextBox.SendKeys("dotNetFx45_Full_setup.exe;directx_Jun2010_redist.exe");
            ServerAddressTextBox.SendKeys("https://download.microsoft.com");
            StartButton.Click();

            Thread.Sleep(TimeSpan.FromSeconds(5)); // Wait for files to download

            var lastLog = GetLastLog();
            var status = lastLog.Split(',')[1].Trim();
            Assert.AreEqual("Status Code: 200", status);
        }
    }
}
