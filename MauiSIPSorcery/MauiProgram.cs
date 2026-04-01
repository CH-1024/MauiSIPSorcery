using CommunityToolkit.Maui;
using MauiSIPSorcery.Interfaces;
using MauiSIPSorcery.UtilityTools;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Platform;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MauiSIPSorcery
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitCamera()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });


#if WINDOWS
            builder.Services.AddSingleton<IVideoRecorder, Platforms.Windows.VideoRecorderService>();
#elif ANDROID
            builder.Services.AddSingleton<IVideoRecorder, Platforms.Android.VideoRecorderService>();
#elif IOS
            builder.Services.AddSingleton<IVideoRecorder, Platforms.iOS.VideoRecorderService>();
#endif


            //            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(nameof(Entry), (handler, view) =>
            //            {
            ////#if ANDROID
            ////                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Colors.Transparent.ToPlatform());
            ////#endif


            ////#if WINDOWS
            ////                handler.PlatformView.Background = Colors.Transparent.ToPlatform();
            ////                handler.PlatformView.BorderThickness = new Thickness(0).ToPlatform();
            ////                handler.PlatformView.GotFocus += (s, e) =>
            ////                {
            ////                    handler.PlatformView.Background = Colors.Transparent.ToPlatform();
            ////                    handler.PlatformView.BorderThickness = new Thickness(0).ToPlatform();
            ////                    handler.PlatformView.BorderBrush = Colors.Transparent.ToPlatform();
            ////                };
            ////#endif
            //            });



            // Fix: Register MainPage and MainPageViewModel separately
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<MainPageViewModel>();

            builder.Services.AddSingleton<ApiService>();

            return builder.Build();
        }
    }
}
