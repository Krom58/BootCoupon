using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using Microsoft.Data.SqlClient; // added for direct DB access
using System.Data;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CouponManagement
{
 public sealed partial class MainPage : Page
 {
 public MainPage()
 {
 this.InitializeComponent();
 }

 private void CouponDefinitionButton_Click(object sender, RoutedEventArgs e)
 {
 // Navigate to CouponDefinition page
 Frame.Navigate(typeof(CouponDefinitionPage));
 }

 private void GeneratedCouponsButton_Click(object sender, RoutedEventArgs e)
 {
 // Navigate to Generated Coupons page
 Frame.Navigate(typeof(GeneratedCouponsPage));
 }

 private void EditReceiptButton_Click(object sender, RoutedEventArgs e)
 {
 // Navigate to ReprintReceiptPage (contains edit/print/cancel functionality)
 Frame.Navigate(typeof(ReceiptPage));
 }

 private void ManageUsersButton_Click(object sender, RoutedEventArgs e)
 {
 // Navigate to UsersPage
 Frame.Navigate(typeof(UsersPage));
 }
 }
}
