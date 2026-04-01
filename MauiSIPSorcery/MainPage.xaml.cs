using MauiSIPSorcery.Models;
using MauiSIPSorcery.UtilityTools;
using Microsoft.AspNetCore.SignalR.Client;
using SIPSorcery.Net;
using SkiaSharp;
using System.Buffers.Text;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MauiSIPSorcery
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageViewModel mainPageViewModel)
        {
            InitializeComponent();
            BindingContext = mainPageViewModel;

            mainPageViewModel._localSKCanvas = localSKCanvas;
            mainPageViewModel._remoteSKCanvas = remoteSKCanvas;
        }

        /// <summary>
        /// 去除下划线
        /// </summary>
        //        protected override void OnHandlerChanged()
        //        {

        //            base.OnHandlerChanged();
        //#if ANDROID
        //                var edittext = txtUserName.Handler.PlatformView as Android.Widget.EditText;
        //                edittext.Background = null;
        //#endif

        //        }



        //private Point _dragStart;
        //private Rect _originalBounds;

        //private void OnAbsolutePanUpdated(object sender, PanUpdatedEventArgs e)
        //{
        //    switch (e.StatusType)
        //    {
        //        case GestureStatus.Started:
        //            // 获取当前位置
        //            var bounds = AbsoluteLayout.GetLayoutBounds(localSKCanvas);
        //            _originalBounds = bounds;
        //            _dragStart = new Point(bounds.X, bounds.Y);
        //            localSKCanvas.Opacity = 0.8;
        //            break;

        //        case GestureStatus.Running:
        //            // 计算新位置
        //            var newX = _dragStart.X + e.TotalX;
        //            var newY = _dragStart.Y + e.TotalY;

        //            // 更新位置，保持大小不变
        //            AbsoluteLayout.SetLayoutBounds(localSKCanvas, new Rect(newX, newY, _originalBounds.Width, _originalBounds.Height));
        //            break;

        //        case GestureStatus.Completed:
        //        case GestureStatus.Canceled:
        //            localSKCanvas.Opacity = 1.0;
        //            ConstrainAbsoluteBounds();
        //            break;
        //    }
        //}

        //// 限制在边界内（AbsoluteLayout 版本）
        //private void ConstrainAbsoluteBounds()
        //{
        //    var bounds = AbsoluteLayout.GetLayoutBounds(localSKCanvas);
        //    var parentWidth = 400;
        //    var parentHeight = 400;
        //    var controlWidth = bounds.Width;
        //    var controlHeight = bounds.Height;

        //    var minX = 0;
        //    var maxX = parentWidth - controlWidth;
        //    var minY = 0;
        //    var maxY = parentHeight - controlHeight;

        //    var newX = Math.Max(minX, Math.Min(maxX, bounds.X));
        //    var newY = Math.Max(minY, Math.Min(maxY, bounds.Y));

        //    AbsoluteLayout.SetLayoutBounds(localSKCanvas, new Rect(newX, newY, bounds.Width, bounds.Height));
        //}




        double _startX;
        double _startY;

        protected override void OnAppearing()
        {
            base.OnAppearing();

            var pan = new PanGestureRecognizer();
            pan.PanUpdated += OnLocalCanvasPanUpdated;
            localSKCanvas.GestureRecognizers.Add(pan);
        }

        private void OnLocalCanvasPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            if (sender is not View local)
                return;

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _startX = local.TranslationX;
                    _startY = local.TranslationY;
                    break;

                case GestureStatus.Running:
                    MoveLocalCanvas(local, e.TotalX, e.TotalY);
                    break;
            }
        }

        private void MoveLocalCanvas(View local, double dx, double dy)
        {
            // remoteCanvas 在 RootGrid 内的尺寸
            double maxX = remoteSKCanvas.Width - local.Width;
            double maxY = remoteSKCanvas.Height - local.Height;

            // 新位置
            double newX = _startX + dx;
            double newY = _startY + dy;

            // 边界裁剪
            newX = Math.Max(0, Math.Min(newX, maxX));
            newY = Math.Max(0, Math.Min(newY, maxY));

            local.TranslationX = newX;
            local.TranslationY = newY;
        }



    }
}
