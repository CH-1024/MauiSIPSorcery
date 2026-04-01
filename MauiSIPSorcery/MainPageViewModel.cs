using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiSIPSorcery.Interfaces;
using MauiSIPSorcery.Models;
using MauiSIPSorcery.UtilityTools;
using SIPSorceryMedia.Abstractions;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MauiSIPSorcery
{
    public partial class MainPageViewModel : ObservableObject
    {
        ApiService _apiService;
        WebRTCManager _webRTC;

        // 类级别变量
        //private SKBitmap? _currentFrame;
        //private readonly object _frameLock = new object();

        public SKCanvasView _localSKCanvas;
        private SKBitmap? _localFrame;
        private readonly object _localLock = new object();

        public SKCanvasView _remoteSKCanvas;
        private SKBitmap? _remoteFrame;
        private readonly object _remoteLock = new object();


        readonly IVideoRecorder _videoRecorder;

        [ObservableProperty]
        string _state;

        [ObservableProperty]
        string _username;

        [ObservableProperty]
        string _password;

        [ObservableProperty]
        UserModel _user;

        [ObservableProperty]
        string _message;

        [ObservableProperty]
        string _hash;

        [ObservableProperty]
        int _length;

        [ObservableProperty]
        List<ContactModel> _friendList;

        [ObservableProperty]
        ContactModel _selectedFriend;


        public MainPageViewModel(ApiService apiService, IVideoRecorder videoRecorder)
        {
            _apiService = apiService;
            _videoRecorder = videoRecorder;
        }

        private void Local_PaintSurface(object? sender, SKPaintSurfaceEventArgs args)
        {
            var surface = args.Surface;
            var canvas = surface.Canvas;

            // 清除画布
            canvas.Clear(SKColors.Gray);

            // 检查是否有有效帧
            if (_localFrame == null) return;

            // 锁定帧数据避免更新冲突
            lock (_localLock)
            {
                if (_localFrame == null) return;

                // 计算缩放比例以保持宽高比
                float scale = Math.Min((float)args.Info.Width / _localFrame.Width, (float)args.Info.Height / _localFrame.Height);

                // 计算目标矩形（居中显示）
                float scaledWidth = _localFrame.Width * scale;
                float scaledHeight = _localFrame.Height * scale;
                float x = (args.Info.Width - scaledWidth) / 2;
                float y = (args.Info.Height - scaledHeight) / 2;
                var destRect = new SKRect(x, y, x + scaledWidth, y + scaledHeight);

                // 绘制帧
                canvas.DrawBitmap(_localFrame, destRect);
            }
        }
        private void Remote_PaintSurface(object? sender, SKPaintSurfaceEventArgs args)
        {
            var surface = args.Surface;
            var canvas = surface.Canvas;

            // 清除画布
            canvas.Clear(SKColors.DarkGray);

            // 检查是否有有效帧
            if (_remoteFrame == null) return;

            // 锁定帧数据避免更新冲突
            lock (_remoteLock)
            {
                if (_remoteFrame == null) return;

                // 计算缩放比例以保持宽高比
                float scale = Math.Min((float)args.Info.Width / _remoteFrame.Width, (float)args.Info.Height / _remoteFrame.Height);

                // 计算目标矩形（居中显示）
                float scaledWidth = _remoteFrame.Width * scale;
                float scaledHeight = _remoteFrame.Height * scale;
                float x = (args.Info.Width - scaledWidth) / 2;
                float y = (args.Info.Height - scaledHeight) / 2;
                var destRect = new SKRect(x, y, x + scaledWidth, y + scaledHeight);

                // 绘制帧
                canvas.DrawBitmap(_remoteFrame, destRect);
            }
        }

        [RelayCommand]
        void Appearing()
        {
            _localSKCanvas.PaintSurface += Local_PaintSurface;
            _remoteSKCanvas.PaintSurface += Remote_PaintSurface;

            _localSKCanvas.InvalidateSurface();
            _remoteSKCanvas.InvalidateSurface();
        }

        [RelayCommand]
        async Task Login()
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                await Shell.Current.DisplayAlert("Error", "用户名或密码不能为空", "OK");
                return;
            }

            var response = await _apiService.LoginAsync(Username, Password);
            if (response.IsSucc)
            {
                User = response.GetValue<UserModel>("User")!;

                var resp = await _apiService.LoadContactListAsync();
                if (resp.IsSucc)
                {
                    FriendList = resp.GetValue<List<ContactModel>>("ContactList");
                    SelectedFriend = FriendList.FirstOrDefault();
                }
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", response.Msg, "OK");
            }
        }

        [RelayCommand]
        async Task InitRTC()
        {
            if (SelectedFriend == null)
            {
                await Shell.Current.DisplayAlert("提示", "请选择一个联系人进行通话！", "确定", "取消");
                return;
            }

            var targetId = SelectedFriend.ContactUserId;

            if (_webRTC != null)
            {
                _webRTC.OnConnectionStateChanged -= HandleConnectionStateChanged;
                _webRTC.OnReceivedMessage -= HandleMessage;
                _webRTC.OnRemoteVideoFrameReceived -= HandleRemoteVideoFrame;
                _webRTC.OnLocalVideoFrameReceived -= HandleLocalVideoFrame;
                _webRTC.Dispose();
                _webRTC = null;
            }

            _webRTC = new WebRTCManager(targetId);

            _webRTC.OnConnectionStateChanged += HandleConnectionStateChanged;
            _webRTC.OnReceivedMessage += HandleMessage;
            _webRTC.OnRemoteVideoFrameReceived += HandleRemoteVideoFrame;
            _webRTC.OnLocalVideoFrameReceived += HandleLocalVideoFrame;

            await _webRTC.InitializeAsync();
        }

        [RelayCommand]
        async Task SendOffer()
        {
            if (_webRTC != null)
            {
                await _webRTC.SendOffer();
            }
        }

        [RelayCommand]
        async Task SendMessage()
        {
            var message = await Shell.Current.DisplayPromptAsync("Message", "请输入消息内容", "OK", "Cancel", null, -1, Keyboard.Text, null);

            if (string.IsNullOrEmpty(message)) return;

            if (_webRTC != null)
            {
                _webRTC.SendMessage(message);
                await Task.CompletedTask;
            }

        }

        [RelayCommand]
        async Task SendVideo()
        {
            if (!await Request_Camera_Async())
            {
                return;
            }

            if (_webRTC != null)
            {
                await _webRTC.SendVideoFrame();
            }
        }

        [RelayCommand]
        void Close()
        {
            if (_webRTC != null)
            {
                _webRTC.OnConnectionStateChanged -= HandleConnectionStateChanged;
                _webRTC.OnReceivedMessage -= HandleMessage;
                _webRTC.OnRemoteVideoFrameReceived -= HandleRemoteVideoFrame;
                _webRTC.OnLocalVideoFrameReceived -= HandleLocalVideoFrame;

                _webRTC.Dispose();
                _webRTC = null;
            }
        }





        private void HandleMessage(byte[] obj)
        {
            try
            {
                Message = Encoding.UTF8.GetString(obj);
            }
            catch (Exception)
            {
                Message = "无法解析消息内容";
            }
        }
        private void HandleRemoteVideoFrame(byte[] sample)
        {
            Hash = sample.GetHashCode().ToString();
            Length = sample.Length;

            // 锁定并更新位图数据
            lock (_remoteLock)
            {
                using var imageStream = new SKMemoryStream(sample);

                _remoteFrame = SKBitmap.Decode(imageStream);
                _remoteSKCanvas?.InvalidateSurface();

                // 请求重绘（确保在主线程调用）
                //MainThread.BeginInvokeOnMainThread(() => SKCanvas?.InvalidateSurface());
            }
        }
        private void HandleLocalVideoFrame(byte[] sample)
        {
            Hash = sample.GetHashCode().ToString();
            Length = sample.Length;

            // 锁定并更新位图数据
            lock (_localLock)
            {
                using var imageStream = new SKMemoryStream(sample);

                _localFrame = SKBitmap.Decode(imageStream);
                _localSKCanvas?.InvalidateSurface();

                // 请求重绘（确保在主线程调用）
                //MainThread.BeginInvokeOnMainThread(() => SKCanvas?.InvalidateSurface());
            }
        }
        private void HandleConnectionStateChanged(string state)
        {
            State = state;
        }





        async Task<bool> Request_Camera_Async()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();

                _ = Shell.Current.DisplayAlert("提示", $"请允许访问相机！", "退出");

                return false;
            }
            return true;
        }





    }
}