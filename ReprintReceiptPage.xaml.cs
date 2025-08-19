using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace BootCoupon
{
    // Value converter for status badge colors
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string status)
            {
                return status switch
                {
                    "Active" => new SolidColorBrush(Color.FromArgb(255, 16, 124, 16)), // Green
                    "Cancelled" => new SolidColorBrush(Color.FromArgb(255, 209, 52, 56)), // Red
                    _ => new SolidColorBrush(Color.FromArgb(255, 120, 120, 120)) // Gray
                };
            }
            return new SolidColorBrush(Color.FromArgb(255, 120, 120, 120));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed partial class ReprintReceiptPage : Page
    {
        private ObservableCollection<ReceiptDisplayModel> _receipts;
        private ObservableCollection<ReceiptDisplayModel> _filteredReceipts;
        private string _currentStatusFilter = "Active";
        private string _currentSearchText = string.Empty;
        private bool _isInitialized = false;

        public ReprintReceiptPage()
        {
            this.InitializeComponent();
            
            // Add the converter to resources
            this.Resources["StatusToBrushConverter"] = new StatusToBrushConverter();
            
            // Initialize collections
            _receipts = new ObservableCollection<ReceiptDisplayModel>();
            _filteredReceipts = new ObservableCollection<ReceiptDisplayModel>();
            
            // Set up ListView
            ReceiptsDataGrid.ItemsSource = _filteredReceipts;
            ReceiptsDataGrid.SelectionChanged += ReceiptsDataGrid_SelectionChanged;
            
            // Mark as initialized
            _isInitialized = true;
            
            Loaded += ReprintReceiptPage_Loaded;
        }

        private async void ReprintReceiptPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadReceiptsAsync();
        }

        private async Task LoadReceiptsAsync()
        {
            try
            {
                // Show loading indicator
                LoadingProgressRing.IsActive = true;
                
                using (var context = new CouponContext())
                {
                    var receipts = await context.Receipts
                        .OrderBy(r => r.ReceiptID) // เรียงจากรหัสใบเสร็จน้อยไปมาก
                        .ToListAsync();

                    _receipts.Clear();
                    foreach (var receipt in receipts)
                    {
                        _receipts.Add(new ReceiptDisplayModel
                        {
                            ReceiptID = receipt.ReceiptID,
                            ReceiptDate = receipt.ReceiptDate,
                            TotalAmount = receipt.TotalAmount,
                            CustomerName = receipt.CustomerName ?? "",
                            CustomerPhoneNumber = receipt.CustomerPhoneNumber ?? "",
                            ReceiptCode = receipt.ReceiptCode ?? "",
                            SalesPersonId = receipt.SalesPersonId,
                            Status = receipt.Status ?? "Active"
                        });
                    }

                    ApplyFilters();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading receipts: {ex.Message}");
                await ShowErrorDialog($"ไม่สามารถโหลดข้อมูลใบเสร็จได้: {ex.Message}");
            }
            finally
            {
                // Hide loading indicator
                LoadingProgressRing.IsActive = false;
                UpdateEmptyState();
            }
        }

        private void ApplyFilters()
        {
            if (!_isInitialized || _filteredReceipts == null || _receipts == null) 
                return;
                
            _filteredReceipts.Clear();
            
            var filtered = _receipts.AsEnumerable();
            
            // Filter by status
            if (_currentStatusFilter != "All")
            {
                filtered = filtered.Where(r => r.Status == _currentStatusFilter);
            }
            
            // Filter by search text
            if (!string.IsNullOrWhiteSpace(_currentSearchText))
            {
                var searchLower = _currentSearchText.ToLower();
                filtered = filtered.Where(r => 
                    (r.CustomerName?.ToLower().Contains(searchLower) ?? false) ||
                    (r.ReceiptCode?.ToLower().Contains(searchLower) ?? false) ||
                    (r.CustomerPhoneNumber?.Contains(_currentSearchText) ?? false) ||
                    r.ReceiptID.ToString().Contains(_currentSearchText)
                );
            }
            
            // เรียงตามรหัสใบเสร็จจากน้อยไปมาก หลังจากกรองแล้ว
            var sortedFiltered = filtered.OrderBy(r => r.ReceiptID);
            
            foreach (var receipt in sortedFiltered)
            {
                _filteredReceipts.Add(receipt);
            }
            
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            // This would be implemented in the code-behind to show/hide empty state
            // For now, we'll rely on the ListView's built-in empty state handling
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            
            if (StatusFilterComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _currentStatusFilter = selectedItem.Tag?.ToString() ?? "All";
                ApplyFilters();
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;
            
            _currentSearchText = SearchTextBox.Text ?? "";
            ApplyFilters();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadReceiptsAsync();
        }

        private void ReceiptsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = ReceiptsDataGrid.SelectedItem != null;
            var selectedReceipt = ReceiptsDataGrid.SelectedItem as ReceiptDisplayModel;
            
            ReprintButton.IsEnabled = hasSelection && selectedReceipt?.Status == "Active";
            CancelReceiptButton.IsEnabled = hasSelection && selectedReceipt?.Status == "Active";
        }

        private async void ReprintButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReceiptsDataGrid.SelectedItem is not ReceiptDisplayModel selectedReceipt)
            {
                await ShowErrorDialog("กรุณาเลือกใบเสร็จที่ต้องการพิมพ์ใหม่");
                return;
            }

            if (selectedReceipt.Status != "Active")
            {
                await ShowErrorDialog("ไม่สามารถพิมพ์ใบเสร็จที่ถูกยกเลิกแล้ว");
                return;
            }

            try
            {
                var success = await ReceiptPrintService.PrintReceiptAsync(selectedReceipt.ReceiptID, this.XamlRoot);
                
                if (success)
                {
                    await ShowSuccessDialog("ส่งใบเสร็จไปยังเครื่องพิมพ์เรียบร้อยแล้ว");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reprinting receipt: {ex.Message}");
                await ShowErrorDialog($"เกิดข้อผิดพลาดในการพิมพ์ใบเสร็จ: {ex.Message}");
            }
        }

        private async void CancelReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReceiptsDataGrid.SelectedItem is not ReceiptDisplayModel selectedReceipt)
            {
                await ShowErrorDialog("กรุณาเลือกใบเสร็จที่ต้องการยกเลิก");
                return;
            }

            if (selectedReceipt.Status != "Active")
            {
                await ShowErrorDialog("ใบเสร็จนี้ถูกยกเลิกแล้ว");
                return;
            }

            var confirmDialog = new ContentDialog
            {
                Title = "ยืนยันการยกเลิกใบเสร็จ",
                Content = $"คุณต้องการยกเลิกใบเสร็จ {selectedReceipt.ReceiptCode} ใช่หรือไม่?\n\nใบเสร็จที่ยกเลิกแล้วจะไม่สามารถใช้งานได้",
                PrimaryButtonText = "ยกเลิกใบเสร็จ",
                SecondaryButtonText = "ไม่ยกเลิก",
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await UpdateReceiptStatusAsync(selectedReceipt.ReceiptID, "Cancelled");
                    await LoadReceiptsAsync();
                    await ShowSuccessDialog($"ยกเลิกใบเสร็จ {selectedReceipt.ReceiptCode} เรียบร้อยแล้ว");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error cancelling receipt: {ex.Message}");
                    await ShowErrorDialog($"เกิดข้อผิดพลาดในการยกเลิกใบเสร็จ: {ex.Message}");
                }
            }
        }

        private async Task UpdateReceiptStatusAsync(int receiptId, string newStatus)
        {
            using (var context = new CouponContext())
            {
                var receipt = await context.Receipts.FindAsync(receiptId);
                if (receipt != null)
                {
                    receipt.Status = newStatus;
                    await context.SaveChangesAsync();
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(MainPage));
        }

        private async Task ShowErrorDialog(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "ข้อผิดพลาด",
                Content = message,
                CloseButtonText = "ตกลง",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ShowSuccessDialog(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "สำเร็จ",
                Content = message,
                CloseButtonText = "ตกลง",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
