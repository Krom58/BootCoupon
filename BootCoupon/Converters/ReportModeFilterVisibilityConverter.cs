using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace BootCoupon.Converters
{
    /// <summary>
    /// Converter ????????????????????????????????????? ReportMode ???????
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

// ??????????????????????????????? ReportMode ???????
            switch (filterName)
     {
             case "StartDate":
      case "EndDate":
    // ????????????? ?????? RemainingCoupons
 return mode != SalesReportViewModel.ReportModes.RemainingCoupons 
   ? Visibility.Visible 
     : Visibility.Collapsed;

   case "SalesPerson":
    // ????????????? ?????? RemainingCoupons ??? SummaryByCoupon
        return mode != SalesReportViewModel.ReportModes.RemainingCoupons 
  && mode != SalesReportViewModel.ReportModes.SummaryByCoupon
     ? Visibility.Visible 
      : Visibility.Collapsed;

           case "CouponType":
        case "Coupon":
          // ?????????????
      return Visibility.Visible;

          case "PaymentMethod":
  // ????????? ByReceipt ??? CancelledReceipts
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
