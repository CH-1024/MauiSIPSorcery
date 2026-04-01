using Android.Graphics;
using Android.Hardware.Lights;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Lifecycle;
using Java.Util.Concurrent;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Android.Graphics.Bitmap;
using static Android.Icu.Text.ListFormatter;
using static MauiSIPSorcery.Platforms.Android.VideoRecorderService;

namespace MauiSIPSorcery.Platforms.Android
{
    public class FrameAnalyzer : Java.Lang.Object, ImageAnalysis.IAnalyzer
    {
        private Action<IImageProxy> _analyze;

        public FrameAnalyzer(Action<IImageProxy> analyze)
        {
            _analyze = analyze;
        }

        public void Analyze(IImageProxy imageProxy)
        {
            _analyze?.Invoke(imageProxy);
        }
    }

    public class AndroidVideoEndPoint : IVideoSource, IVideoSink, IDisposable
    {
        private readonly VideoPixelFormatsEnum EncoderInputFormat = VideoPixelFormatsEnum.NV12;

        private MediaFormatManager<VideoFormat> _videoFormatManager;
        private IVideoEncoder _videoEncoder;
        private bool _isInitialised;
        private bool _isStarted;
        private bool _isPaused;
        private bool _isClosed;
        private uint _width = 0;
        private uint _height = 0;
        private uint _fps = 0;
        private DateTime _lastFrameAt = DateTime.MinValue;

        ImageAnalysis imageAnalysis;
        ProcessCameraProvider cameraProvider;

        public event RawVideoSampleDelegate OnVideoSourceRawSample;
        public event RawVideoSampleFasterDelegate OnVideoSourceRawSampleFaster;
        public event EncodedSampleDelegate OnVideoSourceEncodedSample;
        public event VideoSinkSampleDecodedDelegate OnVideoSinkDecodedSample;
        public event VideoSinkSampleDecodedFasterDelegate OnVideoSinkDecodedSampleFaster;
        public event SourceErrorDelegate OnVideoSourceError;

        public AndroidVideoEndPoint(IVideoEncoder videoEncoder, uint width = 0, uint height = 0, uint fps = 0)
        {
            _width = width;
            _height = height;
            _fps = fps;
            _videoEncoder = videoEncoder;

            _videoFormatManager = new MediaFormatManager<VideoFormat>(videoEncoder.SupportedFormats);
        }

        public List<VideoFormat> GetVideoSourceFormats() => _videoFormatManager.GetSourceFormats();

        public void SetVideoSourceFormat(VideoFormat videoFormat) => _videoFormatManager.SetSelectedFormat(videoFormat);

        public void GotVideoFrame(IPEndPoint remoteEndPoint, uint timestamp, byte[] frame, VideoFormat format)
        {
            if (!_isClosed)
            {
                //DateTime startTime = DateTime.Now;

                var decodedFrames = _videoEncoder.DecodeVideo(frame, EncoderInputFormat, _videoFormatManager.SelectedFormat.Codec);

                if (decodedFrames == null)
                {
                    // "VPX decode of video sample failed."
                }
                else
                {
                    foreach (var decodedFrame in decodedFrames)
                    {
                        OnVideoSinkDecodedSample(decodedFrame.Sample, decodedFrame.Width, decodedFrame.Height, (int)(decodedFrame.Width * 3), VideoPixelFormatsEnum.Bgr);
                    }
                }
            }
        }

        public async Task PauseVideo()
        {
            _isPaused = true;

            if (imageAnalysis != null)
            {
                imageAnalysis.ClearAnalyzer();
                await Task.CompletedTask;
            }
            else
            {
                await Task.CompletedTask;
            }
        }

        public async Task ResumeVideo()
        {
            if (!_isPaused)
            {
                _isPaused = false;

                if (imageAnalysis != null)
                {
                    imageAnalysis.SetAnalyzer(Executors.NewSingleThreadExecutor(), new FrameAnalyzer(FrameArrivedHandler));
                    await Task.CompletedTask;
                }
                else
                {
                    await Task.CompletedTask;
                }
            }
        }

