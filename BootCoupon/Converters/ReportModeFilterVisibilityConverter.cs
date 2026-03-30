using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace BootCoupon.Converters
{
    /// <summary>
    /// Converter สำหรับแสดง/ซ่อนตัวกรองตาม ReportMode ที่เลือก
    /// </summary>
    public class ReportModeFilterVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null) 
                return Visibility.Collapsed;

            if (!(value is SalesReportViewModel.ReportModes mode))
                return Visibility.Collapsed;

            var filterName = parameter?.ToString() ?? string.Empty;

            // กำหนดการแสดงผลของตัวกรองตาม ReportMode
            switch (filterName)
            {
                case "StartDate":
                case "EndDate":
                    // ✅ แก้ไข: แสดงตัวกรองวันที่สำหรับทุก mode (รวม RemainingCoupons)
                    return Visibility.Visible;

                case "SalesPerson":
                    // ซ่อนสำหรับ RemainingCoupons และ SummaryByCoupon
                    return mode != SalesReportViewModel.ReportModes.RemainingCoupons 
                        && mode != SalesReportViewModel.ReportModes.SummaryByCoupon
                        ? Visibility.Visible 
                        : Visibility.Collapsed;

                case "Branch":
                case "Coupon":
                    // แสดงทุก mode
                    return Visibility.Visible;

                case "PaymentMethod":
                    // แสดงเฉพาะ ByReceipt และ CancelledReceipts
                    return mode == SalesReportViewModel.ReportModes.ByReceipt 
                        || mode == SalesReportViewModel.ReportModes.CancelledReceipts
                        ? Visibility.Visible 
                        : Visibility.Collapsed;

                default:
                    return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
