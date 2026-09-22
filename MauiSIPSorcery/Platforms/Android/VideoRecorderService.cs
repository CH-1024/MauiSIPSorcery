using Android.Graphics;
using Android.Hardware.Lights;
using AndroidX.Camera.Core;
using AndroidX.Camera.Core.ResolutionSelector;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Bumptech.Glide.Util;
using Java.Lang;
using Java.Nio;
using Java.Util.Concurrent;
using MauiSIPSorcery.Interfaces;
using Microsoft.Maui;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static Android.Graphics.Bitmap;
using static Android.Icu.Text.ListFormatter;
using Exception = Java.Lang.Exception;
using Executors = Java.Util.Concurrent.Executors;

namespace MauiSIPSorcery.Platforms.Android
{
    public class VideoRecorderService : IVideoRecorder
    {
        public IVideoEncoder _videoEncoder;

        ImageAnalysis imageAnalysis;
        ProcessCameraProvider cameraProvider;

        public event Action<byte[]> OnVideoFrameArrived;
        public event Action<uint, byte[]> OnVideoSourceEncodedSample;   // uint durationRtpUnits, byte[] sample
        public event Action<uint, int, int, byte[], VideoPixelFormatsEnum> OnVideoSourceRawSample;   // uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat

        private bool _isRecording;


        public void StartRecording(IVideoEncoder encoder)
        {
            if (_isRecording) return;

            _videoEncoder = encoder;

            var cameraProviderFuture = ProcessCameraProvider.GetInstance(Platform.AppContext);

            cameraProvider = (ProcessCameraProvider)cameraProviderFuture.Get();

            //// 配置预览
            //var preview = new Preview.Builder().Build();
            //preview.SetSurfaceProvider(previewView.SurfaceProvider);

            // 配置帧分析
            imageAnalysis = new ImageAnalysis.Builder()
                .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest) // 只保留最新帧
                .SetOutputImageFormat(ImageAnalysis.OutputImageFormatRgba8888) // 设置输出格式
                .Build();

            imageAnalysis.SetAnalyzer(Executors.NewSingleThreadExecutor(), new FrameAnalyzer(this));

            var lifecycleOwner = (ILifecycleOwner)Platform.CurrentActivity;
            var cameraSelector = CameraSelector.DefaultFrontCamera;

            cameraProvider.UnbindAll();

            // 绑定帧分析
            cameraProvider.BindToLifecycle(lifecycleOwner, cameraSelector/*, preview*/, imageAnalysis);

            _isRecording = true;
        }


        public void StopRecording()
        {
            if (!_isRecording) return;

            _isRecording = false;
            imageAnalysis?.ClearAnalyzer();
            cameraProvider.ShutdownAsync();
            cameraProvider.UnbindAll();

            cameraProvider.Dispose();
            imageAnalysis.Dispose();
        }

        // 自定义帧分析器
        public class FrameAnalyzer : Java.Lang.Object, ImageAnalysis.IAnalyzer
        {
            VideoRecorderService _recorderService;

            public FrameAnalyzer(VideoRecorderService recorderService)
            {
                _recorderService = recorderService;
            }

            private DateTime _lastFrameAt = DateTime.MinValue;

            public void Analyze(IImageProxy imageProxy)
            {
                try
                {
                    var rotation = imageProxy.ImageInfo.RotationDegrees; // 获取设备方向

                    var bitmap = imageProxy.ToBitmap();

                    var newBitmap = ApplyRotation(bitmap, rotation);

                    var w = newBitmap.Width;
                    var h = newBitmap.Height;

                    // 确保 Bitmap 是 RGBA8888
                    // RGBA8888 = 4 bytes / pixel
                    var rgba = new byte[w * h * 4];

                    using (var buffer = ByteBuffer.Allocate(rgba.Length))
                    {
                        newBitmap.CopyPixelsToBuffer(buffer);

                        buffer.Rewind();
                        buffer.Get(rgba);
                    }

                    if (_recorderService.OnVideoSourceEncodedSample != null)
                    {
                        // RGBA -> I420
                        var i420 = PixelConverter.RGBAtoI420(rgba, w, h, w * 4);

                        var encodedBuffer = _recorderService._videoEncoder.EncodeVideo(w, h, i420, VideoPixelFormatsEnum.I420, VideoCodecsEnum.VP8);

                        if (encodedBuffer != null)
                        {
                            uint durationRtpUnits = 90000 / 30;
                            _recorderService.OnVideoSourceEncodedSample.Invoke(durationRtpUnits, encodedBuffer);
                        }
                    }

                    if (_recorderService.OnVideoSourceRawSample != null)
                    {
                        uint frameSpacing = 0;
                        if (_lastFrameAt != DateTime.MinValue)
                        {
                            frameSpacing = Convert.ToUInt32(DateTime.Now.Subtract(_lastFrameAt).TotalMilliseconds);
                        }

                        _recorderService.OnVideoSourceRawSample.Invoke(frameSpacing, w, h, rgba, VideoPixelFormatsEnum.Rgba);
                    }
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

        }

    }
}
