﻿//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using SDKTemplate;
using System;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using System.IO;
using Windows.Networking;
using System.Runtime.InteropServices.WindowsRuntime;
using CameraCapture80.Common;

namespace CameraCapture
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CapturePhoto : SDKTemplate.Common.LayoutAwarePage
    {
        // A pointer back to the main page.  This is needed if you want to call methods in MainPage such
        // as NotifyUser()
        MainPage rootPage = MainPage.Current;
        private Windows.Foundation.Collections.IPropertySet appSettings;
        private const String photoKey = "capturedPhoto";

        public CapturePhoto()
        {
            this.InitializeComponent();
            appSettings = ApplicationData.Current.LocalSettings.Values;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // Reload previously taken photo
            if (appSettings.ContainsKey(photoKey))
            {
                object filePath;
                if (appSettings.TryGetValue(photoKey, out filePath) && filePath.ToString() != "")
                {
                    CaptureButton.IsEnabled = false;
                    await ReloadPhoto(filePath.ToString());
                    CaptureButton.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// This is the click handler for the 'CaptureButton' button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CapturePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                rootPage.NotifyUser("", NotifyType.StatusMessage);

                // Using Windows.Media.Capture.CameraCaptureUI API to capture a photo
                CameraCaptureUI dialog = new CameraCaptureUI();
                dialog.PhotoSettings.AllowCropping = false;

                StorageFile file = await dialog.CaptureFileAsync(CameraCaptureUIMode.Photo);
                if (file != null)
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
                    {
                        bitmapImage.SetSource(fileStream);
                    }
                    CapturedPhoto.Source = bitmapImage;

                    // Store the file path in Application Data
                    appSettings[photoKey] = file.Path;

                    rootPage.NotifyUser("Photo is saved at:" + file.Path, NotifyType.StatusMessage);

                    var StreamRandom = await file.OpenAsync(FileAccessMode.Read);
                    pushToServer(StreamRandom);
                    StreamRandom.Dispose();

                }
                else
                {
                    rootPage.NotifyUser("No photo captured.", NotifyType.StatusMessage);
                }
            }
            catch (Exception ex)
            {
                rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
            }
        }

        private async void pushToServer(IRandomAccessStream randomstream)
        {
            var sourceStream = randomstream.GetInputStreamAt(0).AsStreamForRead();
            try
            {
                var socket = new StreamSocket();
                await socket.ConnectAsync(new HostName(ConstParameters.VideoStreamReceiverHost), ConstParameters.VideoStreamReceiverPort.ToString());

                //Write data to the echo server.
                Stream streamOut = socket.OutputStream.AsStreamForWrite();

                var buffer = new byte[16 * 1024];

                int read;
                while ((read = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    streamOut.Write(buffer, 0, read);
                    streamOut.Flush();
                }
                streamOut.Dispose();
                sourceStream.Dispose();
                socket.Dispose();
            }
            catch (Exception e)
            {
                rootPage.NotifyUser(e.Message, NotifyType.ErrorMessage);
            }
        }

        /// <summary>
        /// This is the click handler for the 'Reset' button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            CapturedPhoto.Source = new BitmapImage(new Uri(this.BaseUri, "Assets/placeholder-sdk.png"));

            // Clear file path in Application Data
            appSettings.Remove(photoKey);
        }

        /// <summary>
        /// Loads the photo from file path
        /// </summary>
        /// <param name="filePath">The path to load the photo from</param>
        private async Task ReloadPhoto(String filePath)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                BitmapImage bitmapImage = new BitmapImage();
                using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    bitmapImage.SetSource(fileStream);
                }
                CapturedPhoto.Source = bitmapImage;
                rootPage.NotifyUser("", NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                appSettings.Remove(photoKey);
                rootPage.NotifyUser(ex.Message, NotifyType.ErrorMessage);
            }
        }
    }
}
