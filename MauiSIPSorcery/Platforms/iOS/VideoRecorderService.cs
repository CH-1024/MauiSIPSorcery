using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using CoreImage;
using CoreMedia;
using CoreVideo;
using Foundation;
using MauiSIPSorcery.Interfaces;
using MediaPlayer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using UIKit;

namespace MauiSIPSorcery.Platforms.iOS
{
    public class VideoRecorderService : IVideoRecorder
    {
        private AVCaptureSession _captureSession;
        private AVCaptureDeviceInput _deviceInput;
        private AVCaptureVideoDataOutput _videoOutput;

        public event Action<byte[]> OnVideoFrameArrived;

        private bool _isRecording;


        public void StartRecording()
        {
            if (_isRecording) return;

            _captureSession = new AVCaptureSession
            {
                SessionPreset = AVCaptureSession.PresetLow, // 设置分辨率
            };

            foreach (var _input in _captureSession.Inputs)
            {
                _captureSession.RemoveInput(_input);
                _input.Dispose();
            }

            var deviceTypes = new AVCaptureDeviceType[]
            {
               AVCaptureDeviceType.BuiltInTrueDepthCamera,
               AVCaptureDeviceType.BuiltInDualCamera,
               AVCaptureDeviceType.BuiltInWideAngleCamera
            };
            var discoverySession = AVCaptureDeviceDiscoverySession.Create(deviceTypes, AVMediaTypes.Video, AVCaptureDevicePosition.Front);
            var device = discoverySession.Devices.FirstOrDefault();
            _deviceInput = AVCaptureDeviceInput.FromDevice(device);
            _captureSession.AddInput(_deviceInput);

            // 配置视频输出
            _videoOutput = new AVCaptureVideoDataOutput
            {
                AlwaysDiscardsLateVideoFrames = true,
                MinFrameDuration = new CMTime(1, 30),
                WeakVideoSettings = new NSDictionary(CVPixelBuffer.PixelFormatTypeKey, (int)CVPixelFormatType.CV32BGRA),
            };

            var _queue = new DispatchQueue("myQueue");
            var _delegate = new VideoDataDelegate(OnVideoFrameArrived);
            _videoOutput.SetSampleBufferDelegate(_delegate, _queue);
            _captureSession.AddOutput(_videoOutput);

            foreach (var connection in _videoOutput.Connections)
            {
                // 设置视频方向
                if (connection.SupportsVideoOrientation)
                {
                    connection.VideoOrientation = AVCaptureVideoOrientation.Portrait;
                }

                // 设置视频镜像
                if (connection.SupportsVideoMirroring)
                {
                    connection.VideoMirrored = true;
                }
            }

            // 开始捕获
            _captureSession.StartRunning();
            _isRecording = true;
        }


        public void StopRecording()
        {
            if (!_isRecording) return;

            _isRecording = false;
            _captureSession?.StopRunning();

            _deviceInput.Dispose();
            _videoOutput?.Dispose();
            _captureSession?.Dispose();
        }


        static AVCaptureVideoOrientation GetVideoOrientation()
        {
            IEnumerable<UIScene> scenes = UIApplication.SharedApplication.ConnectedScenes;
            var interfaceOrientation = scenes.FirstOrDefault() is UIWindowScene windowScene ? windowScene.InterfaceOrientation : UIApplication.SharedApplication.StatusBarOrientation;

            return interfaceOrientation switch
            {
                UIInterfaceOrientation.Portrait => AVCaptureVideoOrientation.Portrait,
                UIInterfaceOrientation.PortraitUpsideDown => AVCaptureVideoOrientation.PortraitUpsideDown,
                UIInterfaceOrientation.LandscapeRight => AVCaptureVideoOrientation.LandscapeRight,
                UIInterfaceOrientation.LandscapeLeft => AVCaptureVideoOrientation.LandscapeLeft,
                _ => AVCaptureVideoOrientation.Portrait
            };
        }


        // 内部类处理帧数据
        private class VideoDataDelegate : AVCaptureVideoDataOutputSampleBufferDelegate
        {
            public event Action<byte[]> _onVideoFrameArrived;

            public VideoDataDelegate(Action<byte[]> onVideoFrameArrived)
            {
                _onVideoFrameArrived = onVideoFrameArrived;
            }

            public override void DidOutputSampleBuffer(AVCaptureOutput output, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
            {
                using (sampleBuffer)
                {
                    try
                    {
                        // 将帧转换为字节数组（示例：转换为 JPEG）
                        using var imageBuffer = sampleBuffer.GetImageBuffer();
                        using var ciImage = new CIImage(imageBuffer);
                        using var uiImage = new UIImage(ciImage);
                        using var imageData = uiImage.AsJPEG(0.5f);
                        var bytes = imageData.ToArray();

                        _onVideoFrameArrived?.Invoke(bytes);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
        }
    }
}
