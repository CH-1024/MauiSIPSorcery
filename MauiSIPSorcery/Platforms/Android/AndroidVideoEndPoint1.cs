using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.Util;
using Android.Views;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Size = Android.Util.Size;

namespace MauiSIPSorcery.Platforms.Android
{
    public class AndroidVideoEndPoint1 : IVideoSource, IVideoSink, IDisposable
    {
        private const VideoPixelFormatsEnum EncoderInputFormat = VideoPixelFormatsEnum.NV12;

        private readonly IVideoEncoder _videoEncoder;
        private readonly MediaFormatManager<VideoFormat> _videoFormatManager;

        private CameraManager _cameraManager;
        private CameraDevice _cameraDevice;
        private CameraCaptureSession _captureSession;
        private ImageReader _imageReader;
        private string _cameraId;
        private Size _previewSize;

        private bool _isInitialised;
        private bool _isStarted;
        private bool _isPaused;
        private bool _isClosed;
        private uint _fps;
        private DateTime _lastFrameAt = DateTime.MinValue;

        public event RawVideoSampleDelegate OnVideoSourceRawSample;
        public event RawVideoSampleFasterDelegate OnVideoSourceRawSampleFaster;
        public event EncodedSampleDelegate OnVideoSourceEncodedSample;
        public event VideoSinkSampleDecodedDelegate OnVideoSinkDecodedSample;
        public event VideoSinkSampleDecodedFasterDelegate OnVideoSinkDecodedSampleFaster;
        public event SourceErrorDelegate OnVideoSourceError;

        public AndroidVideoEndPoint1(IVideoEncoder videoEncoder, uint width = 0, uint height = 0, uint fps = 30)
        {
            _videoEncoder = videoEncoder;
            _fps = fps;

            _videoFormatManager = new MediaFormatManager<VideoFormat>(videoEncoder.SupportedFormats);
            _cameraManager = (CameraManager)Platform.AppContext.GetSystemService(Context.CameraService);
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

        public async Task StartVideo()
        {
            if (_isStarted) return;

            try
            {
                if (!_isInitialised)
                {
                    await InitializeCameraAsync();
                    _isInitialised = true;
                }

                CreateCaptureSession();
                _isStarted = true;
            }
            catch (Exception ex)
            {
                OnVideoSourceError?.Invoke($"StartVideo failed: {ex.Message}");
            }
        }

        public Task PauseVideo()
        {
            _isPaused = true;
            // 实际实现中需要停止帧捕获
            return Task.CompletedTask;
        }

        public Task ResumeVideo()
        {
            _isPaused = false;
            // 实际实现中需要重启帧捕获
            return Task.CompletedTask;
        }

        public async Task CloseVideo()
        {
            if (_isClosed) return;
            _isClosed = true;

            try
            {
                _captureSession?.StopRepeating();
                _captureSession?.Close();
                _cameraDevice?.Close();
                _imageReader?.Close();

                _videoEncoder?.Dispose();
            }
            catch (Exception ex)
            {
            }
        }

        private async Task InitializeCameraAsync()
        {
            try
            {
                _cameraId = GetFrontCameraId();
                if (string.IsNullOrEmpty(_cameraId))
                    throw new ApplicationException("No front-facing camera found");

                var characteristics = _cameraManager.GetCameraCharacteristics(_cameraId);
                var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);

                // 选择最佳预览尺寸
                _previewSize = ChooseOptimalSize(map.GetOutputSizes((int)ImageFormatType.Yuv420888));

                // 创建ImageReader
                _imageReader = ImageReader.NewInstance(_previewSize.Width, _previewSize.Height, ImageFormatType.Yuv420888, 2);

                _imageReader.SetOnImageAvailableListener(new ImageAvailableListener(ProcessImage), null);

                _cameraManager.OpenCamera(_cameraId, new CameraStateCallback(this), null);
            }
            catch (Exception ex)
            {
                OnVideoSourceError?.Invoke($"Camera initialization failed: {ex.Message}");
            }
        }

