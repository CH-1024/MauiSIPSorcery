using MauiSIPSorcery.Interfaces;
using Microsoft.Maui.Controls;
using Org.BouncyCastle.Utilities.Encoders;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;
using TinyJson;
using static SIPSorcery.net.RTP.Packetisation.MJPEGPacketiser;

namespace MauiSIPSorcery.UtilityTools
{
    public class WebRTCManager : IDisposable
    {
        IVideoRecorder _videoRecorder = Application.Current?.Handler?.MauiContext?.Services.GetService<IVideoRecorder>();
        ApiService _apiService = Application.Current?.Handler?.MauiContext?.Services.GetService<ApiService>();

        int TargetId;

        private RTCConfiguration _configuration;
        private RTCPeerConnection _peerConnection;
        private RTCDataChannel _dataChannel;


        public event Action<string> OnConnectionStateChanged;
        public event Action<byte[]> OnReceivedMessage;
        public event Action<byte[]> OnLocalVideoFrameReceived;
        public event Action<byte[]> OnRemoteVideoFrameReceived;
        public event Action<string> OnErrorOccurred;


        public WebRTCManager(int targetId)
        {
            TargetId = targetId;

            var useTurnServer = false;
            var gatherTime = 2000;

            _configuration = new RTCConfiguration
            {
                bundlePolicy = RTCBundlePolicy.balanced,
                iceTransportPolicy = RTCIceTransportPolicy.all,
                X_GatherTimeoutMs = gatherTime,
                iceServers =
                [
                    //new RTCIceServer { urls = "stun:stun.l.google.com:19302" },
                    new() { urls = "stun:43.142.234.223:3478", username = "cheng", credential = "12345678" },
                    new() { urls = "turn:43.142.234.223:3478", username = "cheng", credential = "12345678" },

                    //new() { urls = "stun:stun.xten.com:3478" },
                    //new() { urls = "stun:stun.voipbuster.com:3478" },
                    //new() { urls = "stun:stun.sipgate.net:3478" },
                    //new() { urls = "stun:stun.l.google.com:19302" },
                    //new() { urls = "stun:stun1.l.google.com:19302" },
                    //new() { urls = "stun:stun2.l.google.com:19302" },
                    //new() { urls = "stun:stun3.l.google.com:19302" },
                    //new() { urls = "stun:stun4.l.google.com:19302" },

                    #region MyRegion

                    //new() { urls = "stun:51.83.201.84:3478" },
                    //new() { urls = "stun:69.20.59.115:3478" },
                    //new() { urls = "stun:188.138.90.169:3478" },
                    //new() { urls = "stun:35.158.233.7:3478" },
                    //new() { urls = "stun:5.39.72.109:3478" },
                    //new() { urls = "stun:185.125.180.70:3478" },
                    //new() { urls = "stun:89.37.98.122:3478" },
                    //new() { urls = "stun:108.163.134.186:3478" },
                    //new() { urls = "stun:212.53.40.40:3478" },
                    //new() { urls = "stun:5.161.52.174:3478" },
                    //new() { urls = "stun:45.15.102.34:3478" },
                    //new() { urls = "stun:24.204.48.11:3478" },
                    //new() { urls = "stun:52.24.174.49:3478" },
                    //new() { urls = "stun:80.156.214.187:3478" },
                    //new() { urls = "stun:192.76.120.66:3478" },
                    //new() { urls = "stun:195.201.132.113:3478" },
                    //new() { urls = "stun:90.145.158.66:3478" },
                    //new() { urls = "stun:129.153.212.128:3478" },
                    //new() { urls = "stun:54.197.117.0:3478" },
                    //new() { urls = "stun:3.121.78.82:3478" },
                    //new() { urls = "stun:195.208.107.138:3478" },
                    //new() { urls = "stun:81.82.206.117:3478" },
                    //new() { urls = "stun:51.255.31.35:3478" },
                    //new() { urls = "stun:157.161.10.32:3478" },
                    //new() { urls = "stun:20.93.239.173:3478" },
                    //new() { urls = "stun:217.91.243.229:3478" },
                    //new() { urls = "stun:35.180.81.93:3478" },
                    //new() { urls = "stun:52.47.70.236:3478" },
                    //new() { urls = "stun:5.161.57.75:3478" },
                    //new() { urls = "stun:212.18.0.14:3478" },
                    //new() { urls = "stun:34.192.137.246:3478" },
                    //new() { urls = "stun:209.251.63.76:3478" },
                    //new() { urls = "stun:213.203.211.131:3478" },
                    //new() { urls = "stun:31.184.236.23:3478" },
                    //new() { urls = "stun:62.72.83.10:3478" },
                    //new() { urls = "stun:198.100.144.121:3478" },
                    //new() { urls = "stun:194.149.74.158:3478" },
                    //new() { urls = "stun:95.216.78.222:3478" },
                    //new() { urls = "stun:88.218.220.40:3478" },
                    //new() { urls = "stun:193.182.111.151:3478" },
                    //new() { urls = "stun:23.21.199.62:3478" },
                    //new() { urls = "stun:212.53.40.43:3478" },
                    //new() { urls = "stun:44.230.252.214:3478" },
                    //new() { urls = "stun:192.172.233.145:3478" },
                    //new() { urls = "stun:81.83.12.46:3478" },
                    //new() { urls = "stun:51.68.112.203:3478" },
                    //new() { urls = "stun:79.140.42.88:3478" },
                    //new() { urls = "stun:3.78.237.53:3478" },
                    //new() { urls = "stun:34.74.124.204:3478" },
                    //new() { urls = "stun:52.52.70.85:3478" },
                    //new() { urls = "stun:95.216.145.84:3478" },
                    //new() { urls = "stun:212.103.68.7:3478" },
                    //new() { urls = "stun:51.83.15.212:3478" },
                    //new() { urls = "stun:34.206.168.53:3478" },
                    //new() { urls = "stun:188.40.203.74:3478" },
                    //new() { urls = "stun:52.26.251.34:3478" },
                    //new() { urls = "stun:51.68.45.75:3478" },
                    //new() { urls = "stun:212.144.246.197:3478" },
                    //new() { urls = "stun:91.213.98.54:3478" },
                    //new() { urls = "stun:23.21.92.55:3478" },
                    //new() { urls = "stun:143.198.60.79:3478" },
                    //new() { urls = "stun:159.69.191.124:443" },
                    //new() { urls = "stun:185.88.236.76:3478" },
                    //new() { urls = "stun:66.228.54.23:3478" },
                    //new() { urls = "stun:136.243.59.79:3478" },
                    //new() { urls = "stun:195.145.93.141:3478" },
                    //new() { urls = "stun:34.195.177.19:3478" },
                    //new() { urls = "stun:159.69.191.124:3478" },
                    //new() { urls = "stun:49.12.125.53:3478" },
                    //new() { urls = "stun:213.251.48.147:3478" },
                    //new() { urls = "stun:91.212.41.85:3478" },
                    //new() { urls = "stun:172.233.245.118:3478" },
                    //new() { urls = "stun:188.40.18.246:3478" },
                    //new() { urls = "stun:35.177.202.92:3478" },
                    //new() { urls = "stun:88.99.67.241:3478" },
                    //new() { urls = "stun:80.155.54.123:3478" },
                    //new() { urls = "stun:147.182.188.245:3478" },
                    //new() { urls = "stun:193.22.17.97:3478" },
                    //new() { urls = "stun:176.9.24.184:3478" },
                    //new() { urls = "stun:137.74.112.113:3478" },
                    //new() { urls = "stun:85.197.87.182:3478" },
                    //new() { urls = "stun:81.3.27.44:3478" }

                    #endregion

                ]
            };
        }

