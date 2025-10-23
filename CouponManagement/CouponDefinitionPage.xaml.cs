using CouponManagement.Shared.Models;
using CouponManagement.Shared.Services;
using CouponManagement.Dialogs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace CouponManagement
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var color = value?.ToString() switch
            {
                "ใช้งานได้" => Microsoft.UI.Colors.Green,
                "ไม่ใช้งาน" => Microsoft.UI.Colors.Gray,
                "หมดอายุ" => Microsoft.UI.Colors.Red,
                "ยังไม่เริ่ม" => Microsoft.UI.Colors.Orange,
                _ => Microsoft.UI.Colors.Gray
            };

            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal price)
                return $"{price:N2} บาท";
            return "0.00 บาท";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class DateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime date)
                return date.ToString("dd/MM/yyyy");
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ActiveToggleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (bool)value ? "ปิดใช้งาน" : "เปิดใช้งาน";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    // New converter to map bool -> Visibility
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b && b)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Visibility v)
                return v == Visibility.Visible;
            return false;
        }
    }

    public sealed partial class CouponDefinitionPage : Page
    {
        private readonly CouponDefinitionService _service;
        private readonly CouponService _couponService;
        public ObservableCollection<CouponDefinition> CouponDefinitions { get; set; }

        // สำหรับแสดงข้อความสถานะ
        private InfoBar? _statusInfoBar;

        public CouponDefinitionPage()
        {
            this.InitializeComponent();

            // Add converters to resources
            this.Resources["StatusColorConverter"] = new StatusColorConverter();
            this.Resources["CurrencyConverter"] = new CurrencyConverter();
            this.Resources["DateConverter"] = new DateConverter();
            this.Resources["ActiveToggleConverter"] = new ActiveToggleConverter();
            this.Resources["BoolToVisibilityConverter"] = new BoolToVisibilityConverter();

            _service = new CouponDefinitionService();
            _couponService = new CouponService();
            CouponDefinitions = new ObservableCollection<CouponDefinition>();
            CouponDefinitionsListView.ItemsSource = CouponDefinitions;

            // เพิ่ม InfoBar สำหรับแสดงข้อความสถานะ
            CreateStatusInfoBar();

            Loaded += CouponDefinitionPage_Loaded;
        }

        private void CreateStatusInfoBar()
        {
            _statusInfoBar = new InfoBar
            {
                IsOpen = false,
                Margin = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // หา main grid จาก XAML
            if (Content is Grid mainGrid)
            {
                // เพิ่ม row definition สำหรับ InfoBar
                mainGrid.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });
                
                // ปรับ row index ของ element อื่นๆ
                foreach (FrameworkElement child in mainGrid.Children)
                {
                    var currentRow = Grid.GetRow(child);
                    Grid.SetRow(child, currentRow + 1);
                }

                // เพิ่ม InfoBar
                Grid.SetRow(_statusInfoBar, 0);
                mainGrid.Children.Insert(0, _statusInfoBar);
            }
        }

        private void ShowStatusMessage(string message, InfoBarSeverity severity = InfoBarSeverity.Informational, int autoHideDelayMs = 3000)
        {
            if (_statusInfoBar == null) return;

            _statusInfoBar.Message = message;
            _statusInfoBar.Severity = severity;
            _statusInfoBar.IsOpen = true;

            // Auto hide after delay
            if (autoHideDelayMs > 0)
            {
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(autoHideDelayMs)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    if (_statusInfoBar != null)
                        _statusInfoBar.IsOpen = false;
                };
                timer.Start();
            }
        }

        private void ShowSuccessMessage(string message)
        {
            ShowStatusMessage(message, InfoBarSeverity.Success);
        }

        private void ShowErrorMessage(string message)
        {
            ShowStatusMessage(message, InfoBarSeverity.Error, 5000); // Error แสดงนานกว่า
        }

        private void ShowWarningMessage(string message)
        {
            ShowStatusMessage(message, InfoBarSeverity.Warning);
        }

        private async void CouponDefinitionPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Load coupon types from database first, then data
            await LoadCouponTypesAsync();
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var typeFilter = GetSelectedTag(TypeFilterComboBox);
                var statusFilter = GetSelectedTag(StatusFilterComboBox);
                var searchText = SearchTextBox.Text;

                var data = await _service.GetAllAsync(typeFilter, statusFilter, searchText);

                CouponDefinitions.Clear();
                foreach (var item in data)
                {
                    CouponDefinitions.Add(item);
                }
            }
            catch (OverflowException ex)
            {
                ShowErrorMessage($"เกิดข้อผิดพลาดด้านตัวเลข: ราคาหรือจำนวนเกินขีดจำกัด - {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"เกิดข้อผิดพลาดในการโหลดข้อมูล: {ex.Message}");
            }
        }

        private string GetSelectedTag(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
        }

        private async Task LoadCouponTypesAsync()
        {
            try
            {
                var types = await _couponService.GetAllCouponTypesAsync();

                TypeFilterComboBox.Items.Clear();
                // Always keep "ทั้งหมด"
                TypeFilterComboBox.Items.Add(new ComboBoxItem { Content = "ทั้งหมด", Tag = "ALL", IsSelected = true });

                if (types.Count == 0)
                {
                    // Create default type if DB is empty
                    var defaultType = await _couponService.AddCouponTypeAsync("คูปอง", Environment.UserName);
                    types.Add(defaultType);
                }

                foreach (var t in types)
                {
                    // Use CouponType.Id as Tag for filtering instead of behavior code
                    TypeFilterComboBox.Items.Add(new ComboBoxItem { Content = t.Name, Tag = t.Id.ToString() });
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"ไม่สามารถโหลดประเภทคูปองได้: {ex.Message}");
            }
        }

        private async void TypeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                await LoadDataAsync();
        }

        private async void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                await LoadDataAsync();
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
                await LoadDataAsync();
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            ShowStatusMessage("กำลังรีเฟรชข้อมูล...", InfoBarSeverity.Informational);
            await LoadDataAsync();
            ShowSuccessMessage("รีเฟรชข้อมูลเรียบร้อย");
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable button ระหว่างเปิด dialog
                CreateButton.IsEnabled = false;
                CreateButton.Content = "กำลังเปิด...";

                var dialog = new CouponDefinitionDialog();
                dialog.XamlRoot = this.XamlRoot;

                var result = await dialog.ShowAsync();
                
                if (dialog.WasSaved)
                {
                    ShowSuccessMessage("สร้างคำนิยามคูปองเรียบร้อย");
                }

                // Always refresh data after dialog closes to ensure UI is up-to-date
                await LoadDataAsync();
            }
            catch (OverflowException ex)
            {
                ShowErrorMessage($"ข้อผิดพลาดด้านตัวเลข: ราคาหรือจำนวนเกินขีดจำกัด - {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"เกิดข้อผิดพลาดในการสร้างคูปอง: {ex.Message}");
            }
            finally
            {
                // Reset button
                CreateButton.IsEnabled = true;
                CreateButton.Content = "สร้างใหม่";
            }
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CouponDefinition definition)
            {
                try
                {
                    // Disable button ระหว่างเปิด dialog
                    button.IsEnabled = false;
                    var originalContent = button.Content;
                    button.Content = "กำลังเปิด...";

                    var dialog = new CouponDefinitionDialog(definition);
                    dialog.XamlRoot = this.XamlRoot;

                    var result = await dialog.ShowAsync();

                    if (dialog.WasSaved)
                    {
                        ShowSuccessMessage("แก้ไขคำนิยามคูปองเรียบร้อย");
                    }

                    // Always refresh after dialog closes to ensure latest data
                    await LoadDataAsync();
                }
                catch (OverflowException ex)
                {
                    ShowErrorMessage($"ข้อผิดพลาดด้านตัวเลข: ราคาหรือจำนวนเกินขีดจำกัด - {ex.Message}");
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"เกิดข้อผิดพลาดในการแก้ไขคูปอง: {ex.Message}");
                }
                finally
                {
                    // Reset button
                    button.IsEnabled = true;
                    button.Content = "แก้ไข";
                }
            }
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is CouponDefinition definition)
            {
                // Prevent generation for unlimited coupons
                if (!definition.IsLimited)
                {
                    ShowWarningMessage("คูปองนี้เป็นแบบไม่จำกัดจำนวน — ไม่มีรหัสให้สร้าง");
                    return;
                }

                try
                {
                    // Disable button ระหว่างเปิด dialog
                    button.IsEnabled = false;
                    var originalContent = button.Content;
                    button.Content = "กำลังเปิด...";

                    var dialog = new GenerateCouponDialog(definition);
                    dialog.XamlRoot = this.XamlRoot;

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        ShowSuccessMessage("สร้างคูปองเรียบร้อย");
                        await LoadDataAsync();
                    }
                }
                catch (OverflowException ex)
                {
                    ShowErrorMessage($"ข้อผิดพลาดด้านตัวเลข: ราคาหรือจำนวนเกินขีดจำกัด - {ex.Message}");
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"เกิดข้อผิดพลาดในการสร้างคูปอง: {ex.Message}");
                }
                finally
                {
                    // Reset button
                    button.IsEnabled = true;
                    button.Content = "สร้างคูปอง";
                }
            }
        }

        private async void ActiveToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle && toggle.Tag is CouponDefinition definition)
            {
                var originalContent = toggle.Content;
                var newState = toggle.IsChecked ?? false;

                try
                {
                    // แสดงสถานะกำลังทำงาน
                    toggle.IsEnabled = false;
                    toggle.Content = "กำลังประมวลผล...";
                    
                    ShowStatusMessage($"กำลัง{(newState ? "เปิด" : "ปิด")}การใช้งานคูปอง...", InfoBarSeverity.Informational);

                    var success = await _service.SetActiveAsync(definition.Id, newState, Environment.UserName);

                    if (success)
                    {
                        ShowSuccessMessage($"{(newState ? "เปิด" : "ปิด")}การใช้งานคูปองเรียบร้อย");
                        await LoadDataAsync();
                    }
                    else
                    {
                        ShowErrorMessage("ไม่สามารถเปลี่ยนสถานะได้ - ไม่พบข้อมูลคูปอง");
                        toggle.IsChecked = !newState; // Revert
                    }
                }
                catch (OverflowException ex)
                {
                    ShowErrorMessage($"ข้อผิดพลาดด้านตัวเลข: ราคาหรือจำนวนเกินขีดจำกัด - {ex.Message}");
                    toggle.IsChecked = !newState; // Revert
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"เกิดข้อผิดพลาดในการเปลี่ยนสถานะ: {ex.Message}");
                    toggle.IsChecked = !newState; // Revert
                }
                finally
                {
                    // Reset toggle
                    toggle.IsEnabled = true;
                    toggle.Content = originalContent;
                }
            }
        }

        // Back button handler
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(CouponDefinitionPage));
            }
        }
    }
}