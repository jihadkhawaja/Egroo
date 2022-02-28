using System;
using System.Globalization;
using Microsoft.Maui;

namespace MobileChat.Converters
{
    public class BoolToColorMessageText : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool)
                if ((bool)value)
                    return Application.Current.Resources["ReceiverTextColor"];

            return Application.Current.Resources["SenderTextColor"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}