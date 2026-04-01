using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiSIPSorcery.UtilityTools
{
    public class Nv12Converter
    {
        public static byte[] ConvertJpegToNv12(byte[] jpegBytes, int width, int height)
        {
            // 1. 使用 SkiaSharp 解码 JPEG 字节数组
            using (var skBitmap = SKBitmap.Decode(jpegBytes))
            {
                if (skBitmap == null)
                {
                    throw new ArgumentException("Failed to decode JPEG image.");
                }

                // 2. 可选：调整图像尺寸以确保与给定的 width/height 一致（如果解码后的尺寸与输入参数不符）
                if (skBitmap.Width != width || skBitmap.Height != height)
                {
                    using (var resizedBitmap = new SKBitmap(width, height, skBitmap.ColorType, skBitmap.AlphaType))
                    using (var canvas = new SKCanvas(resizedBitmap))
                    {
                        canvas.DrawBitmap(skBitmap, new SKRect(0, 0, width, height));
                        canvas.Flush();
                        return ConvertToNv12(resizedBitmap, width, height);
                    }
                }
                else
                {
                    return ConvertToNv12(skBitmap, width, height);
                }
            }
        }

        private static byte[] ConvertToNv12(SKBitmap bitmap, int width, int height)
        {
            // 3. 将图像数据转换为 NV12 格式所需的 YUV 数据
            // NV12 缓冲区大小：Y 分量占 width * height，UV 分量占 width * height / 2 (因为 4:2:0 下采样)
            int nv12Size = width * height * 3 / 2;
            byte[] nv12Buffer = new byte[nv12Size];

            // 4. 获取图像的像素数据（SkiaSharp 默认可能是 RGBA 或 BGRA 等格式）
            //    我们需要将 RGB 转换为 YUV，并排列成 NV12 格式
            //    由于 SkiaSharp 没有直接提供 RGB 到 YUV420SP 的转换，我们需要手动实现或使用其他方法

            // 此处使用一个常见的方法：先转换为 I420 (YUV420P)，然后再将 U 和 V 平面交错成 NV12 的 UV 平面
            byte[] yPlane = new byte[width * height];
            byte[] uPlane = new byte[width * height / 4];
            byte[] vPlane = new byte[width * height / 4];

            // 5. 遍历每个像素，进行 RGB 到 YUV 的转换并填充到 I420 的平面中
            int yIndex = 0;
            int uIndex = 0;
            int vIndex = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    SKColor color = bitmap.GetPixel(x, y);
                    byte r = color.Red;
                    byte g = color.Green;
                    byte b = color.Blue;

                    // 计算 YUV 值（使用标准转换公式）
                    byte yValue = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
                    byte uValue = (byte)(-0.169 * r - 0.331 * g + 0.5 * b + 128);
                    byte vValue = (byte)(0.5 * r - 0.419 * g - 0.081 * b + 128);

                    yPlane[yIndex++] = yValue;

                    // 在 4:2:0 下采样中，每 2x2 的像素块共享一组 UV 值
                    if (y % 2 == 0 && x % 2 == 0)
                    {
                        uPlane[uIndex++] = uValue;
                        vPlane[vIndex++] = vValue;
                    }
                }
            }

            // 6. 将 I420 (YUV420P) 数据转换为 NV12 格式
            //    NV12 布局：先是完整的 Y 平面，然后是交错的 UV 平面（U1, V1, U2, V2, ...）
            Buffer.BlockCopy(yPlane, 0, nv12Buffer, 0, yPlane.Length);

            int uvOffset = yPlane.Length;
            for (int i = 0; i < uPlane.Length; i++)
            {
                nv12Buffer[uvOffset + 2 * i] = uPlane[i];     // U 分量放在偶数索引
                nv12Buffer[uvOffset + 2 * i + 1] = vPlane[i]; // V 分量放在奇数索引
            }

            return nv12Buffer;
        }




        public static byte[] ConvertNv12ToJpeg(byte[] nv12Data, int width, int height, int quality = 75)
        {
            // 1. 将 NV12 数据转换为 RGB 格式
            byte[] rgbData = ConvertNv12ToRgb(nv12Data, width, height);

            // 2. 创建 SkiaSharp 位图并设置 RGB 数据
            using (var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888)))
            {
                // 将 RGB 数据复制到位图中
                var pixels = bitmap.Pixels;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width + x) * 3;
                        byte r = rgbData[index];
                        byte g = rgbData[index + 1];
                        byte b = rgbData[index + 2];

                        pixels[y * width + x] = new SKColor(r, g, b);
                    }
                }
                bitmap.Pixels = pixels;

                // 3. 编码为 JPEG
                using (var image = SKImage.FromBitmap(bitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Jpeg, quality))
                {
                    return data.ToArray();
                }
            }

        }

        private static byte[] ConvertNv12ToRgb(byte[] nv12Data, int width, int height)
        {
            int ySize = width * height;
            byte[] rgbData = new byte[width * height * 3];

            // YUV 到 RGB 的转换矩阵 (BT.601)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 获取 Y 分量
                    int yIndex = y * width + x;
                    byte Y = nv12Data[yIndex];

                    // 获取 UV 分量 (NV12 格式中 UV 是交错的)
                    int uvIndex = ySize + (y / 2) * (width) + (x / 2) * 2;
                    byte U = nv12Data[uvIndex];
                    byte V = nv12Data[uvIndex + 1];

                    // YUV 到 RGB 转换公式
                    int r = (int)(Y + 1.402 * (V - 128));
                    int g = (int)(Y - 0.344 * (U - 128) - 0.714 * (V - 128));
                    int b = (int)(Y + 1.772 * (U - 128));

                    // 钳位到 0-255 范围
                    r = Math.Max(0, Math.Min(255, r));
                    g = Math.Max(0, Math.Min(255, g));
                    b = Math.Max(0, Math.Min(255, b));

                    // 存储 RGB 值
                    int rgbIndex = (y * width + x) * 3;
                    rgbData[rgbIndex] = (byte)r;
                    rgbData[rgbIndex + 1] = (byte)g;
                    rgbData[rgbIndex + 2] = (byte)b;
                }
            }

            return rgbData;
        }

    }
}