        public async Task StartVideo()
        {
            if (!_isStarted)
            {
                _isStarted = true;

                if (!_isInitialised)
                {
                    await InitialiseVideoSourceDevice().ConfigureAwait(false);
                }

                imageAnalysis.SetAnalyzer(Executors.NewSingleThreadExecutor(), new FrameAnalyzer(FrameArrivedHandler));
            }
        }

        public async Task CloseVideo()
        {
            if (!_isClosed)
            {
                _isClosed = true;

                CloseVideoCaptureDevice();

                if (_videoEncoder != null)
                {
                    lock (_videoEncoder)
                    {
                        Dispose();
                    }
                }
                else
                {
                    Dispose();
                }
                await Task.CompletedTask;
            }
        }

        // -----------------------------------------------------

        private Task<bool> InitialiseVideoSourceDevice()
        {
            if (!_isInitialised)
            {
                _isInitialised = true;
                return InitialiseDevice(_width, _height, _fps);
            }
            else
            {
                return Task.FromResult(true);
            }
        }

        private async Task<bool> InitialiseDevice(uint width, uint height, uint fps)
        {
            try
            {
                imageAnalysis = new ImageAnalysis.Builder()
                .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest) // 只保留最新帧
                .SetOutputImageFormat(ImageAnalysis.OutputImageFormatYuv420888) // 设置输出格式
                .Build();

                var lifecycleOwner = (ILifecycleOwner)Platform.CurrentActivity;
                var cameraSelector = CameraSelector.DefaultFrontCamera;

                var cameraProviderFuture = ProcessCameraProvider.GetInstance(Platform.AppContext);
                cameraProvider = (ProcessCameraProvider)cameraProviderFuture.Get();
                cameraProvider.UnbindAll();

                // 绑定帧分析
                cameraProvider.BindToLifecycle(lifecycleOwner, cameraSelector/*, preview*/, imageAnalysis);

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                OnVideoSourceError?.Invoke(ex.Message);
                return await Task.FromResult(false);
            }
        }

