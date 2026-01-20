using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace TaskSwitcher
{
    public class IconToBitmapImageConverter
    {
        public BitmapImage Convert(Icon icon)
        {
            if (icon == null)
            {
                return null;
            }

            try
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    using Bitmap bitmap = icon.ToBitmap();
                    bitmap.Save(memory, ImageFormat.Png);
                    memory.Position = 0;
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                // Log the exception when logging is enabled, then return a blank BitmapImage
                DiagnosticLogger.LogException("IconToBitmapImageConverter.Convert", ex);
                return CreateEmptyBitmapImage(16, 16);
            }
        }

        private BitmapImage CreateEmptyBitmapImage(int width, int height)
        {
            using (Bitmap emptyBitmap = new Bitmap(width, height))
            using (MemoryStream memory = new MemoryStream())
            {
                emptyBitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }
    }
}