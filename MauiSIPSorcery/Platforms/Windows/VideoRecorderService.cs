using MauiSIPSorcery.Interfaces;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.UI.Xaml.Media.Imaging;
using Org.BouncyCastle.Utilities.Encoders;
using SIPSorceryMedia.Abstractions;
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
        private IVideoEncoder _videoEncoder;

        private MediaCapture _mediaCapture;
        private MediaFrameReader _frameReader;

        public event Action<byte[]> OnVideoFrameArrived;
        public event Action<uint, byte[]> OnVideoSourceEncodedSample;   // uint durationRtpUnits, byte[] sample
        public event Action<uint, int, int, byte[], VideoPixelFormatsEnum> OnVideoSourceRawSample;   // uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat


        private bool _isRecording;


        public async void StartRecording(IVideoEncoder encoder)
        {
            if (_isRecording) return;

            _videoEncoder = encoder;

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
            _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource, MediaEncodingSubtypes.Nv12, new BitmapSize(320, 320));
            _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            _frameReader.FrameArrived += FrameReader_FrameArrived;

            await _frameReader.StartAsync();
            _isRecording = true;
        }


        private SoftwareBitmap _backBuffer;
        private DateTime _lastFrameAt = DateTime.MinValue;

        private async void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            using (var mediaFrameReference = sender.TryAcquireLatestFrame())
            {
                var videoMediaFrame = mediaFrameReference?.VideoMediaFrame;
                var softwareBitmap = videoMediaFrame?.SoftwareBitmap;

                if (softwareBitmap == null && videoMediaFrame != null)
                {
                    var videoFrame = videoMediaFrame.GetVideoFrame();
                    softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(videoFrame.Direct3DSurface);
                }

                if (softwareBitmap != null)
                {
                    int width = softwareBitmap.PixelWidth;
                    int height = softwareBitmap.PixelHeight;

                    if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Nv12)
                    {
                        softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Nv12, BitmapAlphaMode.Ignore);
                    }

                    // Swap the processed frame to _backBuffer and dispose of the unused image.
                    softwareBitmap = Interlocked.Exchange(ref _backBuffer, softwareBitmap);

                    using (BitmapBuffer buffer = _backBuffer.LockBuffer(BitmapBufferAccessMode.Read))
                    {
                        using (var reference = buffer.CreateReference())
                        {
                            unsafe
                            {
                                byte* dataInBytes;
                                uint capacity;
                                reference.As<IMemoryBufferByteAccess>().GetBuffer(out dataInBytes, out capacity);
                                byte[] nv12Buffer = new byte[capacity];
                                Marshal.Copy((IntPtr)dataInBytes, nv12Buffer, 0, (int)capacity);

                                if (OnVideoSourceEncodedSample != null)
                                {
                                    lock (_videoEncoder)
                                    {
                                        var i420 = PixelConverter.NV12toI420(nv12Buffer, width, height);

                                        var encodedBuffer = _videoEncoder.EncodeVideo(width, height, i420, VideoPixelFormatsEnum.I420, VideoCodecsEnum.VP8);

                                        if (encodedBuffer != null)
                                        {
                                            uint durationRtpUnits = 90000 / 30;
                                            OnVideoSourceEncodedSample.Invoke(durationRtpUnits, encodedBuffer);
                                        }
                                    }
                                }

                                if (OnVideoSourceRawSample != null)
                                {
                                    uint frameSpacing = 0;
                                    if (_lastFrameAt != DateTime.MinValue)
                                    {
                                        frameSpacing = Convert.ToUInt32(DateTime.Now.Subtract(_lastFrameAt).TotalMilliseconds);
                                    }

                                    var bgrBuffer = PixelConverter.NV12toBGR(nv12Buffer, width, height, width * 3);

                                    OnVideoSourceRawSample.Invoke(frameSpacing, width, height, bgrBuffer, VideoPixelFormatsEnum.Bgr);
                                }
                            }
                        }
                    }

                    _backBuffer?.Dispose();
                    softwareBitmap?.Dispose();
                }

                _lastFrameAt = DateTime.Now;
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