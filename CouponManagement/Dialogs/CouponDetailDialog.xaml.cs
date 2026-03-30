using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using CouponManagement.Shared.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CouponManagement.Dialogs;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class CouponDetailDialog : ContentDialog
{
    private readonly GeneratedCoupon _coupon;

    public CouponDetailDialog(GeneratedCoupon coupon)
    {
        this.InitializeComponent();
        _coupon = coupon;
        LoadCouponData();
    }

    private void LoadCouponData()
    {
        if (_coupon?.CouponDefinition != null)
        {
            CouponCodeTextBlock.Text = _coupon.GeneratedCode;
            DefinitionCodeTextBlock.Text = _coupon.CouponDefinition.Code;
            DefinitionNameTextBlock.Text = _coupon.CouponDefinition.Name;
            BatchNumberTextBlock.Text = _coupon.BatchNumber.ToString();
            StatusTextBlock.Text = _coupon.IsUsed ? "ใช้แล้ว" : "ยังไม่ใช้";
            CreatedAtTextBlock.Text = _coupon.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss");

            if (_coupon.IsUsed)
            {
                UsedByTextBlock.Text = _coupon.UsedBy ?? "ไม่ระบุ";
                UsedDateTextBlock.Text = _coupon.UsedDate?.ToString("dd/MM/yyyy HH:mm:ss") ?? "ไม่ระบุ";
                UsageInfoPanel.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else
            {
                UsageInfoPanel.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
        }
    }
}
