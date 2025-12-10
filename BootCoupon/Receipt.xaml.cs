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

    // Converter to map bool (IsLimited) -> Visibility (Collapsed when true)
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isLimited = value as bool? ?? false;
            return isLimited ? Visibility.Collapsed : Visibility.Visible;
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
        private bool _isCom = false;

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

        // Property to track if this item contains COM (complimentary) coupons
        public bool IsCOM
        {
            get => _isCom;
            set
            {
                if (_isCom != value)
                {
                    _isCom = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ComBadgeText));
                }
            }
        }

        // Text to display COM badge
        public string ComBadgeText => IsCOM ? "🎁 COM (ตั๋วฟรี)" : string.Empty;

        // Expose whether this item is for a limited coupon (has generated codes)
        public bool IsLimited => CouponDefinition?.IsLimited ?? false;

        // TotalPrice does not include receipt-level discount
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
        // add a field to hold receipt-level discount when creating a receipt
        private decimal _receiptDiscount =0m;

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
            // Use a short-lived context and AsNoTracking to avoid stale tracked entities
            using var ctx = new CouponContext();
            await ctx.Database.EnsureCreatedAsync();

            var couponDefinitions = await ctx.CouponDefinitions
                .AsNoTracking()
                .Include(cd => cd.CouponType)
                .Include(cd => cd.GeneratedCoupons)
                .Where(cd => cd.IsActive && cd.ValidTo >= DateTime.Now)
                .ToListAsync();

            _couponDefinitionDisplays.Clear();
            foreach (var definition in couponDefinitions)
            {
                var totalGenerated = definition.GeneratedCoupons?.Count ?? 0;
                // consider a coupon allocated to a receipt (ReceiptItemId != null) as already sold
                var totalAllocatedOrUsed = definition.GeneratedCoupons?.Count(gc => gc.IsUsed || gc.ReceiptItemId != null) ?? 0;

                var availableCount = totalGenerated - totalAllocatedOrUsed;
                if (totalGenerated == 0 || availableCount > 0)
                {
                    var display = new CouponDefinitionDisplay
                    {
                        CouponDefinition = definition,
                        TotalGenerated = totalGenerated,
                        TotalUsed = totalAllocatedOrUsed
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
                // Save current scroll position
                try
                {
                    var svBefore = FindScrollViewer(CouponDefinitionsListView);
                    _couponListVerticalOffset = svBefore?.VerticalOffset ?? 0;
                }
                catch { }

                string nameSearch = NameSearchTextBox.Text.Trim().ToLower();
                string codeSearch = CodeSearchTextBox.Text.Trim().ToLower();
                var typeFilter = GetSelectedTag(CouponTypeComboBox);

                // Use a new context instance and AsNoTracking to ensure fresh data from DB
                using var ctx = new CouponContext();
                await ctx.Database.EnsureCreatedAsync();

                var query = ctx.CouponDefinitions
                    .AsNoTracking()
                    .Include(cd => cd.CouponType)
                    .Include(cd => cd.GeneratedCoupons)
                    .Where(cd => cd.IsActive && cd.ValidTo >= DateTime.Now)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(nameSearch))
                    query = query.Where(cd => cd.Name.ToLower().Contains(nameSearch));

                if (!string.IsNullOrEmpty(codeSearch))
                    query = query.Where(cd => cd.Code.ToLower().Contains(codeSearch));

                if (typeFilter != "ALL" && int.TryParse(typeFilter, out int typeId))
                    query = query.Where(cd => cd.CouponTypeId == typeId);

                var couponDefinitions = await query.ToListAsync();

                _couponDefinitionDisplays.Clear();
                foreach (var definition in couponDefinitions)
                {
                    var totalGenerated = definition.GeneratedCoupons?.Count ?? 0;
                    var totalAllocatedOrUsed = definition.GeneratedCoupons?.Count(gc => gc.IsUsed || gc.ReceiptItemId != null) ?? 0;
                    var availableCount = totalGenerated - totalAllocatedOrUsed;

                    if (totalGenerated == 0 || availableCount > 0)
                    {
                        var display = new CouponDefinitionDisplay
                        {
                            CouponDefinition = definition,
                            TotalGenerated = totalGenerated,
                            TotalUsed = totalAllocatedOrUsed
                        };
                        _couponDefinitionDisplays.Add(display);
                    }
                }

                // Restore scroll position
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
                catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการค้นหา: {ex.Message}");
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
                    var result = await ShowPickGeneratedCodesDialogAsync(selectedDefinition, null);
                    if (result == null) return;
     
var (selectedIds, isComMode) = result.Value;
if (selectedIds == null || !selectedIds.Any()) return;

       // Work with distinct ids
 var distinctIds = selectedIds.Distinct().ToList();

      // Load generated codes for preview
       var codesMap = await _context.GeneratedCoupons
    .Where(g => distinctIds.Contains(g.Id))
    .ToDictionaryAsync(g => g.Id, g => g.GeneratedCode);

   var display2 = GetDisplayByDefinitionId(selectedDefinition.Id);

     // Add each selected generated id as its own ReceiptItem (Quantity =1),
         // skip any ids that are already present in selected items
        foreach (var gid in distinctIds)
  {
      var alreadySelected = _selectedItems.Any(it => it.SelectedGeneratedIds != null && it.SelectedGeneratedIds.Contains(gid));
    if (alreadySelected) continue;

       var receiptItem = new ReceiptItem
      {
    CouponDefinition = selectedDefinition,
          Quantity =1,
 SelectedGeneratedIds = new List<int> { gid },
        SelectedCodesPreview = codesMap.TryGetValue(gid, out var code) ? code ?? string.Empty : string.Empty,
    IsCOM = isComMode // เก็บสถานะ COM ที่ได้จาก checkbox
        };

      _selectedItems.Add(receiptItem);

if (display2 != null)
  {
   display2.TotalUsed +=1;
  }
      }

  UpdateTotalPrice();
       return;
                }

                // Non-limited flow (existing behavior)
                var quantityBox = new NumberBox
                {
                    Value =1,
                    Minimum =1,
                    Maximum = selectedDefinition.IsLimited ? selectedDisplay.AvailableCount : int.MaxValue, // สำหรับคูปองไม่จำกัด ให้อนุญาตจำนวนมาก
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                    Margin = new Thickness(0,10,0,0)
                };

                var priceTextBlock = new TextBlock
                {
                    Text = $"ราคา: {selectedDefinition.Price} บาท/ใบ",
                    Margin = new Thickness(0,10,0,10)
                };

                var availableTextBlock = new TextBlock
                {
                    Text = selectedDefinition.IsLimited ? $"คงเหลือ: {selectedDisplay.AvailableCount:N0} ใบ" : string.Empty,
                    Margin = new Thickness(0,5,0,10),
                    Foreground = selectedDefinition.IsLimited && selectedDisplay.AvailableCount >10 ? 
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green) : 
                        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                };

                var totalPriceTextBlock = new TextBlock
                {
                    Text = $"รวมเป็นเงิน: {selectedDefinition.Price} บาท",
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Margin = new Thickness(0,0,0,10)
                };

                void RecalcTotal()
                {
                    var q = quantityBox.Value >0 ? (int)quantityBox.Value :0;
                    var total = selectedDefinition.Price * (decimal)q;
                    totalPriceTextBlock.Text = $"รวมเป็นเงิน: {total} บาท";
                }

                quantityBox.ValueChanged += (s, args) => RecalcTotal();

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
                    if (double.IsNaN(quantityBox.Value) || quantityBox.Value <=0)
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
                     var existingItem2 = _selectedItems.FirstOrDefault(it => it.CouponDefinition.Id == selectedDefinition.Id && (it.SelectedGeneratedIds==null || !it.SelectedGeneratedIds.Any()));
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

        // New helper: show dialog to pick generated codes (searchable checklist). Returns selected generated coupon IDs and COM flag or null if cancelled.
        private async Task<(List<int>? selectedIds, bool isCom)?> ShowPickGeneratedCodesDialogAsync(CouponDefinition selectedDefinition, ReceiptItem? existingItem)
      {
    await _context.Database.EnsureCreatedAsync();

 var initialSelectedIds = existingItem?.SelectedGeneratedIds ?? new List<int>();

      // รวบรวม IDs ของหมายเลขที่ถูกเลือกไปแล้วในรายการอื่นๆ (ยกเว้นรายการปัจจุบัน)
        var alreadySelectedInOtherItems = _selectedItems
    .Where(it => it != existingItem && it.SelectedGeneratedIds != null && it.SelectedGeneratedIds.Any())
 .SelectMany(it => it.SelectedGeneratedIds)
     .Distinct()
     .ToList();

            // Include currently selected ids even if marked IsUsed, so user can manage them
 var availableCodes = await _context.GeneratedCoupons
    .Where(g => g.CouponDefinitionId == selectedDefinition.Id && ((g.ReceiptItemId == null && !g.IsUsed) || initialSelectedIds.Contains(g.Id)))
     .OrderBy(g => g.GeneratedCode)
  .Take(2000)
        .ToListAsync();

   var stack = new StackPanel { Spacing = 6 };

   stack.Children.Add(new TextBlock { Text = $"เลือกหมายเลขคูปองสำหรับ '{selectedDefinition.Name}'", TextWrapping = TextWrapping.Wrap });
        
    // Add COM checkbox in the dialog
        var comCheckBox = new CheckBox 
        { 
            Content = "โหมด: COM (ตั๋วฟรี) - รหัสที่เลือกจะถูกทำเครื่องหมายเป็นตั๋วฟรี", 
            FontSize = 16,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange),
     FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
   Margin = new Thickness(0, 4, 0, 8)
  };
        stack.Children.Add(comCheckBox);

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
                    var isAlreadySelected = alreadySelectedInOtherItems.Contains(g.Id);
                    
                    // สร้าง content ที่แสดงสถานะ
   var content = g.GeneratedCode;
            if (isAlreadySelected)
  {
          content += " (ถูกเลือกแล้ว)";
    }

  var cb = new CheckBox 
         { 
      Content = content, 
   Tag = g.Id, 
   Margin = new Thickness(0, 2, 0, 2),
    IsEnabled = !isAlreadySelected // ปิดการใช้งานถ้าถูกเลือกไปแล้ว
          };

    // ถ้าเป็นหมายเลขที่เลือกไว้ในรายการปัจจุบัน ให้ check ไว้
                 if (initialSelectedIds.Contains(g.Id)) 
        {
    cb.IsChecked = true;
       }

  // เปลี่ยนสีถ้าถูกเลือกไปแล้ว
    if (isAlreadySelected)
         {
             cb.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

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

    // Get COM mode flag but DON'T save to database yet
       var isComMode = comCheckBox.IsChecked == true;
        
    // Return tuple with selectedIds and COM flag
        // Database update will happen when receipt is saved
      return (selectedIds, isComMode);
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

                var totalGenerated = currentDefinition.GeneratedCoupons?.Count ??0;
                var totalAllocatedOrUsed = currentDefinition.GeneratedCoupons?.Count(gc => gc.IsUsed || gc.ReceiptItemId != null) ??0;
                var availableCount = totalGenerated - totalAllocatedOrUsed;

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

         // คำนวณส่วนลดจาก COM ล่วงหน้า
        decimal comDiscount = 0m;
foreach (var item in _selectedItems)
         {
    if (item.IsCOM)
 {
      comDiscount += item.CouponDefinition.Price * item.Quantity;
                }
            }

    // แสดงส่วนลดจาก COM (อ่านอย่างเดียว)
  if (comDiscount > 0)
            {
      customerPanel.Children.Add(new TextBlock 
              { 
 Text = $"ส่วนลดจาก COM: {comDiscount:N2} บาท",
             FontSize = 16,
    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
           Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange),
              Margin = new Thickness(0, 10, 0, 0)
        });
  }

            // ช่องกรอกส่วนลดเพิ่มเติม
            customerPanel.Children.Add(new TextBlock 
       { 
     Text = "ส่วนลดเพิ่มเติม (ถ้ามี):",
           Margin = new Thickness(0, 10, 0, 0)
 });
          var additionalDiscountBox = new NumberBox 
    { 
       Value = 0, 
              Minimum = 0, 
     Maximum = (double)_selectedItems.Sum(item => item.TotalPrice),
    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
       PlaceholderText = "0.00"
 };
       customerPanel.Children.Add(additionalDiscountBox);

   // แสดงยอดรวมส่วนลด
        var totalDiscountText = new TextBlock
      {
       Text = $"ส่วนลดรวม: {comDiscount:N2} บาท",
       FontSize = 16,
          FontWeight = Microsoft.UI.Text.FontWeights.Bold,
         Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green),
     Margin = new Thickness(0, 10, 0, 0)
            };
