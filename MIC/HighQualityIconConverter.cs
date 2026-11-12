using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace MIC
{
    public static class HighQualityIconConverter
    {
        // Standard icon sizes needed for sharp scaling across all Windows DPI settings
        private static readonly int[] IconSizes = { 16, 24, 32, 48, 64, 128, 256 };

        public static void ConvertPngToMultiResIco(string pngPath, string icoPath)
        {
            using (var sourceBitmap = new Bitmap(pngPath))
            {
                using (var fs = new FileStream(icoPath, FileMode.Create))
                {
                    using (var writer = new BinaryWriter(fs))
                    {
                        int imageCount = IconSizes.Length;

                        // 1. Write ICO Header (6 bytes)
                        writer.Write((short.Parse("0"))); // Reserved (Must be 0)
                        writer.Write((short)1);           // Image Type (1 = Icon)
                        writer.Write((short)imageCount);  // Number of images

                        int currentOffset = 6 + (16 * imageCount);
                        var pngDataList = new byte[imageCount][];

                        // 2. Generate and resize each icon layer with High Quality settings
                        for (int i = 0; i < imageCount; i++)
                        {
                            int size = IconSizes[i];
                            using (var resizedBmp = ResizeImage(sourceBitmap, size, size))
                            {
                                using (var ms = new MemoryStream())
                                {
                                    // Save each layer as PNG format (supported inside modern .ico files)
                                    resizedBmp.Save(ms, ImageFormat.Png);
                                    pngDataList[i] = ms.ToArray();
                                }
                            }
                        }

                        // 3. Write Directory Entries (16 bytes per image)
                        for (int i = 0; i < imageCount; i++)
                        {
                            int size = IconSizes[i];
                            byte[] pngData = pngDataList[i];

                            // Width and Height (if size is 256, byte value is 0)
                            writer.Write((byte)(size == 256 ? 0 : size));
                            writer.Write((byte)(size == 256 ? 0 : size));
                            writer.Write((byte)0); // Color palette (0 for true color)
                            writer.Write((byte)0); // Reserved
                            writer.Write((short)1);  // Color planes
                            writer.Write((short)32); // Bits per pixel
                            writer.Write((int)pngData.Length); // Size of PNG data
                            writer.Write((int)currentOffset);  // Offset from start of file

                            currentOffset += pngData.Length;
                        }

                        // 4. Write the actual image payloads
                        for (int i = 0; i < imageCount; i++)
                        {
                            writer.Write(pngDataList[i]);
                        }
                    }
                }
            }
        }

        private static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                // Apply professional high-quality interpolation settings to eliminate jagged edges
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }
}
