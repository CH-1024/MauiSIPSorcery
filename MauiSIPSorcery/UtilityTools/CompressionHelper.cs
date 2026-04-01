using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;
using System.IO;
using Microsoft.Maui.ApplicationModel;

namespace MauiSIPSorcery.UtilityTools
{
    public class CompressionHelper
    {
        // 压缩 byte[]
        public static byte[] Compress(byte[] data, CompressionLevel compressionLevel = CompressionLevel.Fastest)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, compressionLevel))
                {
                    gzipStream.Write(data, 0, data.Length);
                }
                return outputStream.ToArray();
            }
        }


        // 解压缩 byte[]
        public static byte[] Decompress(byte[] compressedData)
        {
            using (var inputStream = new MemoryStream(compressedData))
            using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                gzipStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
        }


        //public static byte[] ChangeImageQuality(byte[] imageData, int quality)
        //{
        //    using (var inputStream = new SKMemoryStream(imageData))
        //    using (var original = SKBitmap.Decode(inputStream))
        //    using (var image = SKImage.FromBitmap(original))
        //    using (var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality))
        //    {
        //        return encodedData.ToArray();
        //    }
        //}
        //public static byte[] ChangeImageQuality(Stream imageStream, int quality)
        //{
        //    using (var original = SKBitmap.Decode(imageStream))
        //    using (var image = SKImage.FromBitmap(original))
        //    using (var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality))
        //    {
        //        return encodedData.ToArray();
        //    }
        //}


        public static byte[] ChangeImageQuality(byte[] imageData, int quality)
        {
            using (var imageStream = new SKMemoryStream(imageData))
            using (var codec = SKCodec.Create(imageStream))
            {
                var origin = codec.EncodedOrigin;
                using (var original = SKBitmap.Decode(codec))
                {
                    var (matrix, newSize) = GetOrientationTransform(origin, original.Width, original.Height);

                    using (var rotated = new SKBitmap(newSize.Width, newSize.Height))
                    using (var canvas = new SKCanvas(rotated))
                    {
                        // 应用变换矩阵
                        canvas.SetMatrix(matrix);
                        canvas.DrawBitmap(original, 0, 0);
                        canvas.ResetMatrix();

                        #region 添加 video icon
                        //float circleRadius = Math.Min(rotated.Width, rotated.Height) * 0.2f; // 按图片尺寸比例设置
                        //float cx = rotated.Width / 2f;    // 水平居中
                        //float cy = rotated.Height / 2f;  // 垂直居中

                        //// 绘制背景圆圈
                        //using (var circlePaint = new SKPaint
                        //{
                        //    Style = SKPaintStyle.Fill,
                        //    Color = SKColors.White.WithAlpha(0xCC), // 80%透明度
                        //    IsAntialias = true
                        //})
                        //{
                        //    canvas.DrawCircle(cx, cy, circleRadius, circlePaint);
                        //}

                        //// 绘制三角形
                        //using (var trianglePaint = new SKPaint
                        //{
                        //    Style = SKPaintStyle.Fill,
                        //    Color = SKColors.Black,
                        //    IsAntialias = true
                        //})
                        //using (var path = new SKPath())
                        //{
                        //    // 三角形参数（居中显示）
                        //    float triangleSize = circleRadius * 0.6f;

                        //    path.MoveTo(cx - triangleSize * 0.8f, cy - triangleSize); // 左顶点
                        //    path.LineTo(cx + triangleSize, cy);                       // 右顶点
                        //    path.LineTo(cx - triangleSize * 0.8f, cy + triangleSize); // 下顶点
                        //    path.Close();
                        //    canvas.DrawPath(path, trianglePaint);
                        //}
                        #endregion

                        // 编码并清除方向元数据
                        using (var image = SKImage.FromBitmap(rotated))
                        using (var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality))
                        {
                            return encodedData.ToArray();
                        }
                    }
                }
            }
        }


        public static byte[] ChangeImageQuality(Stream imageStream, int quality)
        {
            if (imageStream.CanSeek) imageStream.Position = 0;

            using (var codec = SKCodec.Create(imageStream))
            {
                var origin = codec.EncodedOrigin;
                using (var original = SKBitmap.Decode(codec))
                {
                    var (matrix, newSize) = GetOrientationTransform(origin, original.Width, original.Height);

                    using (var rotated = new SKBitmap(newSize.Width, newSize.Height))
                    using (var canvas = new SKCanvas(rotated))
                    {
                        // 应用变换矩阵
                        canvas.SetMatrix(matrix);
                        canvas.DrawBitmap(original, 0, 0);
                        canvas.ResetMatrix();

                        // 编码并清除方向元数据
                        using (var image = SKImage.FromBitmap(rotated))
                        using (var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality))
                        {
                            return encodedData.ToArray();
                        }
                    }
                }
            }
        }


        private static (SKMatrix matrix, SKSizeI size) GetOrientationTransform(SKEncodedOrigin origin, int width, int height)
        {
            SKMatrix matrix = SKMatrix.CreateIdentity();
            SKSizeI size = new SKSizeI(width, height);

            #region MyRegion
            //// 根据方向类型设置矩阵变换
            //switch (origin)
            //{
            //    case SKEncodedOrigin.TopRight:
            //        matrix = SKMatrix.CreateScale(-1, 1); // 水平镜像
            //        matrix.TransX = width;
            //        break;

            //    case SKEncodedOrigin.BottomRight:
            //        matrix = SKMatrix.CreateRotationDegrees(180);
            //        matrix.TransX = width;
            //        matrix.TransY = height;
            //        break;

            //    case SKEncodedOrigin.BottomLeft:
            //        matrix = SKMatrix.CreateRotationDegrees(180);
            //        break;

            //    case SKEncodedOrigin.LeftTop:
            //        matrix = SKMatrix.CreateRotationDegrees(-90);
            //        //matrix.TransY = original.Height;
            //        matrix.TransX = height;
            //        break;

            //    case SKEncodedOrigin.RightTop:
            //        matrix = SKMatrix.CreateRotationDegrees(90);
            //        matrix.TransX = height;
            //        //matrix.TransY = original.Width;
            //        break;

            //    case SKEncodedOrigin.RightBottom:
            //        matrix = SKMatrix.CreateRotationDegrees(90);
            //        //matrix.TransX = original.Width;
            //        //matrix.TransY = original.Height;
            //        matrix.ScaleX = -1;
            //        matrix.ScaleY = 1;
            //        matrix.TransX = height;
            //        matrix.TransY = width;
            //        break;

            //    case SKEncodedOrigin.LeftBottom:
            //        matrix = SKMatrix.CreateRotationDegrees(-90);
            //        matrix.TransY = width;
            //        break;

            //    default: // TopLeft 不处理
            //        break;
            //
            #endregion

            switch (origin)
            {
                case SKEncodedOrigin.TopLeft:
                    break;

                case SKEncodedOrigin.TopRight:
                    matrix = SKMatrix.CreateScale(-1, 1); // 水平镜像
                    matrix = SKMatrix.CreateTranslation(width, 0);
                    break;

                case SKEncodedOrigin.BottomRight:
                    matrix = SKMatrix.CreateRotationDegrees(180);
                    matrix = SKMatrix.CreateTranslation(width, height);
                    break;

                case SKEncodedOrigin.BottomLeft:
                    matrix = SKMatrix.CreateRotationDegrees(180);
                    break;

                case SKEncodedOrigin.LeftTop:
                    matrix = SKMatrix.CreateRotationDegrees(-90);
                    matrix = SKMatrix.CreateTranslation(height, 0);
                    size = new SKSizeI(height, width);
                    break;

                case SKEncodedOrigin.RightTop:
                    matrix = SKMatrix.CreateRotationDegrees(90);
                    matrix = SKMatrix.CreateTranslation(height, 0);
                    size = new SKSizeI(height, width);
                    break;

                case SKEncodedOrigin.RightBottom:
                    matrix = SKMatrix.CreateRotationDegrees(90);
                    matrix = SKMatrix.CreateScale(-1, 1); // 水平镜像
                    matrix = SKMatrix.CreateTranslation(height, width);
                    size = new SKSizeI(height, width);
                    break;

                case SKEncodedOrigin.LeftBottom:
                    matrix = SKMatrix.CreateRotationDegrees(-90);
                    matrix.TransY = width;
                    //matrix.ScaleX = -1;
                    //matrix.ScaleY = 1;
                    //matrix = SKMatrix.CreateScale(-1, 1); // 水平镜像
                    //matrix = SKMatrix.CreateTranslation(0, width);
                    size = new SKSizeI(height, width);
                    break;
            }

            return (matrix, size);
        }


        public static byte[] ChangeImageQualityWithResize(byte[] imageData, int maxWidth, int maxHeight, int quality)
        {
            using (var inputStream = new SKMemoryStream(imageData))
            using (var original = SKBitmap.Decode(inputStream))
            {
                // 计算新尺寸
                float ratio = Math.Min((float)maxWidth / original.Width, (float)maxHeight / original.Height);
                int newWidth = (int)(original.Width * ratio);
                int newHeight = (int)(original.Height * ratio);

                // 调整尺寸
                using (var resized = original.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)))
                using (var image = SKImage.FromBitmap(resized))
                using (var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality))
                {
                    return encodedData.ToArray();
                }
            }
        }


        public static byte[] ChangeImageQualityWithResize(Stream imageStream, int maxWidth, int maxHeight, int quality)
        {
            using (var original = SKBitmap.Decode(imageStream))
            {
                // 计算新尺寸
                float ratio = Math.Min((float)maxWidth / original.Width, (float)maxHeight / original.Height);
                int newWidth = (int)(original.Width * ratio);
                int newHeight = (int)(original.Height * ratio);

                // 调整尺寸
                using (var resized = original.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)))
                using (var image = SKImage.FromBitmap(resized))
                using (var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, quality))
                {
                    return encodedData.ToArray();
                }
            }
        }


    }
}
