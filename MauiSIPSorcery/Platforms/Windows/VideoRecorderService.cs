using MauiSIPSorcery.Interfaces;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.UI.Xaml.Media.Imaging;
using Org.BouncyCastle.Utilities.Encoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using WinRT;

namespace MauiSIPSorcery.Platforms.Windows
{
    public class VideoRecorderService : IVideoRecorder
    {
        private MediaCapture _mediaCapture;
        private MediaFrameReader _frameReader;

        public event Action<byte[]> OnVideoFrameArrived;

        private bool _isRecording;


        public async void StartRecording()
        {
            if (_isRecording) return;

            // 1. 初始化 MediaCapture 对象
            _mediaCapture = new MediaCapture();
            var videos = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var settings = new MediaCaptureInitializationSettings()
            {
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MediaCategory = MediaCategory.Communications,
            };
            await _mediaCapture.InitializeAsync(settings);

            // 配置视频帧读取器
            var frameSource = _mediaCapture.FrameSources.Values.FirstOrDefault(source => source.Info.MediaStreamType == MediaStreamType.VideoRecord);
            _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource, MediaEncodingSubtypes.Bgra8, new BitmapSize(320, 320));
            _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            _frameReader.FrameArrived += FrameReader_FrameArrived;

            await _frameReader.StartAsync();
            _isRecording = true;
        }


        //private Stopwatch _frameStopwatch = Stopwatch.StartNew();
        //private readonly TimeSpan _frameInterval = TimeSpan.FromMilliseconds(100); // 限制为每秒3帧
        //private readonly object _frameLock = new object();
        //private bool _isProcessingFrame = false;

        //private async void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        //{
        //    if (!_isRecording) return;

        //    bool shouldProcess = false;
        //    lock (_frameLock)
        //    {
        //        // 检查是否超过时间间隔且当前无处理中的帧
        //        if (!_isProcessingFrame && _frameStopwatch.Elapsed >= _frameInterval)
        //        {
        //            _isProcessingFrame = true;
        //            shouldProcess = true;
        //            _frameStopwatch.Restart(); // 重置计时器以确保间隔
        //        }
        //    }

        //    if (!shouldProcess) return;

        //    try
        //    {
        //        var frame = sender.TryAcquireLatestFrame();
        //        if (frame != null)
        //        {
        //            var bitmap = frame.VideoMediaFrame?.SoftwareBitmap;
        //            if (bitmap != null)
        //            {
        //                var bitmapImage = await SoftwareBitmapToByteArrayAsync(bitmap);
        //                VideoChannel.Writer.TryWrite(bitmapImage);
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        lock (_frameLock)
        //        {
        //            _isProcessingFrame = false; // 确保处理完成后释放标志
        //        }
        //    }
        //}


        private async void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            var frame = sender.TryAcquireLatestFrame();
            if (frame != null)
            {
                var softwareBitmap = frame.VideoMediaFrame?.SoftwareBitmap;
                if (softwareBitmap != null)
                {
                    if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                    {
                        softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    }

                    using var stream = new InMemoryRandomAccessStream();

                    var encodingOptions = new List<KeyValuePair<string, BitmapTypedValue>>
                    {
                        new("ImageQuality", new BitmapTypedValue(0.5, PropertyType.Single)),
                    };
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, encodingOptions);

                    encoder.SetSoftwareBitmap(softwareBitmap);
                    await encoder.FlushAsync();
                    softwareBitmap.Dispose();

                    using var memoryStream = new MemoryStream();
                    await stream.AsStream().CopyToAsync(memoryStream);

                    OnVideoFrameArrived?.Invoke(memoryStream.ToArray());
                }
            }
        }


        public async void StopRecording()
        {
            if (!_isRecording) return;

            _isRecording = false;
            await _frameReader?.StopAsync();

            _frameReader.FrameArrived -= FrameReader_FrameArrived;

            _mediaCapture.Dispose();
            _frameReader.Dispose();
        }

    }
}