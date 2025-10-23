using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using BootCoupon;

namespace BootCoupon.Converters
{
    public class ReportModeToVisibilityConverter : IValueConverter
    {
        // parameter should be one of "ByReceipt", "UnlimitedGrouped", "LimitedCoupons"
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;

            if (!(value is SalesReportViewModel.ReportModes mode))
                return Visibility.Collapsed;

            var param = parameter?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(param)) return Visibility.Collapsed;
            // support comma-separated list of modes
            var parts = param.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var trimmed = p.Trim();
                if (Enum.TryParse<SalesReportViewModel.ReportModes>(trimmed, out var targetMode))
                {
                    if (mode == targetMode) return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
