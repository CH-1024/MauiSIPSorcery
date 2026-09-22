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
using SIPSorceryMedia.Abstractions;
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
        public IVideoEncoder _videoEncoder;

        private AVCaptureSession _captureSession;
        private AVCaptureDeviceInput _deviceInput;
        private AVCaptureVideoDataOutput _videoOutput;

        public event Action<byte[]> OnVideoFrameArrived;
        public event Action<uint, byte[]> OnVideoSourceEncodedSample;   // uint durationRtpUnits, byte[] sample
        public event Action<uint, int, int, byte[], VideoPixelFormatsEnum> OnVideoSourceRawSample;   // uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat

        private bool _isRecording;


        public void StartRecording(IVideoEncoder encoder)
        {
            if (_isRecording) return;

            _videoEncoder = encoder;

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
            var _delegate = new VideoDataDelegate(this);
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
            VideoRecorderService _videoRecorder;

            public VideoDataDelegate(VideoRecorderService videoRecorder)
            {
                _videoRecorder = videoRecorder;
            }

            public override void DidOutputSampleBuffer(AVCaptureOutput output, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
            {
                using (sampleBuffer)
                {
                    try
                    {
                        var imageBuffer = sampleBuffer.GetImageBuffer();

                        if (imageBuffer is not CVPixelBuffer pixelBuffer)
                            return;

                        int width = (int)pixelBuffer.Width;
                        int height = (int)pixelBuffer.Height;
                        int bytesPerRow = (int)pixelBuffer.BytesPerRow;

                        pixelBuffer.Lock(CVPixelBufferLock.ReadOnly);

                        try
                        {
                            var bgra = new byte[width * height * 4];

                            var baseAddress = pixelBuffer.BaseAddress;

                            // 如果没有 padding，可以直接复制
                            if (bytesPerRow == width * 4)
                            {
                                Marshal.Copy(baseAddress, bgra, 0, bgra.Length);
                            }
                            else
                            {
                                // 有 stride 时逐行复制
                                for (int y = 0; y < height; y++)
                                {
                                    Marshal.Copy(IntPtr.Add(baseAddress, y * bytesPerRow), bgra, y * width * 4, width * 4);
                                }
                            }

                            if (_videoRecorder.OnVideoSourceEncodedSample != null)
                            {
                                // BGRA -> I420
                                var i420 = PixelConverter.BGRAtoI420(bgra, width, height, width * 4);

                                var encodedBuffer = _videoRecorder._videoEncoder.EncodeVideo(width, height, i420, VideoPixelFormatsEnum.I420, VideoCodecsEnum.VP8);

                                if (encodedBuffer != null)
                                {
                                    uint durationRtpUnits = 90000 / 30;

                                    _videoRecorder.OnVideoSourceEncodedSample.Invoke(durationRtpUnits, encodedBuffer);
                                }
                            }
                        }
                        finally
                        {
                            pixelBuffer.Unlock(CVPixelBufferLock.ReadOnly);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }

        }
    }
}