        private Size ChooseOptimalSize(Size[] choices)
        {
            //const double ASPECT_TOLERANCE = 0.1;
            //var targetRatio = (double)_videoFormatManager.SelectedFormat.Width / _videoFormatManager.SelectedFormat.Height;

            //// 过滤不满足要求的尺寸
            //var viableSizes = choices.Where(size => Math.Abs((double)size.Width / size.Height - targetRatio) < ASPECT_TOLERANCE).ToList();

            //// 选择最接近目标分辨率的尺寸
            //return viableSizes.OrderBy(size => Math.Abs(size.Width - _videoFormatManager.SelectedFormat.Width)).ThenBy(size => Math.Abs(size.Height - _videoFormatManager.SelectedFormat.Height)).FirstOrDefault();

            return choices.OrderByDescending(size => size.Width * size.Height).FirstOrDefault();
        }

        private string GetFrontCameraId()
        {
            foreach (var id in _cameraManager.GetCameraIdList())
            {
                var characteristics = _cameraManager.GetCameraCharacteristics(id);
                var facingObj = characteristics.Get(CameraCharacteristics.LensFacing);
                if (facingObj is Java.Lang.Integer facingInt && facingInt.IntValue() == (int)LensFacing.Front)
                    return id;
            }
            return null;
        }

        private void CreateCaptureSession()
        {
            var targets = new List<Surface> { _imageReader.Surface };
            var stateCallback = new CaptureSessionStateCallback(this);

            _cameraDevice.CreateCaptureSession(targets, stateCallback, null);
        }

        private void StartPreview()
        {
            if (_cameraDevice == null || _captureSession == null) return;

            var builder = _cameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
            builder.AddTarget(_imageReader.Surface);
            builder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousVideo);
            builder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.On);

            _captureSession.SetRepeatingRequest(builder.Build(), null, null);
        }

        private void ProcessImage(ImageReader reader)
        {
            if (_isClosed || _isPaused) return;

            using (var image = reader.AcquireLatestImage())
            {
                if (image == null) return;

                var planes = image.GetPlanes();
                var yBuffer = planes[0].Buffer;
                var uBuffer = planes[1].Buffer;
                var vBuffer = planes[2].Buffer;

                int width = image.Width;
                int height = image.Height;
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

        public void Dispose() => CloseVideo().Wait();


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

        // Camera2 API回调类
        private class CameraStateCallback : CameraDevice.StateCallback
        {
            private readonly AndroidVideoEndPoint1 _endPoint;

            public CameraStateCallback(AndroidVideoEndPoint1 endPoint) => _endPoint = endPoint;

            public override void OnOpened(CameraDevice camera) => _endPoint._cameraDevice = camera;

            public override void OnDisconnected(CameraDevice camera) => camera.Close();

            public override void OnError(CameraDevice camera, CameraError error) => _endPoint.OnVideoSourceError?.Invoke($"Camera error: {error}");
        }

        private class CaptureSessionStateCallback : CameraCaptureSession.StateCallback
        {
            private readonly AndroidVideoEndPoint1 _endPoint;

            public CaptureSessionStateCallback(AndroidVideoEndPoint1 endPoint) => _endPoint = endPoint;

            public override void OnConfigured(CameraCaptureSession session)
            {
                _endPoint._captureSession = session;
                _endPoint.StartPreview();
            }

            public override void OnConfigureFailed(CameraCaptureSession session) => _endPoint.OnVideoSourceError?.Invoke("Capture session configuration failed");
        }

        private class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
        {
            private readonly Action<ImageReader> _processImage;

            public ImageAvailableListener(Action<ImageReader> processImage) => _processImage = processImage;

            public void OnImageAvailable(ImageReader reader) => _processImage(reader);
        }

    }
}
