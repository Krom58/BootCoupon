using BootCoupon.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Printing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Printing;
using Windows.Graphics.Printing.OptionDetails;
using WinRT.Interop;

namespace BootCoupon
{
    public class ReceiptItem
    {
        public Coupon Coupon { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal TotalPrice => Coupon.Price * Quantity;
    }

    public sealed partial class Receipt : Page
    {
        private readonly CouponContext _context = new CouponContext();
        private readonly ObservableCollection<Coupon> _coupons = new();
        private readonly ObservableCollection<ReceiptItem> _selectedItems = new();
        private readonly ObservableCollection<SalesPerson> _salesPersons = new();
        private readonly ObservableCollection<CouponType> _couponTypes = new();
        private readonly List<PaymentMethod> _paymentMethods = new();
        private readonly List<CheckBox> _paymentCheckBoxes = new();

        // เพิ่มตัวแปรสำหรับ debounce
        private System.Threading.Timer? _searchTimer;
        private readonly object _searchLock = new object();

        public Receipt()
        {
            InitializeComponent();
            CouponsListView.ItemsSource = _coupons;
            SelectedItemsListView.ItemsSource = _selectedItems;
            SalesPersonComboBox.ItemsSource = _salesPersons;
            CouponTypeComboBox.ItemsSource = _couponTypes;
            this.Loaded += Receipt_Loaded;
        }

        private async void Receipt_Loaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe so it only runs once
            this.Loaded -= Receipt_Loaded;

            try
            {
                await LoadAllCoupons();
                await LoadSalesPersons();
                await LoadCouponTypes();
                await LoadPaymentMethods();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Error during load: {ex.Message}");
            }
        }

        private async Task LoadPaymentMethods()
        {
            try
            {
                await _context.Database.EnsureCreatedAsync();

                // ตรวจสอบว่าตาราง PaymentMethods มีข้อมูลหรือไม่
                var paymentMethodsExist = await _context.PaymentMethods.AnyAsync();
                
                if (!paymentMethodsExist)
                {
                    // สร้างข้อมูลเริ่มต้นถ้ายังไม่มี
                    var defaultPaymentMethods = new List<PaymentMethod>
                    {
                        new PaymentMethod { Name = "เงินสด", IsActive = true },
                        new PaymentMethod { Name = "เงินโอน", IsActive = true },
                        new PaymentMethod { Name = "เครดิตการ์ด", IsActive = true },
                        new PaymentMethod { Name = "QR", IsActive = true }
                    };

                    _context.PaymentMethods.AddRange(defaultPaymentMethods);
                    await _context.SaveChangesAsync();
                    
                    Debug.WriteLine("สร้างข้อมูล Payment Methods เริ่มต้นเรียบร้อย");
                }

                var paymentMethods = await _context.PaymentMethods
                    .Where(pm => pm.IsActive)
                    .OrderBy(pm => pm.Id)
                    .ToListAsync();

                _paymentMethods.Clear();
                _paymentMethods.AddRange(paymentMethods);

                Debug.WriteLine($"โหลด Payment Methods สำเร็จ: {paymentMethods.Count} รายการ");

                // สร้างเช็คบ็อกแบบไดนามิก
                CreatePaymentMethodCheckBoxes();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการโหลด Payment Methods: {ex.Message}");
                
                // สร้าง payment methods แบบ hardcode ถ้าเกิดข้อผิดพลาด
                CreateFallbackPaymentMethods();
            }
        }

        private void CreateFallbackPaymentMethods()
        {
            try
            {
                _paymentMethods.Clear();
                _paymentMethods.AddRange(new List<PaymentMethod>
                {
                    new PaymentMethod { Id = 1, Name = "เงินสด", IsActive = true },
                    new PaymentMethod { Id = 2, Name = "เงินโอน", IsActive = true },
                    new PaymentMethod { Id = 3, Name = "เครดิตการ์ด", IsActive = true },
                    new PaymentMethod { Id = 4, Name = "QR", IsActive = true }
                });

                CreatePaymentMethodCheckBoxes();
                Debug.WriteLine("สร้าง Payment Methods แบบ fallback สำเร็จ");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง fallback payment methods: {ex.Message}");
            }
        }

