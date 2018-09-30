using System;
using System.Globalization;
using System.Runtime.Caching;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TaskSwitcher
{
    public class WindowHandleToCachedIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string key = "IconImage-" + value + "-longCache";
            return MemoryCache.Default.Get(key) as BitmapImage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}