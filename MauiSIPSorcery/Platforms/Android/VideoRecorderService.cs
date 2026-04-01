using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using Bumptech.Glide.Util;
using Java.Nio;
using Java.Util.Concurrent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using AndroidX.Lifecycle;
using Executors = Java.Util.Concurrent.Executors;
using Java.Lang;
using Microsoft.Maui;
using AndroidX.Camera.Core.ResolutionSelector;
using System.Runtime.Versioning;
using Android.Graphics;
using Exception = Java.Lang.Exception;
using static Android.Graphics.Bitmap;
using System.Threading.Tasks;
using MauiSIPSorcery.Interfaces;

namespace MauiSIPSorcery.Platforms.Android
{
    public class VideoRecorderService : IVideoRecorder
    {
        ImageAnalysis imageAnalysis;
        ProcessCameraProvider cameraProvider;

        public event Action<byte[]> OnVideoFrameArrived;

        private bool _isRecording;


        public void StartRecording()
        {
            if (_isRecording) return;

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

            imageAnalysis.SetAnalyzer(Executors.NewSingleThreadExecutor(), new FrameAnalyzer(OnVideoFrameArrived));

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
            public event Action<byte[]> _onVideoFrameArrived;

            public FrameAnalyzer(Action<byte[]> onVideoFrameArrived)
            {
                _onVideoFrameArrived = onVideoFrameArrived;
            }

            public void Analyze(IImageProxy imageProxy)
            {
                try
                {
                    var rotation = imageProxy.ImageInfo.RotationDegrees; // 获取设备方向

                    var bitmap = imageProxy.ToBitmap();

                    var newBitmap = ApplyRotation(bitmap, rotation);

                    using var stream = new MemoryStream();
                    newBitmap?.Compress(CompressFormat.Jpeg, 60, stream);
                    newBitmap?.Recycle();

                    _onVideoFrameArrived?.Invoke(stream.ToArray());
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