customerPanel.Children.Add(totalDiscountText);

            // อัปเดตยอดรวมส่วนลดเมื่อกรอกส่วนลดเพิ่ม
            additionalDiscountBox.ValueChanged += (s, args) =>
  {
        var additionalDiscount = double.IsNaN(additionalDiscountBox.Value) ? 0m : (decimal)additionalDiscountBox.Value;
 var totalDiscount = comDiscount + additionalDiscount;
 totalDiscountText.Text = $"ส่วนลดรวม: {totalDiscount:N2} บาท";
    };

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

        // อ่านค่าส่วนลดเพิ่มเติม
           var additionalDiscountVal = double.IsNaN(additionalDiscountBox.Value) ? 0.0 : additionalDiscountBox.Value;
          if (additionalDiscountVal < 0)
                {
                    await ShowErrorDialog("ส่วนลดต้องเป็นค่าบวกหรือเท่ากับศูนย์");
            return;
           }

     // เก็บส่วนลดเพิ่มเติม (ส่วนลด COM จะคำนวณภายหลัง)
            _receiptDiscount = (decimal)additionalDiscountVal;

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Verify DB connectivity before generating receipt code to provide clearer error message
                try
                {
                    using var testCtx = new CouponContext();
                    var canConnect = await testCtx.Database.CanConnectAsync();
                    if (!canConnect)
                    {
                        await ShowErrorDialog("ไม่สามารถเชื่อมต่อฐานข้อมูล: กรุณาตรวจสอบการตั้งค่าการเชื่อมต่อฐานข้อมูล");
                        return;
                    }
                }
                catch (Exception connEx)
                {
                    await ShowErrorDialog($"ไม่สามารถตรวจสอบการเชื่อมต่อฐานข้อมูล: {connEx.Message}");
                    return;
                }
                
                string receiptCode;
                try
                {
                    // Generate next receipt code (may throw if DB sequence/unavailable)
                    receiptCode = await ReceiptNumberService.GenerateNextReceiptCodeAsync();
                }
                catch (Exception genEx)
                {
                    // Provide more context about failure to generate receipt number
                    var detail = genEx.InnerException != null ? genEx.InnerException.Message : genEx.Message;
                    await ShowErrorDialog($"เกิดข้อผิดพลาด: ไม่สามารถสร้างหมายเลขใบเสร็จได้\n\nรายละเอียด: {detail}");
                    return;
                }

                // Create and save receipt with customer information, receipt code and payment method
                var receipt = new ReceiptModel
                {
                    ReceiptCode = receiptCode,
                    ReceiptDate = DateTime.Now,
                    CustomerName = customerName,
                    CustomerPhoneNumber = phoneNumber,
                    Discount = _receiptDiscount,
                    TotalAmount = _selectedItems.Sum(item => item.TotalPrice) - _receiptDiscount,
                    SalesPersonId = (SalesPersonComboBox.SelectedItem as SalesPerson)?.ID,
                    PaymentMethodId = GetSelectedPaymentMethod()?.Id
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
                        CouponId = item.CouponDefinition.Id,
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
                for (int i =0; i < _selectedItems.Count; i++)
                {
                    var item = _selectedItems[i];
                    var receiptItem = createdReceiptItems[i];

                    if (item.CouponDefinition.IsLimited)
                    {
                        if (item.SelectedGeneratedIds != null && item.SelectedGeneratedIds.Any())
                        {
                            // allocate those specific ids
                            var allocate = await _context.GeneratedCoupons
                                .Where(g => item.SelectedGeneratedIds.Contains(g.Id) && !g.IsUsed && g.ReceiptItemId == null)
                                .ToListAsync();

                            if (allocate.Count < item.Quantity)
                            {
                                await tx.RollbackAsync();
                                await ShowErrorDialog("คูปองที่เลือกบางรายการไม่พร้อมใช้งาน");
                                return;
                            }

                            foreach (var g in allocate)
                            {
                                // Link the generated coupon to the receipt item
                                g.ReceiptItemId = receiptItem.ReceiptItemId;
   
            // *** บันทึก IsComplimentary ตอนนี้ (เมื่อบันทึกใบเสร็จสำเร็จ) ***
         if (item.IsCOM)
      {
         g.IsComplimentary = true;
    }
   
    _context.GeneratedCoupons.Update(g);
 }
      }
         else
{
         // original allocation by selecting first available
 var allocate = await _context.GeneratedCoupons
         .Where(g => g.CouponDefinitionId == item.CouponDefinition.Id && !g.IsUsed && g.ReceiptItemId == null)
          .OrderBy(g => g.Id)
        .Take(item.Quantity)
          .ToListAsync();

    if (allocate.Count < item.Quantity) { await tx.RollbackAsync(); await ShowErrorDialog("คูปองไม่เพียงพอ"); return; }

       foreach (var g in allocate)
      {
       // Link the generated coupon to the receipt item
            g.ReceiptItemId = receiptItem.ReceiptItemId;
       _context.GeneratedCoupons.Update(g);
}
     }
    }
      }

                    // Calculate total discount from COM (complimentary) coupons
    decimal comDiscountFinal = 0m;
    foreach (var item in _selectedItems)
    {
        // คำนวณส่วนลดจาก item.IsCOM (ที่เก็บไว้ตอนเลือก)
        if (item.IsCOM)
        {
       // คูปอง COM ทั้งใบจะได้ส่วนลดเท่ากับราคาเต็ม
      comDiscountFinal += item.CouponDefinition.Price * item.Quantity;
 }
    }

    // รวมส่วนลดทั้งหมด = ส่วนลดจาก COM + ส่วนลดเพิ่มเติม
    decimal totalDiscount = comDiscountFinal + _receiptDiscount;
  
    // Update receipt with total combined discount
    receipt.Discount = totalDiscount;
    receipt.TotalAmount = _selectedItems.Sum(item => item.TotalPrice) - receipt.Discount;
_context.Receipts.Update(receipt);

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
            decimal subtotal = _selectedItems.Sum(item => item.TotalPrice);
    
  // คำนวณส่วนลดจาก COM
       decimal comDiscount = 0m;
     foreach (var item in _selectedItems)
     {
    if (item.IsCOM)
     {
  comDiscount += item.CouponDefinition.Price * item.Quantity;
          }
   }
          
     decimal totalDiscount = comDiscount;
   decimal netTotal = subtotal - totalDiscount;
       
      // อัปเดต UI
          SubtotalTextBlock.Text = subtotal.ToString("N2");
     TotalDiscountTextBlock.Text = totalDiscount.ToString("N2");
    TotalPriceTextBlock.Text = netTotal.ToString("N2");
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
            // Outer loop so we can return to the confirmation dialog when user cancels the reason dialog
            while (true)
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

                    return; // printed -> exit
                }

                // กดปุ่มยกเลิก - ขอเหตุผลการยกเลิกการพิมพ์ก่อน และเก็บหมายเลขใบเสร็จเพื่อรีไซเคิลเฉพาะเครื่องนี้
                while (true)
                {
                    var reasonPanel = new StackPanel { Spacing =8 };
                    reasonPanel.Children.Add(new TextBlock { Text = "สาเหตุการยกเลิกการพิมพ์ (จำเป็น):" });
                    var reasonBox = new TextBox { PlaceholderText = "ระบุสาเหตุ เช่น ผิดรายการ/ลืมเพิ่มสินค้า", AcceptsReturn = false };
                    reasonPanel.Children.Add(reasonBox);

                    var reasonDialog = new ContentDialog
                    {
                        Title = "ระบุสาเหตุการยกเลิก",
                        Content = reasonPanel,
                        PrimaryButtonText = "ยืนยัน",
                        CloseButtonText = "ยกเลิก",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.XamlRoot
                    };

                    var r = await reasonDialog.ShowAsync();

                    if (r == ContentDialogResult.Primary)
                    {
                        var reason = reasonBox.Text?.Trim() ?? string.Empty;
                        if (string.IsNullOrEmpty(reason))
                        {
                            // แจ้งเตือนและวนกลับมาให้กรอกใหม่
                            await ShowErrorDialog("กรุณาระบุสาเหตุการยกเลิกก่อนยืนยัน");
                            continue; // เปิด reason dialog ใหม่
                        }

                        try
                        {
                            // เก็บหมายเลขที่ยกเลิกพร้อมเหตุผลและเจ้าของเครื่อง (ReceiptNumberService จะบันทึก OwnerMachineId)
                            await ReceiptNumberService.RecycleReceiptCodeAsync(receiptCode, reason);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to recycle receipt code: {ex.Message}");
                            await ShowErrorDialog($"เกิดข้อผิดพลาดในการเก็บหมายเลขใบเสร็จ: {ex.Message}");
                            return; // error -> abort
                        }

                        // เรียก handler สำหรับการยกเลิกใบเสร็จ (เหมือนปุ่มยกเลิกปกติ)
                        await ReceiptPrintService.HandleReceiptCancellationAsync(receiptId, this.XamlRoot);

                        // ✅ เพิ่มการรีเฟรชข้อมูลคูปองทันทีหลังยกเลิก
                        await PerformSearch();

                        // ✅ อัปเดตราคารวมเผื่อมีรายการค้างอยู่
                        UpdateTotalPrice();

                        return; // recycled -> exit
                    }
                    else
                    {
                        // user cancelled the reason dialog: กลับไปหน้าบอกว่าจะพิมโดยใช้หมายเลขอะไร
                        // break inner reason loop and continue outer loop to show confirmation again
                        break;
                    }
                }
                // continue outer while loop -> show confirm dialog again
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
                // Only allow editing for unlimited coupons
                if (selectedItem.CouponDefinition.IsLimited)
                {
                    // Do nothing (Edit not available for limited coupons)
                    return;
                }

                // For unlimited coupons, edit should allow changing quantity only (and discount)
                var quantityBox = new NumberBox
                {
                    Value = selectedItem.Quantity >0 ? selectedItem.Quantity :1,
                    Minimum =1,
                    Maximum = int.MaxValue,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                    Margin = new Thickness(0,10,0,0)
                };

                var totalPriceTextBlock = new TextBlock
                {
                    Text = $"รวมเป็นเงิน: {selectedItem.CouponDefinition.Price * (decimal)quantityBox.Value} บาท",
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Margin = new Thickness(0,8,0,10)
                };

                void Recalc()
                {
                    var q = quantityBox.Value >0 ? (int)quantityBox.Value :0;
                    var total = selectedItem.CouponDefinition.Price * (decimal)q;
                    if (total <0) total =0;
                    totalPriceTextBlock.Text = $"รวมเป็นเงิน: {total} บาท";
                }

                quantityBox.ValueChanged += (s, args) => Recalc();

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
                    if (double.IsNaN(quantityBox.Value) || quantityBox.Value <=0)
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
                        if (display.TotalUsed <0) display.TotalUsed = 0;
                    }

                    int index = _selectedItems.IndexOf(selectedItem);
                    if (index >=0)
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