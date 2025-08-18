using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

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

        public Receipt()
        {
            InitializeComponent();
            CouponsListView.ItemsSource = _coupons;
            SelectedItemsListView.ItemsSource = _selectedItems;
            this.Loaded += Receipt_Loaded;
        }

        private async void Receipt_Loaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe so it only runs once
            this.Loaded -= Receipt_Loaded;

            try
            {
                await LoadAllCoupons();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Error during load: {ex.Message}");
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

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string nameSearch = NameSearchTextBox.Text.Trim().ToLower();
            string codeSearch = CodeSearchTextBox.Text.Trim().ToLower();

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

            var coupons = await query.ToListAsync();

            _coupons.Clear();
            foreach (var coupon in coupons)
            {
                _coupons.Add(coupon);
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

        private async void SaveReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItems.Count == 0)
            {
                await ShowErrorDialog("ไม่มีรายการที่เลือก กรุณาเลือกคูปองอย่างน้อย 1 รายการ");
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
                    // Generate receipt code first
                    var settings = await AppSettings.GetSettingsAsync();
                    string receiptCode = settings.GetNextReceiptCode();
                    
                    // Create and save receipt with customer information and receipt code
                    var receipt = new ReceiptModel
                    {
                        ReceiptCode = receiptCode,
                        ReceiptDate = DateTime.Now,
                        CustomerName = customerName,
                        CustomerPhoneNumber = phoneNumber,
                        TotalAmount = _selectedItems.Sum(item => item.TotalPrice)
                    };
                    
                    _context.Receipts.Add(receipt);

                    // Save first to get the ID
                    try
                    {
                        await _context.SaveChangesAsync();
                        
                        // Save settings only after successful save
                        await AppSettings.SaveSettingsAsync(settings);
                    }
                    catch (Exception dbEx)
                    {
                        string errorDetails = $"Error saving receipt: {dbEx.Message}";
                        if (dbEx.InnerException != null)
                        {
                            errorDetails += $"\n\nInner exception: {dbEx.InnerException.Message}";
                        }
                        await ShowErrorDialog(errorDetails);
                        return;
                    }

                    // List to store the receipt items
                    var receiptItems = new List<DatabaseReceiptItem>();
                    
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
                        receiptItems.Add(receiptItem);
                    }
                    
                    try
                    {
                        await _context.SaveChangesAsync();
                        
                        // นำทางไปยังหน้า ReceiptPrintPreview พร้อมส่งข้อมูลใบเสร็จ
                        Frame.Navigate(typeof(ReceiptPrintPreview), receipt.ReceiptID);
                        
                        // เคลียร์รายการที่เลือกหลังจากบันทึกเรียบร้อยแล้ว
                        _selectedItems.Clear();
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
    }
}