        private void HandleIce(string signal)
        {
            var ice = JsonSerializer.Deserialize<RTCIceCandidateInit>(signal);

            var init = new RTCIceCandidateInit
            {
                candidate = ice.candidate,
                sdpMid = ice.sdpMid,
                sdpMLineIndex = ice.sdpMLineIndex
            };
            _peerConnection.addIceCandidate(init);
        }

        private async void HandleOffer(string signal)
        {
            var offer = JsonSerializer.Deserialize<RTCSessionDescriptionInit>(signal);

            var init = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offer.sdp
            };
            _peerConnection.setRemoteDescription(init);

            var answer = _peerConnection.createAnswer();
            await _peerConnection.setLocalDescription(answer);

            await _apiService.SendAnswer(TargetId, JsonSerializer.Serialize(answer));
        }

        private async void HandleAnswer(string signal)
        {
            var answer = JsonSerializer.Deserialize<RTCSessionDescriptionInit>(signal);

            var init = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = answer.sdp
            };
            _peerConnection.setRemoteDescription(init);

            await Task.CompletedTask;
        }


        public async Task InitializeAsync()
        {
            _apiService.SignalR_ReceiveIce += HandleIce;
            _apiService.SignalR_ReceiveOffer += HandleOffer;
            _apiService.SignalR_ReceiveAnswer += HandleAnswer;

            _peerConnection = new RTCPeerConnection(_configuration);

            _peerConnection.onicecandidate += async (candidate) =>
            {
                if (candidate.type == RTCIceCandidateType.host || candidate.type == RTCIceCandidateType.prflx) return;

                var ice = new RTCIceCandidateInit
                {
                    candidate = candidate.candidate,
                    sdpMid = candidate.sdpMid,
                    sdpMLineIndex = candidate.sdpMLineIndex
                };

                await _apiService.SendIce(TargetId, JsonSerializer.Serialize(ice));
            };

            _peerConnection.ondatachannel += channel =>
            {
                channel.onmessage += (dc, protocol, data) =>
                {
                    OnReceivedMessage?.Invoke(data);
                };
            };

            _peerConnection.onconnectionstatechange += (state) =>
            {
                OnConnectionStateChanged?.Invoke(state.ToString());

                if (state == RTCPeerConnectionState.disconnected || state == RTCPeerConnectionState.failed || state == RTCPeerConnectionState.closed)
                {
                    _peerConnection.Close("ice disconnection");
                }
                else if (state == RTCPeerConnectionState.connected)
                {
                }
            };

            var init = new RTCDataChannelInit
            {
                ordered = true,
                maxPacketLifeTime = 300,
                protocol = "DataProtocol",
                negotiated = false,
            };
            _dataChannel = await _peerConnection.createDataChannel("DataChannel", init);

            //var audioTrack = new MediaStreamTrack(new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU));
            var videoTrack = new MediaStreamTrack(new VideoFormat(VideoCodecsEnum.VP8, 96, 90000, $"paramter"));
            _peerConnection.addTrack(videoTrack);

            _peerConnection.OnVideoFormatsNegotiated += format =>
            {
                ;
            };
            //var _lastFrameAt = DateTime.MinValue;
            _videoRecorder.OnVideoFrameArrived += (byte[] sample) =>
            {
                //uint frameSpacing = 0;
                //if (_lastFrameAt != DateTime.MinValue)
                //{
                //    frameSpacing = Convert.ToUInt32(DateTime.Now.Subtract(_lastFrameAt).TotalMilliseconds);
                //}

                //var nv12 = Nv12Converter.ConvertJpegToNv12(sample, 320, 320);
                //_peerConnection.SendVideo(3000, nv12);

                //var compressed = CompressionHelper.Compress(sample, CompressionLevel.SmallestSize);
                //_peerConnection.SendVideo(3000, compressed);

                _peerConnection.SendVideo(3000, sample);

                OnLocalVideoFrameReceived?.Invoke(sample);

                //_lastFrameAt = DateTime.Now;
            };
            _peerConnection.OnVideoFrameReceived += (IPEndPoint remoteEndPoint, uint timestamp, byte[] frame, VideoFormat format) =>
            {
                try
                {
                    //var decompressed = CompressionHelper.Decompress(frame);
                    //OnRemoteVideoFrameReceived?.Invoke(decompressed);

                    //var jpeg = Nv12Converter.ConvertNv12ToJpeg(frame, 320, 320);
                    //OnRemoteVideoFrameReceived?.Invoke(jpeg);

                    OnRemoteVideoFrameReceived?.Invoke(frame);
                }
                catch (Exception)
                {
                }
            };

        }


        public async Task SendOffer()
        {
            var offer = _peerConnection.createOffer();
            await _peerConnection.setLocalDescription(offer);

            await _apiService.SendOffer(TargetId, JsonSerializer.Serialize(offer));
        }

        public void SendMessage(string text)
        {
            if (_dataChannel?.readyState == RTCDataChannelState.open)
            {
                _dataChannel.send(text);
            }
        }

        public async Task SendVideoFrame()
        {
            _videoRecorder.StartRecording();
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _apiService.SignalR_ReceiveIce -= HandleIce;
            _apiService.SignalR_ReceiveOffer -= HandleOffer;
            _apiService.SignalR_ReceiveAnswer -= HandleAnswer;

            if (_configuration != null)
            {
                //_configuration.Dispose();
                _configuration = null;
            }
            if (_peerConnection != null)
            {
                _peerConnection.close();
                _peerConnection.Dispose();
                _peerConnection = null;
            }
            if (_dataChannel != null)
            {
                _dataChannel.close();
                //_dataChannel.Dispose();
                _dataChannel = null;
            }
        }

    }
}
