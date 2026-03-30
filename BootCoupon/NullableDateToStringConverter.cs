using Microsoft.UI.Xaml.Data;
using System;

namespace BootCoupon
{
 public class NullableDateToStringConverter : IValueConverter
 {
 public object Convert(object value, Type targetType, object parameter, string language)
 {
 if (value is DateTime dt && dt != DateTime.MinValue)
 {
 return dt.ToString("dd/MM/yyyy");
 }

 // If value is null or not a DateTime, return empty
 return string.Empty;
 }

 public object ConvertBack(object value, Type targetType, object parameter, string language)
 {
 throw new NotImplementedException();
 }
 }
}
