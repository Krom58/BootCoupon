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
using CouponManagement.Shared;
using CouponManagement.Shared.Models;

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
                LoadingProgressRing.IsActive = true;

                using (var context = new CouponContext())
                {
                    var receipts = await context.Receipts
                        .OrderBy(r => r.ReceiptID) // เรียงจากรหัสใบเสร็จน้อยไปมาก
                        .ToListAsync();

                    // โหลด PaymentMethods ทั้งหมดมาเก็บไว้ในหน่วยความจำ
                    var paymentMethods = await context.PaymentMethods.ToDictionaryAsync(pm => pm.Id, pm => pm.Name);

                    // โหลด SalesPersons ทั้งหมดมาเก็บไว้ในหน่วยความจำ
                    var salesPersons = await context.SalesPerson.ToDictionaryAsync(sp => sp.ID, sp => sp.Name);

                    _receipts.Clear();
                    foreach (var receipt in receipts)
                    {
                        string paymentMethodName = "";
                        if (receipt.PaymentMethodId.HasValue && paymentMethods.ContainsKey(receipt.PaymentMethodId.Value))
                        {
                            paymentMethodName = paymentMethods[receipt.PaymentMethodId.Value];
                        }

                        string salesPersonName = "";
                        if (receipt.SalesPersonId.HasValue && salesPersons.ContainsKey(receipt.SalesPersonId.Value))
                        {
                            salesPersonName = salesPersons[receipt.SalesPersonId.Value];
                        }

                        _receipts.Add(new ReceiptDisplayModel
                        {
                            ReceiptID = receipt.ReceiptID,
                            ReceiptDate = receipt.ReceiptDate,
                            TotalAmount = receipt.TotalAmount,
                            CustomerName = receipt.CustomerName ?? "",
                            CustomerPhoneNumber = receipt.CustomerPhoneNumber ?? "",
                            ReceiptCode = receipt.ReceiptCode ?? "",
                            SalesPersonId = receipt.SalesPersonId,
                            SalesPersonName = salesPersonName,
                            Status = receipt.Status ?? "Active",
                            PaymentMethodId = receipt.PaymentMethodId,
                            PaymentMethodName = paymentMethodName,
                            // ✅ เพิ่มข้อมูลเหตุผล
                            CancellationReason = receipt.CancellationReason,
                            CancelledDate = receipt.CancelledDate
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
            EditReceiptButton.IsEnabled = hasSelection && selectedReceipt?.Status == "Active";
            // เปิดปุ่มยกเลิกใบเสร็จสำหรับใบเสร็จที่ Active เท่านั้น
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
                // ✅ เรียกใช้ PrintReceiptAsync โดยไม่ต้องคำนวณ discount เอง
                // ให้ ReceiptPrintService ดึงข้อมูลและคำนวณทั้งหมดเอง
                var success = await ReceiptPrintService.PrintReceiptAsync(
                    selectedReceipt.ReceiptID,
                    this.XamlRoot,
                    comDiscount: 0m  // ส่ง 0 เพื่อบอกให้ service คำนวณเอง
                );

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

            // Create reason input
            var reasonTextBox = new TextBox
            {
                PlaceholderText = "กรุณาระบุเหตุผลการยกเลิก (บังคับ)",
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MinHeight = 100,
                MaxHeight = 200
            };

            var contentPanel = new StackPanel { Spacing = 15 };

            contentPanel.Children.Add(new TextBlock
            {
                Text = $"คุณแน่ใจหรือไม่ว่าต้องการยกเลิกใบเสร็จ?\n\n" +
                       $"รหัสใบเสร็จ: {selectedReceipt.ReceiptCode}\n" +
                       $"ชื่อลูกค้า: {selectedReceipt.CustomerName}\n" +
                       $"ยอดเงิน: {selectedReceipt.TotalAmountFormatted} บาท",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });

            contentPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 230, 245, 255)),
                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 120, 170, 220)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Child = new TextBlock
                {
                    Text = "⚠️ เมื่อยกเลิกแล้ว:\n" +
                           "• ใบเสร็จจะถูกทำเครื่องหมายว่า \"ยกเลิก\"\n" +
                           "• คูปองที่ผูกกับใบเสร็จนี้จะถูกคืนสถานะกลับเป็นยังไม่ได้ใช้\n" +
                           "• คูปองที่มีรหัสจะสามารถนำไปใช้ใหม่ได้",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 10, 64, 96)),
                    TextWrapping = TextWrapping.Wrap
                }
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = "เหตุผลการยกเลิก:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red)
            });
            contentPanel.Children.Add(reasonTextBox);

            var errorTextBlock = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                FontSize = 12,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, -10, 0, 0)
            };
            contentPanel.Children.Add(errorTextBlock);

            var confirmDialog = new ContentDialog
            {
                Title = "⚠️ ยืนยันการยกเลิกใบเสร็จ",
                Content = contentPanel,
                PrimaryButtonText = "ยืนยันการยกเลิก",
                SecondaryButtonText = "ไม่ยกเลิก",
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot
            };

            confirmDialog.PrimaryButtonClick += (s, args) =>
            {
                var reason = reasonTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(reason))
                {
                    args.Cancel = true;
                    errorTextBlock.Text = "❌ กรุณาระบุเหตุผลการยกเลิก";
                    errorTextBlock.Visibility = Visibility.Visible;
                }
                else if (reason.Length < 5)
                {
                    args.Cancel = true;
                    errorTextBlock.Text = "❌ เหตุผลต้องมีความยาวอย่างน้อย 5 ตัวอักษร";
                    errorTextBlock.Visibility = Visibility.Visible;
                }
                else
                {
                    errorTextBlock.Visibility = Visibility.Collapsed;
                }
            };

            reasonTextBox.TextChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(reasonTextBox.Text?.Trim()))
                {
                    errorTextBlock.Visibility = Visibility.Collapsed;
                }
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var cancellationReason = reasonTextBox.Text.Trim();

                try
                {
                    using (var context = new CouponContext())
                    {
                        // load receipt with items
                        var receipt = await context.Receipts
                            .Include(r => r.Items)
                            .FirstOrDefaultAsync(r => r.ReceiptID == selectedReceipt.ReceiptID);

                        if (receipt == null)
                        {
                            await ShowErrorDialog("ไม่พบข้อมูลใบเสร็จ");
                            return;
                        }

                        // gather receipt item ids
                        var receiptItemIds = receipt.Items?.Select(ri => ri.ReceiptItemId).ToList() ?? new List<int>();

                        // start transaction to ensure history + updates are atomic
                        using var tx = await context.Database.BeginTransactionAsync();

                        try
                        {
                            var releasedCouponsCount = 0;
                            var releasedCouponCodes = new List<string>();

                            if (receiptItemIds.Any())
                            {
                                // find linked generated coupons
                                var linkedCoupons = await context.GeneratedCoupons
                                    .Where(gc => gc.ReceiptItemId != null && receiptItemIds.Contains(gc.ReceiptItemId.Value))
                                    .ToListAsync();

                                if (linkedCoupons.Any())
                                {
                                    // insert history for each coupon BEFORE changing it
                                    foreach (var coupon in linkedCoupons)
                                    {
                                        await context.Database.ExecuteSqlInterpolatedAsync($@"
                                    INSERT INTO dbo.GeneratedCouponsHistory
                                        (GeneratedCouponId, CouponDefinitionId, GeneratedCode, BatchNumber, IsComplimentary, IsUsed, UsedDate, UsedBy, ReceiptItemId, ReceiptId, ReceiptCode, ExpiresAt, CreatedAt, CreatedBy, ActionType, ActionBy, ActionAt, ActionReason)
                                    VALUES
                                        ({coupon.Id}, {coupon.CouponDefinitionId}, {coupon.GeneratedCode}, {coupon.BatchNumber}, {coupon.IsComplimentary}, {coupon.IsUsed}, {coupon.UsedDate}, {coupon.UsedBy}, {coupon.ReceiptItemId}, {receipt.ReceiptID}, {receipt.ReceiptCode}, {coupon.ExpiresAt}, {coupon.CreatedAt}, {coupon.CreatedBy}, {"ReceiptCancelled"}, {Environment.UserName}, {DateTime.Now}, {cancellationReason});
                                ");

                                        // then restore coupon state
                                        coupon.IsUsed = false;
                                        coupon.UsedDate = null;
                                        coupon.UsedBy = null;
                                        coupon.ReceiptItemId = null;
                                        context.GeneratedCoupons.Update(coupon);

                                        releasedCouponsCount++;
                                        releasedCouponCodes.Add(coupon.GeneratedCode);
                                    }

                                    // save changes for coupons/history so far
                                    await context.SaveChangesAsync();
                                }
                            }

                            // update receipt status and audit fields
                            receipt.Status = "Cancelled";
                            receipt.CancellationReason = cancellationReason;
                            receipt.CancelledDate = DateTime.Now;

                            await context.SaveChangesAsync();

                            await tx.CommitAsync();

                            // Show result to user
                            string message = $"✅ ยกเลิกใบเสร็จ {selectedReceipt.ReceiptCode} เรียบร้อยแล้ว\n\n";
                            message += $"📝 เหตุผล: {cancellationReason}\n";

                            if (releasedCouponsCount > 0)
                            {
                                message += $"\n📋 คูปองที่ถูกคืนสถานะ: {releasedCouponsCount} รายการ";

                                if (releasedCouponCodes.Count <= 5)
                                {
                                    message += $"\n\nรหัสคูปอง:\n{string.Join("\n", releasedCouponCodes)}";
                                }
                                else
                                {
                                    message += $"\n\nรหัสคูปอง (แสดง 5 รายการแรก):\n{string.Join("\n", releasedCouponCodes.Take(5))}";
                                    message += $"\n...และอีก {releasedCouponCodes.Count - 5} รายการ";
                                }
                            }

                            await ShowSuccessDialog(message);

                            // refresh list
                            await LoadReceiptsAsync();
                        }
                        catch (Exception exInner)
                        {
                            await tx.RollbackAsync();
                            Debug.WriteLine($"Error during cancel transaction: {exInner.Message}");
                            await ShowErrorDialog($"เกิดข้อผิดพลาดในการยกเลิกใบเสร็จ (transaction): {exInner.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error cancelling receipt: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    await ShowErrorDialog($"เกิดข้อผิดพลาดในการยกเลิกใบเสร็จ:\n{ex.Message}");
                }
            }
        }
        private async void EditReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReceiptsDataGrid.SelectedItem is not ReceiptDisplayModel selectedReceipt)
            {
                await ShowErrorDialog("กรุณาเลือกใบเสร็จที่ต้องการแก้ไข");
                return;
            }

            if (selectedReceipt.Status != "Active")
            {
                await ShowErrorDialog("ไม่สามารถแก้ไขใบเสร็จที่ถูกยกเลิกแล้ว");
                return;
            }

            try
            {
                using (var context = new CouponContext())
                {
                    // โหลดข้อมูลใบเสร็จจากฐานข้อมูล
                    var receipt = await context.Receipts
                        .FirstOrDefaultAsync(r => r.ReceiptID == selectedReceipt.ReceiptID);

                    if (receipt == null)
                    {
                        await ShowErrorDialog("ไม่พบข้อมูลใบเสร็จ");
                        return;
                    }

                    // โหลดรายการ SalesPerson
                    var salesPersons = await context.SalesPerson.ToListAsync();

                    // โหลดรายการ PaymentMethods
                    var paymentMethods = await context.PaymentMethods.Where(pm => pm.IsActive).ToListAsync();

                    // คำนวณยอดรวมก่อนส่วนลด
                    decimal totalBeforeDiscount = receipt.TotalAmount + receipt.Discount;

                    // สร้าง Dialog สำหรับแก้ไขข้อมูล
                    var editPanel = new StackPanel { Spacing = 10 };

                    // ชื่อลูกค้า
                    editPanel.Children.Add(new TextBlock { Text = "ชื่อลูกค้า:", FontWeight = Microsoft.UI.Text.FontWeights.Medium });
                    var customerNameBox = new TextBox
                    {
                        Text = receipt.CustomerName,
                        PlaceholderText = "กรุณาระบุชื่อลูกค้า"
                    };
                    editPanel.Children.Add(customerNameBox);

                    // เบอร์โทรศัพท์
                    editPanel.Children.Add(new TextBlock { Text = "เบอร์โทรศัพท์:", FontWeight = Microsoft.UI.Text.FontWeights.Medium, Margin = new Thickness(0, 10, 0, 0) });
                    var phoneNumberBox = new TextBox
                    {
                        Text = receipt.CustomerPhoneNumber,
                        PlaceholderText = "กรุณาระบุเบอร์โทรศัพท์"
                    };
                    editPanel.Children.Add(phoneNumberBox);

                    // เซลล์
                    editPanel.Children.Add(new TextBlock { Text = "เซลล์:", FontWeight = Microsoft.UI.Text.FontWeights.Medium, Margin = new Thickness(0, 10, 0, 0) });
                    var salesPersonComboBox = new ComboBox
                    {
                        ItemsSource = salesPersons,
                        DisplayMemberPath = "Name",
                        SelectedValuePath = "ID",
                        SelectedValue = receipt.SalesPersonId,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        PlaceholderText = "เลือกเซลล์"
                    };
                    editPanel.Children.Add(salesPersonComboBox);

                    // ประเภทการจ่าย
                    editPanel.Children.Add(new TextBlock { Text = "ประเภทการจ่าย:", FontWeight = Microsoft.UI.Text.FontWeights.Medium, Margin = new Thickness(0, 10, 0, 0) });
                    var paymentMethodComboBox = new ComboBox
                    {
                        ItemsSource = paymentMethods,
                        DisplayMemberPath = "Name",
                        SelectedValuePath = "Id",
                        SelectedValue = receipt.PaymentMethodId,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        PlaceholderText = "เลือกประเภทการจ่าย"
                    };
                    editPanel.Children.Add(paymentMethodComboBox);

                    // ส่วนลด
                    editPanel.Children.Add(new TextBlock { Text = "ส่วนลด (บาท):", FontWeight = Microsoft.UI.Text.FontWeights.Medium, Margin = new Thickness(0, 10, 0, 0) });
                    var discountBox = new NumberBox
                    {
                        Value = (double)receipt.Discount,
                        Minimum = 0,
                        Maximum = (double)totalBeforeDiscount,
                        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                        PlaceholderText = "ระบุส่วนลด",
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    editPanel.Children.Add(discountBox);

                    // แสดงยอดรวมก่อนส่วนลด
                    var totalBeforeText = new TextBlock
                    {
                        Text = $"ยอดรวมก่อนส่วนลด: {totalBeforeDiscount:N2} บาท",
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                        Margin = new Thickness(0, 5, 0, 0)
                    };
                    editPanel.Children.Add(totalBeforeText);

                    // แสดงยอดสุทธิที่คำนวณใหม่
                    var netTotalText = new TextBlock
                    {
                        Text = $"ยอดสุทธิ: {(totalBeforeDiscount - receipt.Discount):N2} บาท",
                        FontSize = 14,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green),
                        Margin = new Thickness(0, 5, 0, 0)
                    };
                    editPanel.Children.Add(netTotalText);

                    // อัปเดตยอดสุทธิเมื่อส่วนลดเปลี่ยน
                    discountBox.ValueChanged += (s, args) =>
                    {
                        var discount = double.IsNaN(discountBox.Value) ? 0 : discountBox.Value;
                        var netTotal = totalBeforeDiscount - (decimal)discount;
                        netTotalText.Text = $"ยอดสุทธิ: {netTotal:N2} บาท";
                    };

                    var editDialog = new ContentDialog
                    {
                        Title = $"แก้ไขข้อมูลใบเสร็จ {receipt.ReceiptCode}",
                        Content = editPanel,
                        PrimaryButtonText = "บันทึก",
                        CloseButtonText = "ยกเลิก",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await editDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        // ตรวจสอบข้อมูล
                        string newCustomerName = customerNameBox.Text.Trim();
                        string newPhoneNumber = phoneNumberBox.Text.Trim();

                        if (string.IsNullOrEmpty(newCustomerName) || string.IsNullOrEmpty(newPhoneNumber))
                        {
                            await ShowErrorDialog("กรุณากรอกข้อมูลชื่อลูกค้าและเบอร์โทรศัพท์ให้ครบถ้วน");
                            return;
                        }

                        if (salesPersonComboBox.SelectedValue == null)
                        {
                            await ShowErrorDialog("กรุณาเลือกเซลล์");
                            return;
                        }

                        if (paymentMethodComboBox.SelectedValue == null)
                        {
                            await ShowErrorDialog("กรุณาเลือกประเภทการจ่าย");
                            return;
                        }

                        // ตรวจสอบส่วนลด
                        var newDiscount = double.IsNaN(discountBox.Value) ? 0 : discountBox.Value;
                        if (newDiscount < 0 || newDiscount > (double)totalBeforeDiscount)
                        {
                            await ShowErrorDialog($"ส่วนลดต้องอยู่ระหว่าง 0 ถึง {totalBeforeDiscount:N2} บาท");
                            return;
                        }

                        // อัปเดตข้อมูล
                        receipt.CustomerName = newCustomerName;
                        receipt.CustomerPhoneNumber = newPhoneNumber;
                        receipt.SalesPersonId = (int)salesPersonComboBox.SelectedValue;
                        receipt.PaymentMethodId = (int)paymentMethodComboBox.SelectedValue;
                        receipt.Discount = (decimal)newDiscount;
                        receipt.TotalAmount = totalBeforeDiscount - (decimal)newDiscount;

                        await context.SaveChangesAsync();

                        await ShowSuccessDialog("แก้ไขข้อมูลใบเสร็จเรียบร้อยแล้ว");
                        await LoadReceiptsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error editing receipt: {ex.Message}");
                await ShowErrorDialog($"เกิดข้อผิดพลาดในการแก้ไขใบเสร็จ: {ex.Message}");
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