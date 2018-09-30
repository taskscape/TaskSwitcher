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

            using (MemoryStream memory = new MemoryStream())
            {
                Bitmap bitmap = icon.ToBitmap();
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
    }
}