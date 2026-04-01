using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using WinRT;

namespace MauiSIPSorcery.Platforms.Windows
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    public struct VideoCaptureDeviceInfo
    {
        public string ID;
        public string Name;
    }

    public class WindowsVideoEndPoint : IVideoSource, IVideoSink, IDisposable
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

        private MediaCapture _mediaCapture;
        private MediaFrameReader _mediaFrameReader;
        private SoftwareBitmap _backBuffer;

        public event RawVideoSampleDelegate OnVideoSourceRawSample;
        public event RawVideoSampleFasterDelegate OnVideoSourceRawSampleFaster;
        public event EncodedSampleDelegate OnVideoSourceEncodedSample;
        public event VideoSinkSampleDecodedDelegate OnVideoSinkDecodedSample;
        public event VideoSinkSampleDecodedFasterDelegate OnVideoSinkDecodedSampleFaster;
        public event SourceErrorDelegate OnVideoSourceError;

        public WindowsVideoEndPoint(IVideoEncoder videoEncoder, uint width = 0, uint height = 0, uint fps = 0)
        {
            _width = width;
            _height = height;
            _fps = fps;
            _videoEncoder = videoEncoder;

            _mediaCapture = new MediaCapture();
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

        public Task PauseVideo()
        {
            _isPaused = true;

            if (_mediaFrameReader != null)
            {
                return _mediaFrameReader.StopAsync().AsTask();
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        public Task ResumeVideo()
        {
            _isPaused = false;

            if (_mediaFrameReader != null)
            {
                return _mediaFrameReader.StartAsync().AsTask();
            }
            else
            {
                return Task.CompletedTask;
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

                await _mediaFrameReader.StartAsync().AsTask().ConfigureAwait(false);
            }
        }

        public async Task CloseVideo()
        {
            if (!_isClosed)
            {
                _isClosed = true;

                await CloseVideoCaptureDevice().ConfigureAwait(false);

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
                var mediaCaptureSettings = new MediaCaptureInitializationSettings()
                {
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                    MediaCategory = MediaCategory.Communications
                };

                await _mediaCapture.InitializeAsync(mediaCaptureSettings).AsTask().ConfigureAwait(false);

                MediaFrameSourceInfo colorSourceInfo = null;
                foreach (var srcInfo in _mediaCapture.FrameSources)
                {
                    if (srcInfo.Value.Info.MediaStreamType == MediaStreamType.VideoRecord && srcInfo.Value.Info.SourceKind == MediaFrameSourceKind.Color)
                    {
                        colorSourceInfo = srcInfo.Value.Info;
                        break;
                    }
                }

                var colorFrameSource = _mediaCapture.FrameSources[colorSourceInfo.Id];

                var preferredFormat = colorFrameSource.SupportedFormats.Where(format =>
                {
                    return format.VideoFormat.Width >= _width && format.VideoFormat.Width >= _height && (format.FrameRate.Numerator / format.FrameRate.Denominator) >= fps && format.Subtype == MediaEncodingSubtypes.Nv12;
                }).FirstOrDefault();

                if (preferredFormat == null)
                {
                    preferredFormat = colorFrameSource.SupportedFormats.Where(format =>
                    {
                        return format.VideoFormat.Width >= _width && format.VideoFormat.Width >= _height && (format.FrameRate.Numerator / format.FrameRate.Denominator) >= fps;
                    }).FirstOrDefault();
                }

                if (preferredFormat == null)
                {
                    preferredFormat = colorFrameSource.SupportedFormats.FirstOrDefault();
                }

                if (preferredFormat == null)
                {
                    throw new ApplicationException("The video capture device does not support a compatible video format for the requested parameters.");
                }

                await colorFrameSource.SetFormatAsync(preferredFormat).AsTask().ConfigureAwait(false);

                _mediaFrameReader = await _mediaCapture.CreateFrameReaderAsync(colorFrameSource).AsTask().ConfigureAwait(false);
                _mediaFrameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

                // Frame source and format have now been successfully set.
                _width = preferredFormat.VideoFormat.Width;
                _height = preferredFormat.VideoFormat.Height;
                _fps = preferredFormat.FrameRate.Numerator / preferredFormat.FrameRate.Denominator;

                _mediaFrameReader.FrameArrived += FrameArrivedHandler;

                return true;
            }
            catch (Exception ex)
            {
                OnVideoSourceError?.Invoke(ex.Message);
                return false;
            }
        }

        private async void FrameArrivedHandler(MediaFrameReader sender, MediaFrameArrivedEventArgs e)
        {
            if (!_isClosed)
            {
                if (!_videoFormatManager.SelectedFormat.IsEmpty() && (OnVideoSourceEncodedSample != null || OnVideoSourceRawSample != null))
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
                                                var encodedBuffer = _videoEncoder.EncodeVideo(width, height, nv12Buffer, EncoderInputFormat, _videoFormatManager.SelectedFormat.Codec);

                                                if (encodedBuffer != null)
                                                {
                                                    uint durationRtpUnits = (uint)(90000 / ((_fps > 0) ? _fps : 30));
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
            }
        }

        private async Task CloseVideoCaptureDevice()
        {
            if (_mediaFrameReader != null)
            {
                _mediaFrameReader.FrameArrived -= FrameArrivedHandler;
                await _mediaFrameReader.StopAsync().AsTask().ConfigureAwait(false);
            }

            if (_mediaCapture != null && _mediaCapture.CameraStreamState == CameraStreamState.Streaming)
            {
                await _mediaCapture.StopRecordAsync().AsTask().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            CloseVideoCaptureDevice().Wait();
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