        private void CreatePaymentMethodCheckBoxes()
        {
            PaymentMethodPanel.Children.Clear();
            _paymentCheckBoxes.Clear();

            foreach (var paymentMethod in _paymentMethods)
            {
                var checkBox = new CheckBox
                {
                    Content = paymentMethod.Name,
                    Tag = paymentMethod,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                checkBox.Checked += PaymentMethodCheckBox_Checked;
                checkBox.Unchecked += PaymentMethodCheckBox_Unchecked;

                _paymentCheckBoxes.Add(checkBox);
                PaymentMethodPanel.Children.Add(checkBox);
            }
        }

        private void PaymentMethodCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkedBox = sender as CheckBox;
            if (checkedBox != null)
            {
                // ยกเลิกการเลือกเช็คบ็อกอื่นๆ (อนุญาตให้เลือกได้เพียง 1 อัน)
                foreach (var checkBox in _paymentCheckBoxes)
                {
                    if (checkBox != checkedBox)
                    {
                        checkBox.Unchecked -= PaymentMethodCheckBox_Unchecked;
                        checkBox.IsChecked = false;
                        checkBox.Unchecked += PaymentMethodCheckBox_Unchecked;
                    }
                }

                // ซ่อนข้อความเตือน
                PaymentMethodErrorText.Visibility = Visibility.Collapsed;
            }
        }

        private void PaymentMethodCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // ตรวจสอบว่ามีเช็คบ็อกใดถูกเลือกอยู่หรือไม่
            var hasChecked = _paymentCheckBoxes.Any(cb => cb.IsChecked == true);
            if (!hasChecked)
            {
                PaymentMethodErrorText.Visibility = Visibility.Visible;
            }
        }

        private PaymentMethod? GetSelectedPaymentMethod()
        {
            var checkedBox = _paymentCheckBoxes.FirstOrDefault(cb => cb.IsChecked == true);
            return checkedBox?.Tag as PaymentMethod;
        }

        private async Task LoadSalesPersons() // โหลดเซลล์
        {
            await _context.Database.EnsureCreatedAsync();

            var salesPersons = await _context.SalesPerson.ToListAsync();
            _salesPersons.Clear();
            foreach (var sp in salesPersons)
            {
                _salesPersons.Add(sp);
            }
        }

        private async Task LoadAllCoupons()
        {
            await _context.Database.EnsureCreatedAsync();

            var coupons = await _context.Coupons
                .Include(c => c.CouponType)
                .ToListAsync();

            _coupons.Clear();
            foreach (var coupon in coupons)
            {
                _coupons.Add(coupon);
            }
        }

        private async Task LoadCouponTypes()
        {
            await _context.Database.EnsureCreatedAsync();

            var couponTypes = await _context.CouponTypes.ToListAsync();
            _couponTypes.Clear();
            
            // เพิ่มตัวเลือก "ทั้งหมด"
            _couponTypes.Add(new CouponType { Id = 0, Name = "ทั้งหมด" });
            
            foreach (var type in couponTypes)
            {
                _couponTypes.Add(type);
            }
        }

