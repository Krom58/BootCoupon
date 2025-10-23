using BootCoupon.Services;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using CouponManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media; // added to access VisualTreeHelper and ScrollViewer
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Printing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Printing;
using Windows.Graphics.Printing.OptionDetails;
using WinRT.Interop;

namespace BootCoupon
{
    // ย้าย Converter classes ออกมาข้างนอกคลาส Receipt
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isExhausted = (bool)value;
            return isExhausted ?
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red) :
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return !(bool)value;
        }
    }

    // New converter to change Edit button label depending on IsLimited
    public class BoolToEditLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var isLimited = value as bool? ?? false;
            return isLimited ? "เลือกหมายเลข" : "แก้ไข";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class ReceiptItem : INotifyPropertyChanged
    {
        private CouponDefinition _couponDefinition = null!;
        private int _quantity;
        private List<int> _selectedGeneratedIds = new();

        public CouponDefinition CouponDefinition
        {
            get => _couponDefinition;
            set
            {
                if (!EqualityComparer<CouponDefinition>.Default.Equals(_couponDefinition, value))
                {
                    _couponDefinition = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPrice));
                    OnPropertyChanged(nameof(IsLimited));
                }
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPrice));
                }
            }
        }

        // Store selected GeneratedCoupon IDs when user picks specific codes
        public List<int> SelectedGeneratedIds
        {
            get => _selectedGeneratedIds;
            set
            {
                _selectedGeneratedIds = value ?? new List<int>();
                OnPropertyChanged();
            }
        }

        // Expose whether this item is for a limited coupon (has generated codes)
        public bool IsLimited => CouponDefinition?.IsLimited ?? false;

        public decimal TotalPrice => CouponDefinition.Price * Quantity;

        public string SelectedCodesPreview { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // เพิ่ม class สำหรับแสดงข้อมูลที่รวมจำนวนคูปองที่เหลือ
    public class CouponDefinitionDisplay : INotifyPropertyChanged
    {
        private CouponDefinition _couponDefinition = null!;
        private int _totalGenerated;
        private int _totalUsed;

        public CouponDefinition CouponDefinition
        {
            get => _couponDefinition;
            set
            {
                if (!EqualityComparer<CouponDefinition>.Default.Equals(_couponDefinition, value))
                {
                    _couponDefinition = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvailableCount));
                    OnPropertyChanged(nameof(IsExhausted));
                    OnPropertyChanged(nameof(AvailabilityText));

                    // notify passthrough properties
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(Code));
                    OnPropertyChanged(nameof(TypeDisplayText));
                    OnPropertyChanged(nameof(Price));
                }
            }
        }

        public int TotalGenerated
        {
            get => _totalGenerated;
            set
            {
                if (_totalGenerated != value)
                {
                    _totalGenerated = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvailableCount));
                    OnPropertyChanged(nameof(IsExhausted));
                    OnPropertyChanged(nameof(AvailabilityText));
                }
            }
        }

        public int TotalUsed
        {
            get => _totalUsed;
            set
            {
                if (_totalUsed != value)
                {
                    _totalUsed = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvailableCount));
                    OnPropertyChanged(nameof(IsExhausted));
                    OnPropertyChanged(nameof(AvailabilityText));
                }
            }
        }

        // ถ้าเป็นคูปองไม่จำกัด ให้ถือว่า Available ไม่จำกัดและไม่หมด
        public int AvailableCount
        {
            get
            {
                if (CouponDefinition == null) return 0;
                if (!CouponDefinition.IsLimited)
                {
                    // ใช้เลขมาก ๆ เพื่อให้ UI เลือกได้สะดวก
                    return 1000000;
                }

                var v = TotalGenerated - TotalUsed;
                return v < 0 ? 0 : v;
            }
        }

        public bool IsExhausted => (CouponDefinition?.IsLimited ?? false) && AvailableCount <= 0;

        // สำหรับคูปองไม่จำกัด จะไม่แสดงข้อความจำนวนคงเหลือ
        public string AvailabilityText => (CouponDefinition?.IsLimited ?? false) ? (IsExhausted ? "หมด" : $"เหลือ {AvailableCount:N0} ใบ") : string.Empty;

        // Passthrough properties to satisfy x:Bind OneWay notification requirement
        public string Name => CouponDefinition?.Name ?? string.Empty;
        public string Code => CouponDefinition?.Code ?? string.Empty;
        public string TypeDisplayText => CouponDefinition?.TypeDisplayText ?? string.Empty;
        public decimal Price => CouponDefinition?.Price ?? 0m;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed partial class Receipt : Page
    {
        private readonly CouponContext _context = new CouponContext();
        private readonly ObservableCollection<CouponDefinitionDisplay> _couponDefinitionDisplays = new();
        private readonly ObservableCollection<ReceiptItem> _selectedItems = new();
        private readonly ObservableCollection<SalesPerson> _salesPersons = new();
        private readonly string _reservationSessionId = Guid.NewGuid().ToString();
        private readonly ReservationService _reservationService;
        private readonly List<PaymentMethod> _paymentMethods = new();
        private readonly List<CheckBox> _paymentCheckBoxes = new();

        // เพิ่มตัวแปรสำหรับ debounce
        private System.Threading.Timer? _searchTimer;
        private readonly object _searchLock = new object();

        // เก็บค่า VerticalOffset ของ ListView เพื่อคืนสถานะหลังรีเฟรช
        private double _couponListVerticalOffset = 0;

        public Receipt()
        {
            InitializeComponent();
            CouponDefinitionsListView.ItemsSource = _couponDefinitionDisplays;
            SelectedItemsListView.ItemsSource = _selectedItems;
            SalesPersonComboBox.ItemsSource = _salesPersons;
            _reservationService = new ReservationService(_context);
            this.Loaded += Receipt_Loaded;
        }

        private async void Receipt_Loaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe so it only runs once
            this.Loaded -= Receipt_Loaded;

            try
            {
                await LoadAllCouponDefinitions();
                await LoadSalesPersons();
                await LoadCouponTypes(); // เพิ่มการเรียกใช้ LoadCouponTypes กลับมา
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
                        new PaymentMethod { Name = "เครดิตการ์ด", IsActive = true }, // แก้ไขการพิมพ์ผิด
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

            // เพิ่มตัวเลือก placeholder/ทั้งหมด เป็นรายการแรก เพื่อให้ ComboBox มีค่าเริ่มต้น
            _salesPersons.Add(new SalesPerson { ID = 0, Name = "เลือกเซลล์", Branch = string.Empty, Telephone = string.Empty });

            foreach (var sp in salesPersons)
            {
                _salesPersons.Add(sp);
            }

            // ตั้งค่า default selection เป็น placeholder (index 0)
            try
            {
                if (SalesPersonComboBox != null && SalesPersonComboBox.Items.Count > 0)
                {
                    SalesPersonComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set default SalesPerson selection: {ex.Message}");
            }

            Debug.WriteLine($"โหลด SalesPerson สำเร็จ: {_salesPersons.Count} รายการ (รวม placeholder)");
        }

        private async Task LoadAllCouponDefinitions()
        {
            await _context.Database.EnsureCreatedAsync();

            // โหลด CouponDefinitions พร้อมกับข้อมูล GeneratedCoupons
            var couponDefinitions = await _context.CouponDefinitions
                .Include(cd => cd.CouponType)
                .Include(cd => cd.GeneratedCoupons)
                .Where(cd => cd.IsActive && cd.ValidTo >= DateTime.Now)
                .ToListAsync();

            _couponDefinitionDisplays.Clear();
            foreach (var definition in couponDefinitions)
            {
                var totalGenerated = definition.GeneratedCoupons?.Count ?? 0;
                var totalUsed = definition.GeneratedCoupons?.Count(gc => gc.IsUsed) ?? 0;
                
                // เพิ่มเฉพาะคูปองที่ยังมีคงเหลือ หรือที่ยังไม่เคยสร้างเลย
                var availableCount = totalGenerated - totalUsed;
                if (totalGenerated == 0 || availableCount > 0)
                {
                    var display = new CouponDefinitionDisplay
                    {
                        CouponDefinition = definition,
                        TotalGenerated = totalGenerated,
                        TotalUsed = totalUsed
                    };
                    _couponDefinitionDisplays.Add(display);
                }
            }
        }

        private async Task LoadCouponTypes()
        {
            await _context.Database.EnsureCreatedAsync();

            var couponTypes = await _context.CouponTypes
                .OrderBy(ct => ct.Name)
                .ToListAsync();

            // Clear existing items
            CouponTypeComboBox.Items.Clear();
            
            // Add "ทั้งหมด" option with Tag "ALL"
            var allItem = new ComboBoxItem { Content = "ทั้งหมด", Tag = "ALL", IsSelected = true };
            CouponTypeComboBox.Items.Add(allItem);

            // Add coupon types from database using Id as Tag
            foreach (var type in couponTypes)
            {
                var item = new ComboBoxItem { Content = type.Name, Tag = type.Id.ToString() };
                CouponTypeComboBox.Items.Add(item);
            }

            // Set default selection to "ทั้งหมด"
            try
            {
                if (CouponTypeComboBox.Items.Count > 0)
                {
                    CouponTypeComboBox.SelectedIndex = 0; // เลือก "ทั้งหมด" (index 0)
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set default CouponType selection: {ex.Message}");
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

        // Method สำหรับค้นหาจริง - แก้ไขให้ใช้ CouponTypeId แบบเดียวกับ CouponDefinitionPage
        private async Task PerformSearch()
        {
            try
            {
                // เก็บค่า offset ปัจจุบันของ ListView เพื่อคืนค่าเมื่อรีเฟรช
                try
                {
                    var svBefore = FindScrollViewer(CouponDefinitionsListView);
                    _couponListVerticalOffset = svBefore?.VerticalOffset ?? 0;
                }
                catch { /* ignore errors in getting scrollviewer */ }

                string nameSearch = NameSearchTextBox.Text.Trim().ToLower();
                string codeSearch = CodeSearchTextBox.Text.Trim().ToLower();

                // ใช้แบบเดียวกับ CouponDefinitionPage - ดึง Tag จาก ComboBoxItem
                var typeFilter = GetSelectedTag(CouponTypeComboBox);

                await _context.Database.EnsureCreatedAsync();

                var query = _context.CouponDefinitions
                    .Include(cd => cd.CouponType)
                    .Include(cd => cd.GeneratedCoupons)
                    .Where(cd => cd.IsActive && cd.ValidTo >= DateTime.Now)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(nameSearch))
                {
                    query = query.Where(cd => cd.Name.ToLower().Contains(nameSearch));
                }

                if (!string.IsNullOrEmpty(codeSearch))
                {
                    query = query.Where(cd => cd.Code.ToLower().Contains(codeSearch));
                }

                // Filter by CouponTypeId - ใช้ Tag จาก ComboBoxItem
                if (typeFilter != "ALL" && int.TryParse(typeFilter, out int typeId))
                {
                    query = query.Where(cd => cd.CouponTypeId == typeId);
                }

                var couponDefinitions = await query.ToListAsync();

                _couponDefinitionDisplays.Clear();
                foreach (var definition in couponDefinitions)
                {
                    var totalGenerated = definition.GeneratedCoupons?.Count ?? 0;
                    var totalUsed = definition.GeneratedCoupons?.Count(gc => gc.IsUsed) ?? 0;
                    var availableCount = totalGenerated - totalUsed;

                    // เพิ่มเฉพาะคูปองที่ยังมีคงเหลือ หรือที่ยังไม่เคยสร้างเลย
                    if (totalGenerated == 0 || availableCount > 0)
                    {
                        var display = new CouponDefinitionDisplay
                        {
                            CouponDefinition = definition,
                            TotalGenerated = totalGenerated,
                            TotalUsed = totalUsed
                        };
                        _couponDefinitionDisplays.Add(display);
                    }
                }

                // คืนค่าสถานะ Scroll position หลังจากรีเฟรช
                try
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        var svAfter = FindScrollViewer(CouponDefinitionsListView);
                        if (svAfter != null)
                        {
                            svAfter.ChangeView(null, _couponListVerticalOffset, null, disableAnimation: true);
                        }
                    });
                }
                catch { /* ignore restore errors */ }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการค้นหา: {ex.Message}");
                // ไม่แสดง error dialog เพื่อไม่ให้รบกวนผู้ใช้ขณะพิมพ์
            }
        }

        private string GetSelectedTag(ComboBox comboBox)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
        }

        // Helper: หา display จาก CouponDefinition.Id
        private CouponDefinitionDisplay? GetDisplayByDefinitionId(int definitionId)
        {
            return _couponDefinitionDisplays.FirstOrDefault(d => d.CouponDefinition?.Id == definitionId);
        }

        private async void SelectCouponButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is CouponDefinitionDisplay selectedDisplay)
            {
                var selectedDefinition = selectedDisplay.CouponDefinition;

                // ถ้าเป็นคูปองจำกัด ตรวจสอบว่าหมดหรือไม่
                if (selectedDefinition.IsLimited && selectedDisplay.IsExhausted)
                {
                    await ShowErrorDialog($"คูปอง '{selectedDefinition.Name}' หมดแล้ว ไม่สามารถเลือกได้");
                    return;
                }

                if (selectedDefinition.IsLimited)
                {
                    // Use centralized helper to pick specific generated codes
                    var selectedIds = await ShowPickGeneratedCodesDialogAsync(selectedDefinition, null);
                    if (selectedIds == null || !selectedIds.Any()) return;

                    // NOTE: single-user mode - do NOT call ReservationService for specific IDs.
                    // We'll allocate/validate at Save time. Just update UI state.

                    // If existing selectedItem exists, merge
                    var existingItem = _selectedItems.FirstOrDefault(it => it.CouponDefinition.Id == selectedDefinition.Id);
                    if (existingItem != null)
                    {
                        // merge avoiding duplicates
                        var beforeCount = existingItem.SelectedGeneratedIds?.Count ?? existingItem.Quantity;
                        var merged = (existingItem.SelectedGeneratedIds ?? new List<int>()).Union(selectedIds ?? Enumerable.Empty<int>()).Distinct().ToList();
                        existingItem.SelectedGeneratedIds = merged;
                        existingItem.Quantity = merged.Count;

                        var display2 = GetDisplayByDefinitionId(selectedDefinition.Id);
                        if (display2 != null)
                        {
                            display2.TotalUsed += (merged.Count - beforeCount);
                        }
                    }
                    else
                    {
                        var receiptItem = new ReceiptItem
                        {
                            CouponDefinition = selectedDefinition,
                            Quantity = selectedIds.Distinct().Count(),
                            SelectedGeneratedIds = selectedIds.Distinct().ToList()
                        };

                        // preview selected codes for UI (comma separated limited length)
                        receiptItem.SelectedCodesPreview = string.Join(", ", await _context.GeneratedCoupons.Where(g => receiptItem.SelectedGeneratedIds.Contains(g.Id)).Select(g => g.GeneratedCode).Take(5).ToListAsync());

                        _selectedItems.Add(receiptItem);

                        var display2 = GetDisplayByDefinitionId(selectedDefinition.Id);
                        if (display2 != null)
                        {
                            display2.TotalUsed += receiptItem.Quantity;
                        }
                    }

                    UpdateTotalPrice();
                    return;
                }

                // Non-limited flow (existing behavior)
                var quantityBox = new NumberBox
                {
                    Value = 1,
                    Minimum = 1,
                    Maximum = selectedDefinition.IsLimited ? selectedDisplay.AvailableCount : int.MaxValue, // สำหรับคูปองไม่จำกัด ให้อนุญาตจำนวนมาก
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var priceTextBlock = new TextBlock
                {
                    Text = $"ราคา: {selectedDefinition.Price} บาท/ใบ",
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var availableTextBlock = new TextBlock
                {
                    Text = selectedDefinition.IsLimited ? $"คงเหลือ: {selectedDisplay.AvailableCount:N0} ใบ" : string.Empty,
                    Margin = new Thickness(0, 5, 0, 10),
                    Foreground = selectedDefinition.IsLimited && selectedDisplay.AvailableCount > 10 ? 
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green) : 
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                };

                var totalPriceTextBlock = new TextBlock
                {
                    Text = $"รวมเป็นเงิน: {selectedDefinition.Price} บาท",
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                quantityBox.ValueChanged += (s, args) =>
                {
                    if (quantityBox.Value > 0)
                    {
                        totalPriceTextBlock.Text = $"รวมเป็นเงิน: {selectedDefinition.Price * (decimal)quantityBox.Value} บาท";
                    }
                };

                var contentPanel = new StackPanel();
                contentPanel.Children.Add(new TextBlock { Text = $"คูปอง: {selectedDefinition.Name}" });
                contentPanel.Children.Add(priceTextBlock);
                contentPanel.Children.Add(availableTextBlock);
                contentPanel.Children.Add(new TextBlock { Text = "กรุณาระบุจำนวน:" });
                contentPanel.Children.Add(quantityBox);
                contentPanel.Children.Add(totalPriceTextBlock);

                var dialog2 = new ContentDialog
                {
                    Title = "เลือกจำนวนคูปอง",
                    Content = contentPanel,
                    PrimaryButtonText = "ตกลง",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result2 = await dialog2.ShowAsync();
                if (result2 == ContentDialogResult.Primary)
                {
                    // Validate quantity input
                    if (double.IsNaN(quantityBox.Value) || quantityBox.Value <= 0)
                    {
                        await ShowErrorDialog("กรุณาระบุจำนวนคูปองที่ถูกต้อง");
                        return;
                    }

                    var quantity = (int)quantityBox.Value;

                    // ตรวจสอบอีกครั้งก่อนเพิ่มในรายการ เฉพาะคูปองที่จำกัด
                    if (selectedDefinition.IsLimited && quantity > selectedDisplay.AvailableCount)
                    {
                        await ShowErrorDialog($"จำนวนที่เลือก ({quantity}) เกินกว่าที่มีคงเหลือ ({selectedDisplay.AvailableCount})");
                        return;
                    }

                    // Try to reserve in DB for this session before updating UI (เฉพาะคูปองจำกัด)
                    if (selectedDefinition.IsLimited)
                    {
                        var reserved = await _reservationService.TryReserveAsync(selectedDefinition.Id, _reservationSessionId, quantity, TimeSpan.FromMinutes(10));
                        if (!reserved)
                        {
                            await ShowErrorDialog("ไม่สามารถสำรองคูปองได้ (จำนวนไม่พอ)");
                            return;
                        }
                    }
                    
                     // ถ้ามีรายการเดียวกันอยู่แล้ว ให้เพิ่มจำนวนแทนการสร้างรายการใหม่
                     var existingItem2 = _selectedItems.FirstOrDefault(it => it.CouponDefinition.Id == selectedDefinition.Id);
                     if (existingItem2 != null)
                     {
                        existingItem2.Quantity += quantity;
                         // ปรับ TotalUsed ใน display เพื่อสะท้อนการสำรองคูปอง (เฉพาะคูปองจำกัด)
                         var display2 = GetDisplayByDefinitionId(selectedDefinition.Id);
                         if (display2 != null && selectedDefinition.IsLimited)
                         {
                             display2.TotalUsed += quantity;
                         }
                     }
                     else
                     {
                         // เพิ่มลงในรายการที่เลือก
                         var receiptItem = new ReceiptItem
                         {
                             CouponDefinition = selectedDefinition,
                             Quantity = quantity
                         };

                         _selectedItems.Add(receiptItem);

                         // ปรับ TotalUsed ใน display เพื่อสะท้อนการสำรองคูปอง (เฉพาะคูปองจำกัด)
                         var display2 = GetDisplayByDefinitionId(selectedDefinition.Id);
                         if (display2 != null && selectedDefinition.IsLimited)
                         {
                             display2.TotalUsed += quantity;
                         }
                     }

                     UpdateTotalPrice();

                     // ไม่รีเฟรชจาก DB เพื่อไม่ให้สูญเสียการสำรองที่ยังไม่บันทึก
                 }
             }
         }

        // New helper: show dialog to pick generated codes (searchable checklist). Returns selected generated coupon IDs or null if cancelled.
        private async Task<List<int>?> ShowPickGeneratedCodesDialogAsync(CouponDefinition selectedDefinition, ReceiptItem? existingItem)
        {
            await _context.Database.EnsureCreatedAsync();

            var initialSelectedIds = existingItem?.SelectedGeneratedIds ?? new List<int>();

            // Include currently selected ids even if marked IsUsed, so user can manage them
            var availableCodes = await _context.GeneratedCoupons
                .Where(g => g.CouponDefinitionId == selectedDefinition.Id && (!g.IsUsed || initialSelectedIds.Contains(g.Id)))
                .OrderBy(g => g.Id)
                .Take(2000)
                .ToListAsync();

            var stack = new StackPanel { Spacing = 6 };

            stack.Children.Add(new TextBlock { Text = $"เลือกหมายเลขคูปองสำหรับ '{selectedDefinition.Name}'", TextWrapping = TextWrapping.Wrap });

            // Search box to filter the checklist
            var searchBox = new TextBox { PlaceholderText = "ค้นหารหัส (พิมพ์แล้วรายการจะกรองอัตโนมัติ)", Margin = new Thickness(0, 4, 0, 0) };
            stack.Children.Add(searchBox);

            var infoText = new TextBlock { Text = $"หมายเลขที่แสดง: 0 / {availableCodes.Count}", Margin = new Thickness(0, 6, 0, 0) };
            stack.Children.Add(infoText);

            var scroll = new ScrollViewer { Height = 300 };
            var resultsPanel = new StackPanel { Spacing = 2 };
            scroll.Content = resultsPanel;
            stack.Children.Add(scroll);

            // Dictionary to keep track of checkboxes so we can read selections later
            var checkboxMap = new Dictionary<int, CheckBox>();

            // Function to populate resultsPanel based on filter
            void PopulateResults(string? filter)
            {
                resultsPanel.Children.Clear();
                checkboxMap.Clear();

                var query = availableCodes.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    query = query.Where(g => g.GeneratedCode?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                // Limit displayed to avoid UI freeze
                var displayed = query.Take(1000).ToList();

                foreach (var g in displayed)
                {
                    var cb = new CheckBox { Content = g.GeneratedCode, Tag = g.Id, Margin = new Thickness(0, 2, 0, 2) };
                    if (initialSelectedIds.Contains(g.Id)) cb.IsChecked = true;
                    checkboxMap[g.Id] = cb;
                    resultsPanel.Children.Add(cb);
                }

                infoText.Text = $"หมายเลขที่แสดง: {displayed.Count} / {availableCodes.Count}";
            }

            // Initial populate
            PopulateResults(null);

            // Live filter
            searchBox.TextChanged += (s, e) =>
            {
                var text = searchBox.Text?.Trim();
                PopulateResults(text);
            };

            var dialog = new ContentDialog
            {
                Title = "เลือกหมายเลขคูปอง",
                Content = stack,
                PrimaryButtonText = "ตกลง",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var selectedIds = checkboxMap.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();

                // If nothing checked but user typed something in search, try to match those tokens
                if (!selectedIds.Any() && !string.IsNullOrWhiteSpace(searchBox.Text))
                {
                    var tokens = searchBox.Text.Split(new[] { '\n', '\r', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(t => t.Trim())
                                    .Where(t => !string.IsNullOrEmpty(t))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList();

                    if (tokens.Count > 0)
                    {
                        var matched = availableCodes.Where(g => tokens.Any(tok => string.Equals(g.GeneratedCode, tok, StringComparison.OrdinalIgnoreCase))).ToList();
                        selectedIds = matched.Select(m => m.Id).ToList();
                    }
                }

                return selectedIds;
            }

            return null;
        }

        // wrapper to call TryReserve (separated for readability)
        private async Task<bool> _reservation_service_try_reserve_wrapper(int couponDefinitionId, int quantity)
        {
            return await _reservationService.TryReserveAsync(couponDefinitionId, _reservationSessionId, quantity, TimeSpan.FromMinutes(10));
        }

        private async void DeleteItemButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is ReceiptItem selectedItem)
            {
                var dialog = new ContentDialog
                {
                    Title = "ยืนยันการลบ",
                    Content = $"คุณต้องการลบ {selectedItem.CouponDefinition.Name} จำนวน {selectedItem.Quantity} ใบ ใช่หรือไม่?",
                    PrimaryButtonText = "ลบ",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // release reservation in DB for this session (เฉพาะคูปองจำกัด and when not specific ids)
                    if (selectedItem.CouponDefinition.IsLimited && (selectedItem.SelectedGeneratedIds == null || !selectedItem.SelectedGeneratedIds.Any()))
                    {
                        await _reservationService.ReleaseReservationAsync(selectedItem.CouponDefinition.Id, _reservationSessionId, selectedItem.Quantity);
                    }

                    var display = GetDisplayByDefinitionId(selectedItem.CouponDefinition.Id);
                    if (display != null)
                    {
                        display.TotalUsed -= selectedItem.Quantity;
                        if (display.TotalUsed < 0) display.TotalUsed = 0;
                    }

                    _selectedItems.Remove(selectedItem);
                    UpdateTotalPrice();

                    // ไม่รีเฟรชจาก DB เพื่อไม่ให้สูญเสียการสำรองที่ยังไม่บันทึก
                }
            }
        }

        // แทนที่ SaveReceiptButton_Click ให้ใช้ CouponDefinition แทน Coupon
        private async void SaveReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItems.Count == 0)
            {
                await ShowErrorDialog("ไม่มีรายการที่เลือก กรุณาเลือกคูปองอย่างน้อย 1 รายการ");
                return;
            }

            // เช็คว่ามีการเลือก SalesPerson หรือไม่ (ตรวจสอบ placeholder ID = 0 ด้วย)
            var selectedSalesPerson = SalesPersonComboBox.SelectedItem as SalesPerson;
            if (selectedSalesPerson == null || selectedSalesPerson.ID == 0)
            {
                await ShowErrorDialog("กรุณาเลือกเซลล์ก่อนบันทึกใบเสร็จ");
                return;
            }

            // เช็คว่ามีการเลือกวิธีการชำระเงินหรือไม่
            var selectedPaymentMethod = GetSelectedPaymentMethod();
            if (selectedPaymentMethod == null)
            {
                // แสดงข้อความเตือนใน UI
                PaymentMethodErrorText.Visibility = Visibility.Visible;
                await ShowErrorDialog("กรุณาเลือกวิธีการชำระเงินก่อนบันทึกใบเสร็จ");
                return;
            }
            // ตรวจสอบความพร้อมของคูปองอีกครั้งก่อนบันทึก
            foreach (var item in _selectedItems)
            {
                var currentDefinition = await _context.CouponDefinitions
                    .Include(cd => cd.GeneratedCoupons)
                    .FirstOrDefaultAsync(cd => cd.Id == item.CouponDefinition.Id);

                if (currentDefinition == null)
                {
                    await ShowErrorDialog($"ไม่พบข้อมูลคูปอง '{item.CouponDefinition.Name}'");
                    return;
                }

                var totalGenerated = currentDefinition.GeneratedCoupons?.Count ?? 0;
                var totalUsed = currentDefinition.GeneratedCoupons?.Count(gc => gc.IsUsed) ?? 0;
                var availableCount = totalGenerated - totalUsed;

                // เฉพาะกรณีคูปองจำกัดเท่านั้นจึงตรวจสอบจำนวนคงเหลือ
                if (currentDefinition.IsLimited && item.Quantity > availableCount)
                {
                    await ShowErrorDialog($"คูปอง '{item.CouponDefinition.Name}' มีคงเหลือ {availableCount} ใบ ไม่เพียงพอสำหรับจำนวนที่เลือก ({item.Quantity} ใบ)");
                    return;
                }
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
                    await ShowErrorDialog("กรุณากรอกข้อมูลลูกค้าให้ครบถ้วน"); // แก้ไขข้อความที่ซ้ำ
                    return;
                }

                using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    // ใช้ Service ใหม่แทน AppSettings
                    string receiptCode = await ReceiptNumberService.GenerateNextReceiptCodeAsync();

                    // Find or create customer (prefer phone match)
                    Customer? customer = null;
                    if (!string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == phoneNumber);
                    }

                    if (customer == null && !string.IsNullOrWhiteSpace(customerName))
                    {
                        customer = await _context.Customers.FirstOrDefaultAsync(c => c.Name == customerName);
                    }

                    if (customer == null)
                    {
                        customer = new Customer
                        {
                            Name = string.IsNullOrWhiteSpace(customerName) ? "ลูกค้าไม่ระบุ" : customerName,
                            Phone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber,
                            CreatedAt = DateTime.Now
                        };
                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync(); // ensure Id is generated
                    }

                    // Create and save receipt with customer information, receipt code and payment method
                    var receipt = new ReceiptModel
                    {
                        ReceiptCode = receiptCode,
                        ReceiptDate = DateTime.Now,
                        CustomerName = customerName,
                        CustomerPhoneNumber = phoneNumber,
                        CustomerId = customer?.Id,
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

                    // Save receipt items - แก้ไขให้ใช้ CouponDefinition.Id
                    var createdReceiptItems = new List<DatabaseReceiptItem>();
                    foreach (var item in _selectedItems)
                    {
                        var receiptItem = new DatabaseReceiptItem
                        {
                            ReceiptId = receipt.ReceiptID,
                            CouponId = item.CouponDefinition.Id, // ใช้ CouponDefinition.Id แทน Coupon.Id
                            Quantity = item.Quantity,
                            UnitPrice = item.CouponDefinition.Price,
                            TotalPrice = item.TotalPrice
                        };

                        _context.ReceiptItems.Add(receiptItem);
                        createdReceiptItems.Add(receiptItem);
                    }

                    // Save receipt items to get ReceiptItemId populated before allocating GeneratedCoupons
                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception dbEx)
                    {
                        await tx.RollbackAsync();
                        await ReceiptNumberService.RecycleReceiptCodeAsync(receiptCode, "Failed to save receipt items");
                        await ShowErrorDialog($"Error saving receipt items: {dbEx.Message}");
                        return;
                    }

                    // Reserve coupons / allocate specific selected codes
                    for (int i = 0; i < _selectedItems.Count; i++)
                    {
                        var item = _selectedItems[i];
                        var receiptItem = createdReceiptItems[i];

                        if (item.CouponDefinition.IsLimited)
                        {
                            if (item.SelectedGeneratedIds != null && item.SelectedGeneratedIds.Any())
                            {
                                // allocate those specific ids
                                var allocate = await _context.GeneratedCoupons
                                    .Where(g => item.SelectedGeneratedIds.Contains(g.Id) && !g.IsUsed)
                                    .ToListAsync();

                                if (allocate.Count < item.Quantity)
                                {
                                    await tx.RollbackAsync();
                                    await ShowErrorDialog("คูปองที่เลือกบางรายการไม่พร้อมใช้งาน");
                                    return;
                                }

                                foreach (var g in allocate)
                                {
                                    g.IsUsed = true;
                                    g.UsedDate = DateTime.Now;
                                    g.ReceiptItemId = receiptItem.ReceiptItemId; // link to receipt item
                                    // denormalize customer for faster reporting
                                    g.CustomerId = receipt.CustomerId;
                                    _context.GeneratedCoupons.Update(g);
                                }
                            }
                            else
                            {
                                // original allocation by selecting first available
                                var allocate = await _context.GeneratedCoupons
                                    .Where(g => g.CouponDefinitionId == item.CouponDefinition.Id && !g.IsUsed)
                                    .OrderBy(g => g.Id)
                                    .Take(item.Quantity)
                                    .ToListAsync();

                                if (allocate.Count < item.Quantity) { await tx.RollbackAsync(); await ShowErrorDialog("คูปองไม่เพียงพอ"); return; }

                                foreach (var g in allocate)
                                {
                                    g.IsUsed = true;
                                    g.UsedDate = DateTime.Now;
                                    g.ReceiptItemId = receiptItem.ReceiptItemId; // link to receipt item
                                    // denormalize customer for faster reporting
                                    g.CustomerId = receipt.CustomerId;
                                    _context.GeneratedCoupons.Update(g);
                                }
                            }
                        }
                    }

                    // Remove reservations made by this session for these coupon definitions
                    try
                    {
                        var reservations = _context.ReservedCoupons.Where(r => r.SessionId == _reservationSessionId);
                        _context.ReservedCoupons.RemoveRange(reservations);
                        await _context.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to clear reservations: {ex.Message}");
                        // not fatal for commit - proceed
                    }
                    
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    // ใช้ Service แทนการแสดง popup และnavigate
                    await ShowPrintConfirmationDialog(receipt.ReceiptID, receiptCode, selectedPaymentMethod.Name);

                    // เคลียร์รายการและรีเซ็ตวิธีการชำระเงินหลังจากบันทึกเรียบร้อยแล้ว
                    _selectedItems.Clear();
                    ClearPaymentMethodSelection();
                    UpdateTotalPrice();

                    // รีเฟรชข้อมูลเพื่อให้แสดงจำนวนที่เหลือใหม่
                    await PerformSearch();
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

        // Helper: หา ScrollViewer ภายใน ListView โดยใช้ VisualTree
        private ScrollViewer? FindScrollViewer(DependencyObject start)
        {
            if (start == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(start); i++)
            {
                var child = VisualTreeHelper.GetChild(start, i);
                if (child is ScrollViewer sv) return sv;
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }

            return null;
        }

        private async void EditItemButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is ReceiptItem selectedItem)
            {
                // If coupon definition is limited (has generated codes), Edit should open the pick dialog
                if (selectedItem.CouponDefinition.IsLimited)
                {
                    var newSelectedIds = await ShowPickGeneratedCodesDialogAsync(selectedItem.CouponDefinition, selectedItem);
                    if (newSelectedIds == null)
                    {
                        return; // cancelled
                    }

                    var oldCount = selectedItem.SelectedGeneratedIds?.Count ?? 0;
                    var uniqueNew = newSelectedIds.Distinct().ToList();
                    var newCount = uniqueNew.Count;
                    var delta = newCount - oldCount;

                    selectedItem.SelectedGeneratedIds = uniqueNew;
                    selectedItem.Quantity = newCount;

                    var remainingCodes = await _context.GeneratedCoupons
                        .Where(g => uniqueNew.Contains(g.Id))
                        .OrderBy(g => g.Id)
                        .Select(g => g.GeneratedCode)
                        .ToListAsync();

                    selectedItem.SelectedCodesPreview = string.Join(", ", remainingCodes.Take(5));

                    var dsp = GetDisplayByDefinitionId(selectedItem.CouponDefinition.Id);
                    if (dsp != null)
                    {
                        dsp.TotalUsed += delta;
                        if (dsp.TotalUsed < 0) dsp.TotalUsed = 0;
                    }

                    var idx = _selectedItems.IndexOf(selectedItem);
                    if (idx >= 0)
                    {
                        _selectedItems.RemoveAt(idx);
                        _selectedItems.Insert(idx, selectedItem);
                    }

                    UpdateTotalPrice();
                    return;
                }

                // For unlimited coupons, edit should allow changing quantity only
                var quantityBox = new NumberBox
                {
                    Value = selectedItem.Quantity > 0 ? selectedItem.Quantity : 1,
                    Minimum = 1,
                    Maximum = int.MaxValue,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var totalPriceTextBlock = new TextBlock
                {
                    Text = $"รวมเป็นเงิน: {selectedItem.CouponDefinition.Price * (decimal)quantityBox.Value} บาท",
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Margin = new Thickness(0, 8, 0, 10)
                };

                quantityBox.ValueChanged += (s, args) =>
                {
                    if (quantityBox.Value > 0)
                    {
                        totalPriceTextBlock.Text = $"รวมเป็นเงิน: {selectedItem.CouponDefinition.Price * (decimal)quantityBox.Value} บาท";
                    }
                };

                var contentPanel = new StackPanel();
                contentPanel.Children.Add(new TextBlock { Text = $"คูปอง: {selectedItem.CouponDefinition.Name}" });
                contentPanel.Children.Add(new TextBlock { Text = $"ราคา: {selectedItem.CouponDefinition.Price} บาท/ใบ", Margin = new Thickness(0,6,0,6) });
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
                    if (double.IsNaN(quantityBox.Value) || quantityBox.Value <= 0)
                    {
                        await ShowErrorDialog("กรุณาระบุจำนวนคูปองที่ถูกต้อง");
                        return;
                    }

                    var oldQuantity = selectedItem.Quantity;
                    var newQuantity = (int)quantityBox.Value;
                    selectedItem.Quantity = newQuantity;

                    // For unlimited coupons there is no reservation or generated-id mapping, only update totals
                    var display = GetDisplayByDefinitionId(selectedItem.CouponDefinition.Id);
                    if (display != null)
                    {
                        display.TotalUsed += (newQuantity - oldQuantity);
                        if (display.TotalUsed < 0) display.TotalUsed = 0;
                    }

                    var index = _selectedItems.IndexOf(selectedItem);
                    if (index >= 0)
                    {
                        _selectedItems.RemoveAt(index);
                        _selectedItems.Insert(index, selectedItem);
                    }

                    UpdateTotalPrice();
                }
            }
        }
    }
}