        private void FrameArrivedHandler(IImageProxy imageProxy)
        {
            if (!_isClosed)
            {
                if (!_videoFormatManager.SelectedFormat.IsEmpty() && (OnVideoSourceEncodedSample != null || OnVideoSourceRawSample != null))
                {
                    try
                    {
                        //var rotation = imageProxy.ImageInfo.RotationDegrees; // 获取设备方向

                        //var bitmap = imageProxy.ToBitmap();

                        //var newBitmap = ApplyRotation(bitmap, rotation);

                        //using var stream = new MemoryStream();
                        //newBitmap.Compress(CompressFormat.Jpeg, 60, stream);
                        //var bgrBuffer = stream.ToArray();



                        // 获取YUV平面
                        var planes = imageProxy.Image.GetPlanes();
                        var yBuffer = planes[0].Buffer;
                        var uBuffer = planes[1].Buffer;
                        var vBuffer = planes[2].Buffer;

                        int width = imageProxy.Image.Width;
                        int height = imageProxy.Image.Height;
                        int ySize = yBuffer.Remaining();
                        int uvSize = uBuffer.Remaining() + vBuffer.Remaining();

                        // 转换为NV12格式
                        var nv12Buffer = ConvertYUV420ToNV12(yBuffer, uBuffer, vBuffer, width, height, planes[0].RowStride, planes[1].PixelStride);

                        if (OnVideoSourceEncodedSample != null)
                        {
                            lock (_videoEncoder)
                            {
                                var encodedBuffer = _videoEncoder.EncodeVideo(width, height, nv12Buffer, EncoderInputFormat, _videoFormatManager.SelectedFormat.Codec);

                                if (encodedBuffer != null)
                                {
                                    uint durationRtpUnits = (uint)(90000 / (_fps > 0 ? _fps : 30));
                                    OnVideoSourceEncodedSample(durationRtpUnits, encodedBuffer);
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
                            OnVideoSourceRawSample(frameSpacing, width, height, bgrBuffer, VideoPixelFormatsEnum.Bgr);
                        }

                        _lastFrameAt = DateTime.Now;

                        //newBitmap?.Recycle();

                    }
                    catch (Exception ex)
                    {

                    }
                    finally
                    {
                        // 必须关闭以释放资源
                        imageProxy.Close();
                    }
                }
            }
        }

        private byte[] ConvertYUV420ToNV12(Java.Nio.ByteBuffer yBuffer, Java.Nio.ByteBuffer uBuffer, Java.Nio.ByteBuffer vBuffer, int width, int height, int yStride, int uvPixelStride)
        {
            byte[] nv12 = new byte[width * height * 3 / 2];
            int yPos = 0;
            int uvPos = width * height;

            // 复制Y平面
            yBuffer.Rewind();
            for (int y = 0; y < height; y++)
            {
                yBuffer.Position(y * yStride);
                yBuffer.Get(nv12, yPos, width);
                yPos += width;
            }

            // 合并UV平面 (注意Android的UV是分开的)
            uBuffer.Rewind();
            vBuffer.Rewind();
            for (int y = 0; y < height / 2; y++)
            {
                for (int x = 0; x < width / 2; x++)
                {
                    int uvIndex = y * (uvPixelStride * (width / 2)) + x * uvPixelStride;
                    nv12[uvPos++] = (byte)vBuffer.Get(uvIndex); // V分量
                    nv12[uvPos++] = (byte)uBuffer.Get(uvIndex); // U分量
                }
            }

            return nv12;
        }

        private Bitmap ApplyRotation(Bitmap sourceBitmap, int rotationDegrees)
        {
            if (sourceBitmap == null) return sourceBitmap;

            Matrix matrix = new Matrix();
            switch (rotationDegrees)
            {
                case 90:
                    matrix.PostRotate(90);
                    matrix.PostScale(-1, 1);
                    break;
                case 180:
                    matrix.PostRotate(180);
                    break;
                case 270:
                    matrix.PostRotate(270);
                    matrix.PostScale(-1, 1);
                    break;
            }

            Bitmap rotatedBitmap = Bitmap.CreateBitmap(
                sourceBitmap,
                0, 0,                  // 裁剪起点 (x, y)
                sourceBitmap.Width,     // 源图像的宽度（不要修改）
                sourceBitmap.Height,    // 源图像的高度（不要修改）
                matrix,                 // 旋转矩阵（会自动处理尺寸）
                true                    // 启用抗锯齿
            );

            // 回收 Bitmap
            sourceBitmap.Recycle();

            return rotatedBitmap;
        }

        private void CloseVideoCaptureDevice()
        {
            if (imageAnalysis != null)
            {
                imageAnalysis.ClearAnalyzer();
                imageAnalysis.Dispose();
            }

            if (cameraProvider != null)
            {
                cameraProvider.ShutdownAsync();
                cameraProvider.UnbindAll();
                cameraProvider.Dispose();
            }
        }

        public void Dispose()
        {
            CloseVideoCaptureDevice();
            if (_videoEncoder != null)
            {
                lock (_videoEncoder)
                {
                    _videoEncoder.Dispose();
                }
            }
        }

        //=======================================================================================================


        public void RestrictFormats(Func<VideoFormat, bool> filter)
        {
            throw new NotImplementedException();
        }

        public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
        {
            throw new NotImplementedException();
        }

        public void ExternalVideoSourceRawSampleFaster(uint durationMilliseconds, RawImage rawImage)
        {
            throw new NotImplementedException();
        }

        public void ForceKeyFrame()
        {
            throw new NotImplementedException();
        }

        public bool HasEncodedVideoSubscribers()
        {
            throw new NotImplementedException();
        }

        public bool IsVideoSourcePaused()
        {
            throw new NotImplementedException();
        }

        public void GotVideoRtp(IPEndPoint remoteEndPoint, uint ssrc, uint seqnum, uint timestamp, int payloadID, bool marker, byte[] payload)
        {
            throw new NotImplementedException();
        }

        public List<VideoFormat> GetVideoSinkFormats()
        {
            throw new NotImplementedException();
        }

        public void SetVideoSinkFormat(VideoFormat videoFormat)
        {
            throw new NotImplementedException();
        }

        public Task PauseVideoSink()
        {
            throw new NotImplementedException();
        }

        public Task ResumeVideoSink()
        {
            throw new NotImplementedException();
        }

        public Task StartVideoSink()
        {
            throw new NotImplementedException();
        }

        public Task CloseVideoSink()
        {
            throw new NotImplementedException();
        }

    }

}