        // แทนที่ SearchButton_Click ด้วย event handlers สำหรับ real-time search
        private void NameSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PerformDelayedSearch();
        }

        private void CodeSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PerformDelayedSearch();
        }

        private void CouponTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PerformDelayedSearch();
        }
        private void PerformDelayedSearch()
        {
            lock (_searchLock)
            {
                // ยกเลิก timer เก่า
                _searchTimer?.Dispose();

                // สร้าง timer ใหม่ที่จะค้นหาหลังจาก 300ms
                _searchTimer = new System.Threading.Timer(_ =>
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await PerformSearch();
                    });
                }, null, 300, Timeout.Infinite);
            }
        }

        // Method สำหรับค้นหาจริง
        private async Task PerformSearch()
        {
            try
            {
                string nameSearch = NameSearchTextBox.Text.Trim().ToLower();
                string codeSearch = CodeSearchTextBox.Text.Trim().ToLower();
                var selectedType = CouponTypeComboBox.SelectedItem as CouponType;

                await _context.Database.EnsureCreatedAsync();

                var query = _context.Coupons.Include(c => c.CouponType).AsQueryable();

                if (!string.IsNullOrEmpty(nameSearch))
                {
                    query = query.Where(c => c.Name.ToLower().Contains(nameSearch));
                }

                if (!string.IsNullOrEmpty(codeSearch))
                {
                    query = query.Where(c => c.Code.ToLower().Contains(codeSearch));
                }

                // เพิ่มการค้นหาตามประเภท (ไม่รวม "ทั้งหมด" ที่มี Id = 0)
                if (selectedType != null && selectedType.Id > 0)
                {
                    query = query.Where(c => c.CouponTypeId == selectedType.Id);
                }

                var coupons = await query.ToListAsync();

                _coupons.Clear();
                foreach (var coupon in coupons)
                {
                    _coupons.Add(coupon);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการค้นหา: {ex.Message}");
                // ไม่แสดง error dialog เพื่อไม่ให้รบกวนผู้ใช้ขณะพิมพ์
            }
        }

        private async void SelectCouponButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is Coupon selectedCoupon)
            {
                // Create quantity dialog
                var quantityBox = new NumberBox
                {
                    Value = 1,
                    Minimum = 1,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var priceTextBlock = new TextBlock
                {
                    Text = $"ราคา: {selectedCoupon.Price} บาท/ใบ",
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var totalPriceTextBlock = new TextBlock
                {
                    Text = $"รวมเป็นเงิน: {selectedCoupon.Price} บาท",
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                quantityBox.ValueChanged += (s, args) =>
                {
                    if (quantityBox.Value > 0)
                    {
                        totalPriceTextBlock.Text = $"รวมเป็นเงิน: {selectedCoupon.Price * (decimal)quantityBox.Value} บาท";
                    }
                };

                var contentPanel = new StackPanel();
                contentPanel.Children.Add(new TextBlock { Text = $"คูปอง: {selectedCoupon.Name}" });
                contentPanel.Children.Add(priceTextBlock);
                contentPanel.Children.Add(new TextBlock { Text = "กรุณาระบุจำนวน:" });
                contentPanel.Children.Add(quantityBox);
                contentPanel.Children.Add(totalPriceTextBlock);

                var dialog = new ContentDialog
                {
                    Title = "เลือกจำนวนคูปอง",
                    Content = contentPanel,
                    PrimaryButtonText = "ตกลง",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // Add to selected items
                    var quantity = (int)quantityBox.Value;
                    var receiptItem = new ReceiptItem
                    {
                        Coupon = selectedCoupon,
                        Quantity = quantity
                    };

                    _selectedItems.Add(receiptItem);
                    UpdateTotalPrice();
                }
            }
        }

        private async void EditItemButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is ReceiptItem selectedItem)
            {
                // Create edit quantity dialog
                var quantityBox = new NumberBox
                {
                    Value = selectedItem.Quantity,
                    Minimum = 1,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var priceTextBlock = new TextBlock
                {
                    Text = $"ราคา: {selectedItem.Coupon.Price} บาท/ใบ",
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var totalPriceTextBlock = new TextBlock
                {
                    Text = $"รวมเป็นเงิน: {selectedItem.Coupon.Price * selectedItem.Quantity} บาท",
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                quantityBox.ValueChanged += (s, args) =>
                {
                    if (quantityBox.Value > 0)
                    {
                        totalPriceTextBlock.Text = $"รวมเป็นเงิน: {selectedItem.Coupon.Price * (decimal)quantityBox.Value} บาท";
                    }
                };

                var contentPanel = new StackPanel();
                contentPanel.Children.Add(new TextBlock { Text = $"คูปอง: {selectedItem.Coupon.Name}" });
                contentPanel.Children.Add(priceTextBlock);
                contentPanel.Children.Add(new TextBlock { Text = "กรุณาระบุจำนวน:" });
                contentPanel.Children.Add(quantityBox);
                contentPanel.Children.Add(totalPriceTextBlock);

                var dialog = new ContentDialog
                {
                    Title = "แก้ไขจำนวนคูปอง",
                    Content = contentPanel,
                    PrimaryButtonText = "ตกลง",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // Update quantity
                    selectedItem.Quantity = (int)quantityBox.Value;
                    UpdateTotalPrice();

                    // Force UI update
                    var index = _selectedItems.IndexOf(selectedItem);
                    if (index >= 0)
                    {
                        _selectedItems.RemoveAt(index);
                        _selectedItems.Insert(index, selectedItem);
                    }
                }
            }
        }

        private async void DeleteItemButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is ReceiptItem selectedItem)
            {
                var dialog = new ContentDialog
                {
                    Title = "ยืนยันการลบ",
                    Content = $"คุณต้องการลบ {selectedItem.Coupon.Name} จำนวน {selectedItem.Quantity} ใบ ใช่หรือไม่?",
                    PrimaryButtonText = "ลบ",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    _selectedItems.Remove(selectedItem);
                    UpdateTotalPrice();
                }
            }
        }

        // Modified SaveReceiptButton_Click to include payment method validation
        private async void SaveReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItems.Count == 0)
            {
                await ShowErrorDialog("ไม่มีรายการที่เลือก กรุณาเลือกคูปองอย่างน้อย 1 รายการ");
                return;
            }

            // เช็คว่ามีการเลือก SalesPerson หรือไม่
            var selectedSalesPerson = SalesPersonComboBox.SelectedItem as SalesPerson;
            if (selectedSalesPerson == null)
            {
                await ShowErrorDialog("กรุณาเลือกเซลล์ก่อนบันทึกใบเสร็จ");
                return;
            }

            // เช็คว่ามีการเลือกวิธีการชำระเงินหรือไม่
            var selectedPaymentMethod = GetSelectedPaymentMethod();
            if (selectedPaymentMethod == null)
            {
                PaymentMethodErrorText.Visibility = Visibility.Visible;
                await ShowErrorDialog("กรุณาเลือกวิธีการชำระเงินก่อนบันทึกใบเสร็จ");
                return;
            }

            // Create customer information dialog
            var customerPanel = new StackPanel { Spacing = 10 };

            customerPanel.Children.Add(new TextBlock { Text = "ชื่อลูกค้า:" });
            var customerNameBox = new TextBox { PlaceholderText = "กรุณาระบุชื่อลูกค้า" };
            customerPanel.Children.Add(customerNameBox);

            customerPanel.Children.Add(new TextBlock { Text = "เบอร์โทรศัพท์:" });
            var phoneNumberBox = new TextBox { PlaceholderText = "กรุณาระบุเบอร์โทรศัพท์" };
            customerPanel.Children.Add(phoneNumberBox);

            var dialog = new ContentDialog
            {
                Title = "ข้อมูลลูกค้า",
                Content = customerPanel,
                PrimaryButtonText = "บันทึก",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string customerName = customerNameBox.Text.Trim();
                string phoneNumber = phoneNumberBox.Text.Trim();

                // Validate input
                if (string.IsNullOrEmpty(customerName) || string.IsNullOrEmpty(phoneNumber))
                {
                    await ShowErrorDialog("กรุณากรอกข้อมูลลูกค้าให้ครบถ้วน");
                    return;
                }

                try
                {
                    // ใช้ Service ใหม่แทน AppSettings
                    string receiptCode = await ReceiptNumberService.GenerateNextReceiptCodeAsync();

                    // Create and save receipt with customer information, receipt code and payment method
                    var receipt = new ReceiptModel
                    {
                        ReceiptCode = receiptCode,
                        ReceiptDate = DateTime.Now,
                        CustomerName = customerName,
                        CustomerPhoneNumber = phoneNumber,
                        TotalAmount = _selectedItems.Sum(item => item.TotalPrice),
                        SalesPersonId = selectedSalesPerson.ID,
                        PaymentMethodId = selectedPaymentMethod.Id // เพิ่มวิธีการชำระเงิน
                    };

                    _context.Receipts.Add(receipt);

                    // Save first to get the ID
                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception dbEx)
                    {
                        // หากไม่สามารถบันทึกได้ ให้คืนหมายเลขใบเสร็จ
                        await ReceiptNumberService.RecycleReceiptCodeAsync(receiptCode, "Database save failed");

                        string errorDetails = $"Error saving receipt: {dbEx.Message}";
                        if (dbEx.InnerException != null)
                        {
                            errorDetails += $"\n\nInner exception: {dbEx.InnerException.Message}";
                        }
                        await ShowErrorDialog(errorDetails);
                        return;
                    }

                    // Save receipt items
                    foreach (var item in _selectedItems)
                    {
                        var receiptItem = new DatabaseReceiptItem
                        {
                            ReceiptId = receipt.ReceiptID,
                            CouponId = item.Coupon.Id,
                            Quantity = item.Quantity,
                            UnitPrice = item.Coupon.Price,
                            TotalPrice = item.TotalPrice
                        };

                        _context.ReceiptItems.Add(receiptItem);
                    }

                    try
                    {
                        await _context.SaveChangesAsync();

                        // ใช้ Service แทนการแสดง popup และ navigate
                        await ShowPrintConfirmationDialog(receipt.ReceiptID, receiptCode, selectedPaymentMethod.Name);

                        // เคลียร์รายการและรีเซ็ตวิธีการชำระเงินหลังจากบันทึกเรียบร้อยแล้ว
                        _selectedItems.Clear();
                        ClearPaymentMethodSelection();
                        UpdateTotalPrice();
                    }
                    catch (Exception dbEx)
                    {
                        string errorDetails = $"Error saving receipt items: {dbEx.Message}";
                        if (dbEx.InnerException != null)
                        {
                            errorDetails += $"\n\nInner exception: {dbEx.InnerException.Message}";
                        }
                        await ShowErrorDialog(errorDetails);
                    }
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog($"เกิดข้อผิดพลาด: {ex.Message}");
                }
            }
        }

        private void ClearPaymentMethodSelection()
        {
            foreach (var checkBox in _paymentCheckBoxes)
            {
                checkBox.Unchecked -= PaymentMethodCheckBox_Unchecked;
                checkBox.IsChecked = false;
                checkBox.Unchecked += PaymentMethodCheckBox_Unchecked;
            }
            PaymentMethodErrorText.Visibility = Visibility.Collapsed;
        }

        private void UpdateTotalPrice()
        {
            decimal total = _selectedItems.Sum(item => item.TotalPrice);
            TotalPriceTextBlock.Text = total.ToString();
        }

        private async Task ShowErrorDialog(string message)
        {
            var errorDialog = new ContentDialog
            {
                Title = "แจ้งเตือน",
                Content = message,
                CloseButtonText = "ตกลง",
                XamlRoot = this.XamlRoot
            };

            await errorDialog.ShowAsync();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(MainPage));
        }

        // เพิ่ม method สำหรับแสดง popup ยืนยันการพิมพ์ใหม่
        private async Task ShowPrintConfirmationDialog(int receiptId, string receiptCode, string paymentMethodName)
        {
            // สร้าง popup ที่มีปุ่มพิมพ์และยกเลิก
            var confirmDialog = new ContentDialog
            {
                Title = "บันทึกใบเสร็จสำเร็จ",
                Content = $"ใบเสร็จเลขที่ {receiptCode} ถูกบันทึกเรียบร้อยแล้ว\nวิธีการชำระเงิน: {paymentMethodName}\n\nต้องการพิมพ์ใบเสร็จหรือไม่?",
                PrimaryButtonText = "พิมพ์",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // กดปุ่มพิมพ์ - ใช้ Service พิมพ์โดยไม่เปิดหน้า Preview
                bool printSuccess = await ReceiptPrintService.PrintReceiptAsync(receiptId, this.XamlRoot);

                if (printSuccess)
                {
                    Debug.WriteLine("เรียกใช้งานการพิมพ์สำเร็จ");
                }
            }
            else
            {
                // กดปุ่มยกเลิก - ใช้ Service จัดการการยกเลิก (เหมือนปุ่มปิดใน ReceiptPrintPreview)
                await ReceiptPrintService.HandleReceiptCancellationAsync(receiptId, this.XamlRoot);
            }
        }

        // เพิ่มการ dispose timer ใน destructor หรือ method ที่เหมาะสม
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _searchTimer?.Dispose();
            base.OnNavigatedFrom(e);
        }
    }
}