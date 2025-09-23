using Microsoft.UI.Xaml.Data;
using System;

namespace BootCoupon
{
    public class DecimalFormatConverter : IValueConverter
    {
        public string Format { get; set; } = "N2";

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue.ToString(Format);
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}