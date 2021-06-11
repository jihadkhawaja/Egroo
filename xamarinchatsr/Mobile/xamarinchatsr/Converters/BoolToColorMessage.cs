using System;
using System.Globalization;
using Xamarin.Forms;

namespace xamarinchatsr.Converters
{
    public class BoolToColorMessage : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool)
                if ((bool)value)
                    return Application.Current.Resources["ReceiverColor"];

            return Application.Current.Resources["SenderColor"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}