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
using System.Data;
using Microsoft.Data.SqlClient;

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
                    OnPropertyChanged(nameof(TotalPrice)); // ⭐ เพิ่มบรรทัดนี้
                }
            }
        }

        // Text to display COM badge
        public string ComBadgeText => IsCOM ? "🎁 COM (ตั๋วฟรี)" : string.Empty;

        // Expose whether this item is for a limited coupon (has generated codes)
        public bool IsLimited => CouponDefinition?.IsLimited ?? false;

        // ⭐ แก้ไข: ให้แสดงราคาดั้งเดิมแม้เป็น COM (ไม่ลดเป็น 0)
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
        private decimal _receiptDiscount = 0m;

        private readonly CouponContext _context = new CouponContext();

        // Master collection (all search results)
        private readonly ObservableCollection<CouponDefinitionDisplay> _allCouponDefinitionDisplays = new();

        // Paged collection bound to the ListView
        private readonly ObservableCollection<CouponDefinitionDisplay> _couponDefinitionDisplays = new();

        private readonly ObservableCollection<ReceiptItem> _selectedItems = new();
        private readonly ObservableCollection<SalesPerson> _salesPersons = new();
        private readonly string _reservationSessionId = Guid.NewGuid().ToString();
        private readonly ReservationService _reservationService;
        private readonly List<PaymentMethod> _paymentMethods = new();
        private readonly List<CheckBox> _paymentCheckBoxes = new();

        // Pagination fields
        private int _currentPage = 1;
        private const int _pageSize = 25;
        private int _totalPages = 1;

        // เพิ่มตัวแปรสำหรับ debounce
        private System.Threading.Timer? _searchTimer;
        private readonly object _searchLock = new object();

        // เก็บค่า VerticalOffset ของ ListView เพื่อคืนสถานะหลังรีเฟรช
        private double _couponListVerticalOffset = 0;

        private static int ParseTrailingNumber(string? code)
        {
            if (string.IsNullOrEmpty(code))
                return int.MaxValue;

            int i = code.Length - 1;
            while (i >= 0 && char.IsDigit(code[i])) i--;
            var digits = code.Substring(i + 1);
            if (string.IsNullOrEmpty(digits))
                return int.MaxValue;

            // ป้องกัน overflow — ใช้เฉพาะ 9 หลักท้ายสุดถ้ามากเกินไป
            if (digits.Length > 9)
                digits = digits.Substring(digits.Length - 9);

            return int.TryParse(digits, out var n) ? n : int.MaxValue;
        }
        public Receipt()
        {
            InitializeComponent();
            CouponDefinitionsListView.ItemsSource = _couponDefinitionDisplays;
            SelectedItemsListView.ItemsSource = _selectedItems;
            SalesPersonComboBox.ItemsSource = _salesPersons;
            _reservationService = new ReservationService(_context);
            _reservationService = new ReservationService(_context);
            this.Loaded += Receipt_Loaded;
        }

        private async void Receipt_Loaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe so it only runs once
            this.Loaded -= Receipt_Loaded;

            try
            {
                // ✅ เพิ่ม: Preload logo ล่วงหน้าเพื่อให้พร้อมก่อนพิมพ์
                await ReceiptPrintService.ForceReloadLogoAsync();
                Debug.WriteLine("✅ Logo preloaded in Receipt page");
                
                await LoadAllCouponDefinitions();
                await LoadSalesPersons();
                await LoadBranchTypes();
                await LoadPaymentMethods();
                await LoadSaleEvents();
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

        // แก้ไขให้มีการตรวจสอบวันที่เริ่มต้นและสิ้นสุดของกิจกรรมขาย
        private async Task LoadSaleEvents()
        {
            try
            {
                await _context.Database.EnsureCreatedAsync();
                var now = DateTime.Now.Date;

                var saleEvents = await _context.SaleEvents
                    .AsNoTracking()
                    .Where(se => se.IsActive && se.StartDate <= DateTime.Now && se.EndDate >= DateTime.Now)
                    .OrderBy(se => se.StartDate)
                    .ToListAsync();

                SaleEventComboBox.Items.Clear();

                var allItem = new ComboBoxItem { Content = "ทั้งหมด", Tag = "ALL", IsSelected = true };
                SaleEventComboBox.Items.Add(allItem);

                foreach (var se in saleEvents)
                {
                    var item = new ComboBoxItem
                    {
                        Content = $"{se.Name} ({se.StartDate:yyyy-MM-dd} – {se.EndDate:yyyy-MM-dd})",
                        Tag = se.Id.ToString()
                    };
                    SaleEventComboBox.Items.Add(item);
                }

                // ensure default selection
                if (SaleEventComboBox.Items.Count > 0)
                    SaleEventComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load SaleEvents: {ex.Message}");
            }
        }

        private async Task LoadAllCouponDefinitions()
        {
            // fetch definitions + counts in one DB query (no tracking)
            using var ctx = new CouponContext();
            await ctx.Database.EnsureCreatedAsync();

            var today = DateTime.Now.Date;

            var rows = await ctx.CouponDefinitions
                .AsNoTracking()
                .Where(cd => cd.IsActive && cd.ValidTo >= DateTime.Now)
                .Select(cd => new
                {
                    Definition = cd,
                    TotalGenerated = cd.GeneratedCoupons.Count(),
                    TotalAllocatedOrUsed = cd.GeneratedCoupons.Count(gc => gc.IsUsed || gc.ReceiptItemId != null),
                    // project SaleEvent metadata to avoid loading full navigation
                    SaleEventId = cd.SaleEventId,
                    SaleEventStart = cd.SaleEvent != null ? (DateTime?)cd.SaleEvent.StartDate : null,
                    SaleEventEnd = cd.SaleEvent != null ? (DateTime?)cd.SaleEvent.EndDate : null,
                    SaleEventIsActive = cd.SaleEvent != null ? (bool?)cd.SaleEvent.IsActive : null
                })
                .ToListAsync();

            _allCouponDefinitionDisplays.Clear();

            foreach (var r in rows)
            {
                // If coupon is NOT attached to a SaleEvent => skip (per requested rule)
                if (!r.SaleEventId.HasValue)
                {
                    continue;
                }

                // Skip sale events that are inactive or ended or not yet started
                if (r.SaleEventIsActive != true
                    || (r.SaleEventEnd.HasValue && r.SaleEventEnd.Value.Date < today)
                    || (r.SaleEventStart.HasValue && r.SaleEventStart.Value.Date > today))
                {
                    continue;
                }

                var totalGenerated = r.TotalGenerated;
                var totalAllocatedOrUsed = r.TotalAllocatedOrUsed;
                var availableCount = totalGenerated - totalAllocatedOrUsed;

                if (totalGenerated == 0 || availableCount > 0)
                {
                    _allCouponDefinitionDisplays.Add(new CouponDefinitionDisplay
                    {
                        CouponDefinition = r.Definition,
                        TotalGenerated = totalGenerated,
                        TotalUsed = totalAllocatedOrUsed
                    });
                }
            }

            // Apply paging after load
            ApplyPaging();
        }

        private async Task LoadBranchTypes()
        {
            await _context.Database.EnsureCreatedAsync();

            var branches = await _context.Branches
                .OrderBy(b => b.Name)
                .ToListAsync();

            // Clear existing items
            BranchComboBox.Items.Clear();

            // Add "ทั้งหมด" option with Tag "ALL"
            var allItem = new ComboBoxItem { Content = "ทั้งหมด", Tag = "ALL", IsSelected = true };
            BranchComboBox.Items.Add(allItem);

            // Add branches from database using Id as Tag
            foreach (var branch in branches)
            {
                var item = new ComboBoxItem { Content = branch.Name, Tag = branch.Id.ToString() };
                BranchComboBox.Items.Add(item);
            }

            // Set default selection to "ทั้งหมด"
            try
            {
                if (BranchComboBox.Items.Count > 0)
                {
                    BranchComboBox.SelectedIndex = 0; // เลือก "ทั้งหมด" (index 0)
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set default Branch selection: {ex.Message}");
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

        private void BranchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PerformDelayedSearch();
        }
        private void SaleEventComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        // Method สำหรับค้นหาจริง - แก้ไขให้ใช้ BranchId แบบเดียวกับ CouponDefinitionPage
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

                var nameSearch = NameSearchTextBox.Text?.Trim();
                var codeSearch = CodeSearchTextBox.Text?.Trim();
                var branchFilter = GetSelectedTag(BranchComboBox);
                var saleEventFilter = GetSelectedTag(SaleEventComboBox);

                using var ctx = new CouponContext();
                await ctx.Database.EnsureCreatedAsync();

                var today = DateTime.Now.Date;
                var query = ctx.CouponDefinitions
                    .AsNoTracking()
                    .Where(cd => cd.IsActive && cd.ValidTo >= DateTime.Now);

                // Use EF.Functions.Like to allow index use and avoid client-side ToLower
                if (!string.IsNullOrEmpty(nameSearch))
                {
                    var pattern = $"%{nameSearch.Replace("%", "[%]").Replace("_", "[_]")}%";
                    query = query.Where(cd => EF.Functions.Like(cd.Name, pattern));
                }

                if (!string.IsNullOrEmpty(codeSearch))
                {
                    var pattern = $"%{codeSearch.Replace("%", "[%]").Replace("_", "[_]")}%";
                    query = query.Where(cd => EF.Functions.Like(cd.Code, pattern));
                }

                if (branchFilter != "ALL" && int.TryParse(branchFilter, out int branchId))
                {
                    query = query.Where(cd => cd.BranchId == branchId);
                }

                if (saleEventFilter != "ALL" && int.TryParse(saleEventFilter, out int saleEventId))
                {
                    query = query.Where(cd => cd.SaleEventId == saleEventId);
                }

                // Project counts and sale-event metadata server-side to avoid loading GeneratedCoupons collections or SaleEvent navigation
                var rows = await query
                    .Select(cd => new
                    {
                        Definition = cd,
                        TotalGenerated = cd.GeneratedCoupons.Count(),
                        TotalAllocatedOrUsed = cd.GeneratedCoupons.Count(gc => gc.IsUsed || gc.ReceiptItemId != null),
                        SaleEventId = cd.SaleEventId,
                        SaleEventStart = cd.SaleEvent != null ? (DateTime?)cd.SaleEvent.StartDate : null,
                        SaleEventEnd = cd.SaleEvent != null ? (DateTime?)cd.SaleEvent.EndDate : null,
                        SaleEventIsActive = cd.SaleEvent != null ? (bool?)cd.SaleEvent.IsActive : null
                    })
                    .ToListAsync();

            // Populate master list
            _allCouponDefinitionDisplays.Clear();
            foreach (var r in rows)
            {
                // If coupon is NOT attached to a SaleEvent => skip (per requested rule)
                if (!r.SaleEventId.HasValue)
                {
                    continue;
                }

                // Skip sale events that are inactive or ended or not yet started
                if (r.SaleEventIsActive != true
                    || (r.SaleEventEnd.HasValue && r.SaleEventEnd.Value.Date < today)
                    || (r.SaleEventStart.HasValue && r.SaleEventStart.Value.Date > today))
                {
                    continue;
                }

                var totalGenerated = r.TotalGenerated;
                var totalAllocatedOrUsed = r.TotalAllocatedOrUsed;
                var availableCount = totalGenerated - totalAllocatedOrUsed;

                if (totalGenerated == 0 || availableCount > 0)
                {
                    _allCouponDefinitionDisplays.Add(new CouponDefinitionDisplay
                    {
                        CouponDefinition = r.Definition,
                        TotalGenerated = totalGenerated,
                        TotalUsed = totalAllocatedOrUsed
                    });
                }
            }

            // Apply paging to display the first page of the result
            ApplyPaging();

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

        // Helper: หา display จาก CouponDefinition.Id (ค้นหาใน master list)
        private CouponDefinitionDisplay? GetDisplayByDefinitionId(int definitionId)
        {
            return _allCouponDefinitionDisplays.FirstOrDefault(d => d.CouponDefinition?.Id == definitionId);
        }

        // Applies paging to the master collection and fills the paged collection that's bound to ListView
        private void ApplyPaging(int page = -1)
        {
            if (page >= 1) _currentPage = page;
            if (_currentPage < 1) _currentPage = 1;

            int totalItems = _allCouponDefinitionDisplays.Count;
            _totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)_pageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            int skip = (_currentPage - 1) * _pageSize;
            var pageItems = _allCouponDefinitionDisplays.Skip(skip).Take(_pageSize).ToList();

            _couponDefinitionDisplays.Clear();
            foreach (var it in pageItems)
                _couponDefinitionDisplays.Add(it);

            // Update page info UI
            PageInfoTextBlock.Text = $"หน้า {_currentPage} / {_totalPages} ({totalItems} รายการ)";

            UpdatePagingButtons();
        }

        private void UpdatePagingButtons()
        {
            PrevPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < _totalPages;
        }

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                // preserve scroll offset before changing page
                try
                {
                    var sv = FindScrollViewer(CouponDefinitionsListView);
                    _couponListVerticalOffset = sv?.VerticalOffset ?? 0;
                }
                catch { }

                _currentPage--;
                ApplyPaging();

                // restore offset on new page
                try
                {
                    var svAfter = FindScrollViewer(CouponDefinitionsListView);
                    svAfter?.ChangeView(null, _couponListVerticalOffset, null, disableAnimation: true);
                }
                catch { }
            }
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                try
                {
                    var sv = FindScrollViewer(CouponDefinitionsListView);
                    _couponListVerticalOffset = sv?.VerticalOffset ?? 0;
                }
                catch { }

                _currentPage++;
                ApplyPaging();

                try
                {
                    var svAfter = FindScrollViewer(CouponDefinitionsListView);
                    svAfter?.ChangeView(null, _couponListVerticalOffset, null, disableAnimation: true);
                }
                catch { }
            }
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
                    var (normalSelectedIds, comSelectedIds) = result.Value;
                    if ((normalSelectedIds == null || !normalSelectedIds.Any()) && (comSelectedIds == null || !comSelectedIds.Any())) return;

                    // Work with distinct ids (preserve order: normal then com)
                    var allIdsOrdered = new List<int>();
                    if (normalSelectedIds != null) allIdsOrdered.AddRange(normalSelectedIds);
                    if (comSelectedIds != null) allIdsOrdered.AddRange(comSelectedIds);
                    var distinctIds = allIdsOrdered.Distinct().ToList();

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

                        var isComForThisId = comSelectedIds != null && comSelectedIds.Contains(gid);

                        var receiptItem = new ReceiptItem
                        {
                            CouponDefinition = selectedDefinition,
                            Quantity = 1,
                            SelectedGeneratedIds = new List<int> { gid },
                            SelectedCodesPreview = codesMap.TryGetValue(gid, out var code) ? code ?? string.Empty : string.Empty,
                            IsCOM = isComForThisId
                        };

                        _selectedItems.Add(receiptItem);

                        if (display2 != null)
                        {
                            display2.TotalUsed += 1;
                        }
                    }

                    UpdateTotalPrice();
                    return;
                }

                // Non-limited flow - ถามจำนวนปกติและจำนวนฟรี
                var quantityBox = new NumberBox
                {
                    Value = 1,
                    Minimum = 0,
                    Maximum = int.MaxValue,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var freeQuantityBox = new NumberBox
                {
                    Value = 0,
                    Minimum = 0,
                    Maximum = int.MaxValue,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var priceTextBlock = new TextBlock
                {
                    Text = $"ราคา: {selectedDefinition.Price:N2} บาท/ใบ",
                    FontSize = 16,
                    Margin = new Thickness(0, 10, 0, 10)
                };

                var totalPriceTextBlock = new TextBlock
                {
                    Text = $"รวมเป็นเงิน: {selectedDefinition.Price:N2} บาท",
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    FontSize = 18,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                void RecalcTotal()
                {
                    var q = quantityBox.Value > 0 ? (int)quantityBox.Value : 0;
                    var freeQ = freeQuantityBox.Value > 0 ? (int)freeQuantityBox.Value : 0;
                    var total = selectedDefinition.Price * (decimal)q;
                    totalPriceTextBlock.Text = $"รวมเป็นเงิน: {total:N2} บาท (ปกติ: {q} ใบ, ฟรี: {freeQ} ใบ)";
                }

                quantityBox.ValueChanged += (s, args) => RecalcTotal();
                freeQuantityBox.ValueChanged += (s, args) => RecalcTotal();

                var contentPanel = new StackPanel { Spacing = 8 };
                contentPanel.Children.Add(new TextBlock 
                { 
                    Text = $"คูปอง: {selectedDefinition.Name}", 
                    FontSize = 18, 
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold 
                });
                contentPanel.Children.Add(priceTextBlock);

                // ส่วนจำนวนปกติ
                contentPanel.Children.Add(new TextBlock 
                { 
                    Text = "จำนวนที่ซื้อ (คิดเงิน):", 
                    FontSize = 16, 
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 4) 
                });
                contentPanel.Children.Add(quantityBox);

                // ส่วนจำนวนฟรี (COM)
                contentPanel.Children.Add(new TextBlock 
                { 
                    Text = "🎁 จำนวนฟรี (COM - ไม่คิดเงิน):", 
                    FontSize = 16, 
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange),
                    Margin = new Thickness(0, 12, 0, 4) 
                });
                contentPanel.Children.Add(freeQuantityBox);

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
                    var quantity = quantityBox.Value > 0 ? (int)quantityBox.Value : 0;
                    var freeQuantity = freeQuantityBox.Value > 0 ? (int)freeQuantityBox.Value : 0;

                    // Validate that at least one quantity is specified
                    if (quantity <= 0 && freeQuantity <= 0)
                    {
                        await ShowErrorDialog("กรุณาระบุจำนวนคูปองอย่างน้อย 1 ใบ");
                        return;
                    }

                    // เพิ่มจำนวนปกติ (คิดเงิน)
                    if (quantity > 0)
                    {
                        var existingItem = _selectedItems.FirstOrDefault(it => 
                            it.CouponDefinition.Id == selectedDefinition.Id && 
                            !it.IsCOM && 
                            (it.SelectedGeneratedIds == null || !it.SelectedGeneratedIds.Any()));
                        
                        if (existingItem != null)
                        {
                            existingItem.Quantity += quantity;
                        }
                        else
                        {
                            var receiptItem = new ReceiptItem
                            {
                                CouponDefinition = selectedDefinition,
                                Quantity = quantity,
                                IsCOM = false
                            };
                            _selectedItems.Add(receiptItem);
                        }
                    }

                    // เพิ่มจำนวนฟรี (COM - ไม่คิดเงิน)
                    if (freeQuantity > 0)
                    {
                        var existingComItem = _selectedItems.FirstOrDefault(it => 
                            it.CouponDefinition.Id == selectedDefinition.Id && 
                            it.IsCOM && 
                            (it.SelectedGeneratedIds == null || !it.SelectedGeneratedIds.Any()));
                        
                        if (existingComItem != null)
                        {
                            existingComItem.Quantity += freeQuantity;
                        }
                        else
                        {
                            var comReceiptItem = new ReceiptItem
                            {
                                CouponDefinition = selectedDefinition,
                                Quantity = freeQuantity,
                                IsCOM = true
                            };
                            _selectedItems.Add(comReceiptItem);
                        }
                    }

                    UpdateTotalPrice();
                }
            }
        }
        // If the older single-result method name does not exist in your file, add this compatibility wrapper
        // which contains the previous single-list dialog implementation. If you already have the full
        // two-list implementation elsewhere, you can remove the wrapper above and keep that implementation.
        private async Task<(List<int>? selectedIds, bool isComMode)?> ShowPickGeneratedCodesDialogAsync_Old(CouponDefinition selectedDefinition, ReceiptItem? existingItem)
        {
            await _context.Database.EnsureCreatedAsync();

            var initialSelectedIds = existingItem?.SelectedGeneratedIds ?? new List<int>();

            // IDs already chosen in other receipt items (exclude current item)
            var alreadySelectedInOtherItems = _selectedItems
                .Where(it => it != existingItem && it.SelectedGeneratedIds != null && it.SelectedGeneratedIds.Any())
                .SelectMany(it => it.SelectedGeneratedIds!)
                .Distinct()
                .ToList();

            var availableCodes = await _context.GeneratedCoupons
                .Where(g => g.CouponDefinitionId == selectedDefinition.Id && ((g.ReceiptItemId == null && !g.IsUsed) || initialSelectedIds.Contains(g.Id)))
                .ToListAsync();

            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(new TextBlock { Text = $"เลือกหมายเลขคูปองสำหรับ '{selectedDefinition.Name}'", TextWrapping = TextWrapping.Wrap });

            // COM checkbox
            var comCheckBox = new CheckBox
            {
                Content = "โหมด: COM (ตั๋วฟรี) - รหัสที่เลือกจะถูกทำเครื่องหมายเป็นตั๋วฟรี",
                FontSize = 16,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 8)
            };
            stack.Children.Add(comCheckBox);

            // Quantity input for quick selection
            var quantityPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            quantityPanel.Children.Add(new TextBlock { Text = "จำนวนที่ต้องการ:", VerticalAlignment = VerticalAlignment.Center });
            var quantityBox = new NumberBox
            {
                Minimum = 1,
                Maximum = Math.Max(1, availableCodes.Count),
                Value = initialSelectedIds.Count > 0 ? initialSelectedIds.Count : 1,
                Width = 220,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            quantityPanel.Children.Add(quantityBox);
            stack.Children.Add(quantityPanel);

            // Search box
            var searchBox = new TextBox { PlaceholderText = "ค้นหารหัส (พิมพ์แล้วรายการจะกรองอัตโนมัติ)", Margin = new Thickness(0, 4, 0, 0) };
            stack.Children.Add(searchBox);

            var infoText = new TextBlock { Text = $"หมายเลขที่แสดง: 0 / {availableCodes.Count}", Margin = new Thickness(0, 6, 0, 0) };
            stack.Children.Add(infoText);

            var scroll = new ScrollViewer { Height = 300 };
            var resultsPanel = new StackPanel { Spacing = 2 };
            scroll.Content = resultsPanel;
            stack.Children.Add(scroll);

            var checkboxMap = new Dictionary<int, CheckBox>();

            void PopulateResults(string? filter)
            {
                resultsPanel.Children.Clear();
                checkboxMap.Clear();

                var query = availableCodes.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    query = query.Where(g => g.GeneratedCode?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                var displayed = query.Take(1000).ToList();

                foreach (var g in displayed)
                {
                    var isAlreadySelected = alreadySelectedInOtherItems.Contains(g.Id);
                    var content = g.GeneratedCode;
                    if (isAlreadySelected) content += " (ถูกเลือกแล้ว)";

                    var cb = new CheckBox
                    {
                        Content = content,
                        Tag = g.Id,
                        Margin = new Thickness(0, 2, 0, 2),
                        IsEnabled = !isAlreadySelected
                    };

                    if (initialSelectedIds.Contains(g.Id))
                        cb.IsChecked = true;

                    if (isAlreadySelected)
                        cb.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);

                    checkboxMap[g.Id] = cb;
                    resultsPanel.Children.Add(cb);
                }

                infoText.Text = $"หมายเลขที่แสดง: {displayed.Count} / {availableCodes.Count}";
            }

            // Helper to sync selection to desired quantity
            void SyncSelectionWithQuantity()
            {
                if (checkboxMap.Count == 0) return;
                int desired = (int)Math.Max(1, quantityBox.Value);

                var initialChecked = checkboxMap.Where(kv => initialSelectedIds.Contains(kv.Key) && kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
                int alreadyCount = initialChecked.Count;

                var nonInitialKeys = checkboxMap.Keys.Except(initialSelectedIds).ToList();
                foreach (var k in nonInitialKeys)
                    checkboxMap[k].IsChecked = false;

                int need = desired - alreadyCount;
                if (need <= 0) return;

                foreach (var kv in checkboxMap)
                {
                    if (need <= 0) break;
                    var id = kv.Key;
                    var cb = kv.Value;
                    if (initialSelectedIds.Contains(id)) continue;
                    if (!cb.IsEnabled) continue;
                    if (cb.IsChecked == true) continue;
                    cb.IsChecked = true;
                    need--;
                }
            }

            searchBox.TextChanged += (s, e) =>
            {
                PopulateResults(searchBox.Text?.Trim());
                DispatcherQueue.TryEnqueue(() => SyncSelectionWithQuantity());
            };

            quantityBox.ValueChanged += (s, e) =>
            {
                if (double.IsNaN(quantityBox.Value) || quantityBox.Value < 1) quantityBox.Value = 1;
                DispatcherQueue.TryEnqueue(() => SyncSelectionWithQuantity());
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

                int desiredQty = (int)Math.Max(1, quantityBox.Value);
                if (selectedIds.Count != desiredQty)
                {
                    var remaining = availableCodes.Select(g => g.Id).Except(selectedIds).Except(alreadySelectedInOtherItems).ToList();
                    foreach (var id in remaining)
                    {
                        if (selectedIds.Count >= desiredQty) break;
                        selectedIds.Add(id);
                    }

                    if (selectedIds.Count > desiredQty)
                        selectedIds = selectedIds.Take(desiredQty).ToList();
                }

                var isComMode = comCheckBox.IsChecked == true;
                return (selectedIds, isComMode);
            }

            return null;
        }

        // helper used by older dialog
        private List<int> _selected_items_helper()
        {
            return _selectedItems
                .Where(it => it.SelectedGeneratedIds != null && it.SelectedGeneratedIds.Any())
                .SelectMany(it => it.SelectedGeneratedIds!)
                .Distinct()
                .ToList();
        }

        private async Task<(List<int>? normalSelectedIds, List<int>? comSelectedIds)?> ShowPickGeneratedCodesDialogAsync(CouponDefinition selectedDefinition, ReceiptItem? existingItem)
        {
            await _context.Database.EnsureCreatedAsync();

            var initialSelectedIds = existingItem?.SelectedGeneratedIds ?? new List<int>();

            // IDs already chosen in other receipt items (exclude current item)
            var alreadySelectedInOtherItems = _selectedItems
                .Where(it => it != existingItem && it.SelectedGeneratedIds != null && it.SelectedGeneratedIds.Any())
                .SelectMany(it => it.SelectedGeneratedIds!)
                .Distinct()
                .ToList();

            // Load available generated coupons (include currently selected ones so user can manage them)
            var availableCodes = await _context.GeneratedCoupons
                .Where(g => g.CouponDefinitionId == selectedDefinition.Id && ((g.ReceiptItemId == null && !g.IsUsed) || initialSelectedIds.Contains(g.Id)))
                .ToListAsync();

            // sort by trailing numeric value first, then by full code
            availableCodes = availableCodes
                .OrderBy(g => ParseTrailingNumber(g.GeneratedCode))
                .ThenBy(g => g.GeneratedCode)
                .ToList();

            // UI containers
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(new TextBlock { Text = $"เลือกหมายเลขคูปองสำหรับ '{selectedDefinition.Name}'", TextWrapping = TextWrapping.Wrap });

            // Mode toggle: Auto (allocation) vs Manual (pick each)
            var modeToggle = new ToggleSwitch
            {
                Header = "โหมด: เลือกเอง (ติ๊กทีละรายการ)",
                IsOn = false,
                Margin = new Thickness(0, 6, 0, 0)
            };
            stack.Children.Add(modeToggle);

            // Controls: normal/com quantity (used only in Auto mode)
            var normalPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            normalPanel.Children.Add(new TextBlock { Text = "จำนวนที่ต้องการ:", VerticalAlignment = VerticalAlignment.Center });
            var normalQuantityBox = new NumberBox
            {
                Minimum = 0,
                Maximum = Math.Max(0, availableCodes.Count),
                Value = 0,
                Width = 220,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            normalPanel.Children.Add(normalQuantityBox);
            stack.Children.Add(normalPanel);

            var comPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            comPanel.Children.Add(new TextBlock { Text = "จำนวนที่ต้องการ (COM):", VerticalAlignment = VerticalAlignment.Center });
            var comQuantityBox = new NumberBox
            {
                Minimum = 0,
                Maximum = Math.Max(0, availableCodes.Count),
                Value = 0,
                Width = 220,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            comPanel.Children.Add(comQuantityBox);
            stack.Children.Add(comPanel);

            // Search box
            var searchBox = new TextBox { PlaceholderText = "ค้นหารหัส (พิมพ์แล้วรายการจะกรองอัตโนมัติ)", Margin = new Thickness(0, 8, 0, 0) };
            stack.Children.Add(searchBox);

            // Info text (counts)
            var infoText = new TextBlock { Text = $"หมายเลขที่มีทั้งหมด: {availableCodes.Count}", Margin = new Thickness(0, 6, 0, 0) };
            stack.Children.Add(infoText);

            // Scroll area for checkboxes
            var scroll = new ScrollViewer { Height = 320 };
            var resultsPanel = new StackPanel { Spacing = 2 };
            scroll.Content = resultsPanel;
            stack.Children.Add(scroll);

            // Pagination controls
            const int pageSize = 25;
            int currentPage = 1;
            int totalPages = Math.Max(1, (int)Math.Ceiling(availableCodes.Count / (double)pageSize));
            var pagingPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            var prevBtn = new Button { Content = "‹ ก่อนหน้า", MinWidth = 100 };
            var pageInfo = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
            var nextBtn = new Button { Content = "ถัดไป ›", MinWidth = 100 };
            pagingPanel.Children.Add(prevBtn);
            pagingPanel.Children.Add(pageInfo);
            pagingPanel.Children.Add(nextBtn);
            stack.Children.Add(pagingPanel);

            // Working sets and helpers
            var normalSelected = new List<int>();
            var comSelected = new List<int>();

            // Track manual toggles by user across pages
            var manualChecked = new HashSet<int>();
            var manualComChecked = new HashSet<int>();

            bool suppressCheckboxEvents = false;

            // Allocation algorithm: choose first N normal, then next M for COM, skipping alreadySelectedInOtherItems
            void AllocateByCounts(int normalCount, int comCount, string? filter)
            {
                normalSelected.Clear();
                comSelected.Clear();

                var query = availableCodes.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    query = query.Where(g => g.GeneratedCode?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                // Candidate codes exclude those already picked in other items
                var candidate = query.Where(g => !alreadySelectedInOtherItems.Contains(g.Id)).ToList();

                // Fill normal first
                foreach (var g in candidate)
                {
                    if (normalSelected.Count >= normalCount) break;
                    normalSelected.Add(g.Id);
                }

                // Fill COM from the remaining
                foreach (var g in candidate)
                {
                    if (normalSelected.Contains(g.Id)) continue;
                    if (comSelected.Count >= comCount) break;
                    comSelected.Add(g.Id);
                }
            }

            // Populate results for current page (supports Manual mode UI with per-row COM checkbox)
            void PopulatePage(string? filter)
            {
                resultsPanel.Children.Clear();

                var query = availableCodes.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    query = query.Where(g => g.GeneratedCode?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                var filtered = query.ToList();
                int totalFiltered = filtered.Count;
                totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize));
                if (currentPage > totalPages) currentPage = totalPages;
                int skip = (currentPage - 1) * pageSize;
                var displayed = filtered.Skip(skip).Take(pageSize).ToList();

                // replace the per-row creation inside PopulatePage with this mutual-exclusive logic
                foreach (var g in displayed)
                {
                    var isTakenElsewhere = alreadySelectedInOtherItems.Contains(g.Id);

                    var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

                    var selCb = new CheckBox
                    {
                        Content = g.GeneratedCode,
                        Tag = g.Id,
                        Margin = new Thickness(0, 2, 0, 2),
                        IsEnabled = !isTakenElsewhere,
                        Width = 420
                    };

                    var comCb = new CheckBox
                    {
                        Content = "COM",
                        Tag = g.Id,
                        Margin = new Thickness(0, 2, 0, 2),
                        IsEnabled = !isTakenElsewhere,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Orange)
                    };

                    // initial checked state:
                    // - manual selections (manualChecked/manualComChecked) always win
                    // - auto allocations (normalSelected/comSelected) applied only when NOT in manual mode
                    if (manualComChecked.Contains(g.Id))
                    {
                        comCb.IsChecked = true;
                        selCb.IsChecked = false;
                    }
                    else if (manualChecked.Contains(g.Id))
                    {
                        selCb.IsChecked = true;
                        comCb.IsChecked = false;
                    }
                    else if (!modeToggle.IsOn) // Auto mode: reflect allocation results
                    {
                        if (comSelected.Contains(g.Id))
                        {
                            comCb.IsChecked = true;
                            selCb.IsChecked = false;
                        }
                        else if (normalSelected.Contains(g.Id))
                        {
                            selCb.IsChecked = true;
                            comCb.IsChecked = false;
                        }
                        else
                        {
                            selCb.IsChecked = false;
                            comCb.IsChecked = false;
                        }
                    }
                    else
                    {
                        // Manual mode and not previously manualChecked: leave both unchecked
                        selCb.IsChecked = false;
                        comCb.IsChecked = false;
                    }

                    if (isTakenElsewhere)
                    {
                        selCb.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
                        comCb.IsEnabled = false;
                    }

                    // mutual-exclusion handlers with suppression flag to avoid recursion
                    selCb.Checked += (s, e) =>
                    {
                        if (suppressCheckboxEvents) return;
                        var id = (int)selCb.Tag!;
                        if (comCb.IsChecked == true)
                        {
                            suppressCheckboxEvents = true;
                            comCb.IsChecked = false;
                            suppressCheckboxEvents = false;
                        }
                        manualChecked.Add(id);
                        manualComChecked.Remove(id);
                    };
                    selCb.Unchecked += (s, e) =>
                    {
                        if (suppressCheckboxEvents) return;
                        var id = (int)selCb.Tag!;
                        manualChecked.Remove(id);
                        manualComChecked.Remove(id);
                    };

                    comCb.Checked += (s, e) =>
                    {
                        if (suppressCheckboxEvents) return;
                        var id = (int)comCb.Tag!;
                        if (selCb.IsChecked == true)
                        {
                            suppressCheckboxEvents = true;
                            selCb.IsChecked = false;
                            suppressCheckboxEvents = false;
                        }
                        manualChecked.Add(id);
                        manualComChecked.Add(id);
                    };
                    comCb.Unchecked += (s, e) =>
                    {
                        if (suppressCheckboxEvents) return;
                        var id = (int)comCb.Tag!;
                        manualComChecked.Remove(id);
                    };

                    row.Children.Add(selCb);
                    row.Children.Add(comCb);
                    resultsPanel.Children.Add(row);
                }

                pageInfo.Text = $"หน้า {currentPage} / {totalPages} ({totalFiltered} รายการ)";

                prevBtn.IsEnabled = currentPage > 1;
                nextBtn.IsEnabled = currentPage < totalPages;
            }

            // Recompute allocations and refresh current page
            void RecomputeAndRefresh()
            {
                // If manual mode is ON, we only show manual selections; allocation is disabled
                if (modeToggle.IsOn)
                {
                    suppressCheckboxEvents = true;
                    PopulatePage(searchBox.Text?.Trim());
                    suppressCheckboxEvents = false;
                    return;
                }

                var desiredNormal = (int)Math.Max(0, normalQuantityBox.Value);
                var desiredCom = (int)Math.Max(0, comQuantityBox.Value);

                // Cap combined counts to available (filtered)
                var filteredCount = string.IsNullOrWhiteSpace(searchBox.Text)
                    ? availableCodes.Count
                    : availableCodes.Count(g => g.GeneratedCode?.IndexOf(searchBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);

                if (desiredNormal + desiredCom > filteredCount)
                {
                    var overflow = desiredNormal + desiredCom - filteredCount;
                    // prefer preserving normal, reduce COM first
                    desiredCom = Math.Max(0, desiredCom - overflow);
                    if (desiredNormal + desiredCom > filteredCount)
                    {
                        desiredNormal = Math.Max(0, desiredNormal - (desiredNormal + desiredCom - filteredCount));
                    }
                }

                AllocateByCounts(desiredNormal, desiredCom, searchBox.Text?.Trim());

                // Populate page using currentPage
                suppressCheckboxEvents = true;
                PopulatePage(searchBox.Text?.Trim());
                suppressCheckboxEvents = false;
            }

            // Events
            normalQuantityBox.ValueChanged += (s, e) =>
            {
                if (double.IsNaN(normalQuantityBox.Value) || normalQuantityBox.Value < 0) normalQuantityBox.Value = 0;
                currentPage = 1;
                RecomputeAndRefresh();
            };
            comQuantityBox.ValueChanged += (s, e) =>
            {
                if (double.IsNaN(comQuantityBox.Value) || comQuantityBox.Value < 0) comQuantityBox.Value = 0;
                currentPage = 1;
                RecomputeAndRefresh();
            };
            searchBox.TextChanged += (s, e) =>
            {
                currentPage = 1;
                DispatcherQueue.TryEnqueue(() => RecomputeAndRefresh());
            };

            modeToggle.Toggled += (s, e) =>
            {
                // disable quantity inputs in manual mode, enable in auto mode
                normalQuantityBox.IsEnabled = !modeToggle.IsOn;
                comQuantityBox.IsEnabled = !modeToggle.IsOn;

                // Clear selections and reset quantity inputs when mode changes
                manualChecked.Clear();
                manualComChecked.Clear();

                // Reset quantity boxes to defaults (0)
                suppressCheckboxEvents = true;
                normalQuantityBox.Value = 0;
                comQuantityBox.Value = 0;
                suppressCheckboxEvents = false;

                currentPage = 1;
                RecomputeAndRefresh();
            };

            prevBtn.Click += (s, e) =>
            {
                if (currentPage > 1) currentPage--;
                RecomputeAndRefresh();
                // scroll to top of list
                resultsPanel.UpdateLayout();
                var sv = FindScrollViewer(resultsPanel);
                sv?.ChangeView(null, 0, null, disableAnimation: true);
            };
            nextBtn.Click += (s, e) =>
            {
                if (currentPage < totalPages) currentPage++;
                RecomputeAndRefresh();
                resultsPanel.UpdateLayout();
                var sv = FindScrollViewer(resultsPanel);
                sv?.ChangeView(null, 0, null, disableAnimation: true);
            };

            // Initialize
            RecomputeAndRefresh();

            // Wrap whole dialog content in a ScrollViewer so the dialog becomes scrollable
            var outerScroll = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = Math.Min(this.XamlRoot.Size.Height * 0.85, 900)
            };

            var dialog = new ContentDialog
            {
                Title = "เลือกหมายเลขคูปอง",
                Content = outerScroll,
                PrimaryButtonText = "ตกลง",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // If manual mode -> final selections come from manualChecked/manualComChecked
                if (modeToggle.IsOn)
                {
                    var finalManual = manualChecked.Except(alreadySelectedInOtherItems).ToList();
                    var finalComManual = manualComChecked.Except(alreadySelectedInOtherItems).ToList();
                    // Ensure COMs are included in selection
                    foreach (var id in finalComManual)
                    {
                        if (!finalManual.Contains(id)) finalManual.Add(id);
                    }
                    // Split finalManual into normal vs com by using finalComManual membership
                    var finalNormalManual = finalManual.Except(finalComManual).ToList();
                    return (finalNormalManual, finalComManual);
                }

                // Auto mode: process manualChecked as overrides (user manually toggled some items)
                var manualCheckedList = manualChecked.ToList();

                var desiredNormal = (int)Math.Max(0, normalQuantityBox.Value);
                var desiredCom = (int)Math.Max(0, comQuantityBox.Value);

                // Start with computed sets
                var finalNormal = new List<int>(normalSelected);
                var finalCom = new List<int>(comSelected);

                // Incorporate manual checked: try to add them to finalNormal first (but not if already in finalCom)
                foreach (var id in manualCheckedList)
                {
                    if (alreadySelectedInOtherItems.Contains(id)) continue;
                    if (finalCom.Contains(id)) continue;
                    if (!finalNormal.Contains(id) && finalNormal.Count < desiredNormal)
                        finalNormal.Add(id);
                }

                // Fill normal to desired (from filtered candidates)
                var candidates = availableCodes.Select(g => g.Id).Except(alreadySelectedInOtherItems).ToList();
                foreach (var id in candidates)
                {
                    if (finalNormal.Count >= desiredNormal) break;
                    if (!finalNormal.Contains(id) && !finalCom.Contains(id))
                        finalNormal.Add(id);
                }

                // Fill COM to desired from remaining
                foreach (var id in candidates)
                {
                    if (finalCom.Count >= desiredCom) break;
                    if (!finalCom.Contains(id) && !finalNormal.Contains(id))
                        finalCom.Add(id);
                }

                return (finalNormal, finalCom);
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

            var selectedSalesPerson = SalesPersonComboBox.SelectedItem as SalesPerson;
            if (selectedSalesPerson == null || selectedSalesPerson.ID == 0)
            {
                await ShowErrorDialog("กรุณาเลือกเซลล์ก่อนบันทึกใบเสร็จ");
                return;
            }

            var selectedPaymentMethod = GetSelectedPaymentMethod();
            if (selectedPaymentMethod == null)
            {
                PaymentMethodErrorText.Visibility = Visibility.Visible;
                await ShowErrorDialog("กรุณาเลือกวิธีการชำระเงินก่อนบันทึกใบเสร็จ");
                return;
            }

            // ตรวจสอบความพร้อมแบบ batch (เหมือนเดิม)...
            var selectedDefinitionIds = _selectedItems.Select(i => i.CouponDefinition.Id).Distinct().ToList();
            var defCounts = await _context.CouponDefinitions
                .Where(cd => selectedDefinitionIds.Contains(cd.Id))
                .Select(cd => new
                {
                    cd.Id,
                    cd.Name,
                    cd.IsLimited,
                    TotalGenerated = cd.GeneratedCoupons.Count(),
                    TotalAllocatedOrUsed = cd.GeneratedCoupons.Count(gc => gc.IsUsed || gc.ReceiptItemId != null)
                })
                .ToListAsync();
            var defCountsMap = defCounts.ToDictionary(d => d.Id);

            foreach (var item in _selectedItems)
            {
                if (!defCountsMap.TryGetValue(item.CouponDefinition.Id, out var defInfo))
                {
                    await ShowErrorDialog($"ไม่พบข้อมูลคูปอง '{item.CouponDefinition.Name}' (id:{item.CouponDefinition.Id})");
                    return;
                }

                if (defInfo.IsLimited)
                {
                    var availableCount = defInfo.TotalGenerated - defInfo.TotalAllocatedOrUsed;
                    if (availableCount < 0) availableCount = 0;

                    if (item.Quantity > availableCount)
                    {
                        await ShowErrorDialog($"คูปอง '{defInfo.Name}' มีคงเหลือ {availableCount} ใบ ไม่เพียงพอสำหรับจำนวนที่เลือก ({item.Quantity} ใบ)");
                        return;
                    }
                }
            }

            // สร้าง dialog รับข้อมูลลูกค้า + ช่องกรอกส่วนลดเพิ่มเติม (เหมือนเดิม)
            var customerPanel = new StackPanel { Spacing = 10 };

            customerPanel.Children.Add(new TextBlock { Text = "ชื่อลูกค้า:" });
            var customerNameBox = new TextBox { PlaceholderText = "กรุณาระบุชื่อลูกค้า" };
            customerPanel.Children.Add(customerNameBox);

            customerPanel.Children.Add(new TextBlock { Text = "เบอร์โทรศัพท์:" });
            var phoneNumberBox = new TextBox { PlaceholderText = "กรุณาระบุเบอร์โทรศัพท์" };
            customerPanel.Children.Add(phoneNumberBox);

            // คำนวณส่วนลดจาก COM (สำหรับแสดงและคำนวณยอดรวม)
            decimal comDiscount = 0m;
            foreach (var item in _selectedItems)
            {
                if (item.IsCOM)
                    comDiscount += item.CouponDefinition.Price * item.Quantity;
            }

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

            customerPanel.Children.Add(new TextBlock { Text = "ส่วนลดเพิ่มเติม (ถ้ามี):", Margin = new Thickness(0, 10, 0, 0) });
            var additionalDiscountBox = new NumberBox
            {
                Value = 0,
                Minimum = 0,
                Maximum = (double)_selectedItems.Sum(item => item.TotalPrice),
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                PlaceholderText = "0.00"
            };
            customerPanel.Children.Add(additionalDiscountBox);

            var totalDiscountText = new TextBlock
            {
                Text = $"ส่วนลดรวม: {comDiscount:N2} บาท",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green),
                Margin = new Thickness(0, 10, 0, 0)
            };
            customerPanel.Children.Add(totalDiscountText);

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
            if (result != ContentDialogResult.Primary) return;

            string customerName = customerNameBox.Text.Trim();
            string phoneNumber = phoneNumberBox.Text.Trim();

            if (string.IsNullOrEmpty(customerName) || string.IsNullOrEmpty(phoneNumber))
            {
                await ShowErrorDialog("กรุณากรอกข้อมูลลูกค้าให้ครบถ้วน");
                return;
            }

            var additionalDiscountVal = double.IsNaN(additionalDiscountBox.Value) ? 0.0 : additionalDiscountBox.Value;
            if (additionalDiscountVal < 0)
            {
                await ShowErrorDialog("ส่วนลดต้องเป็นค่าบวกหรือเท่ากับศูนย์");
                return;
            }

            // เก็บเฉพาะส่วนลดเพิ่มเติมในตัวแปร _receiptDiscount
            _receiptDiscount = (decimal)additionalDiscountVal;

            // ✅ คำนวณ gross subtotal (ราคาเต็มรวมทั้ง COM)
            var grossSubtotal = _selectedItems.Sum(item => item.CouponDefinition.Price * item.Quantity);

            Debug.WriteLine($"📊 การคำนวณยอดรวมก่อนบันทึก:");
            Debug.WriteLine($"   - Gross Subtotal (รวมทั้ง COM): {grossSubtotal:N2}");
            Debug.WriteLine($"   - COM Discount: {comDiscount:N2}");
            Debug.WriteLine($"   - Additional Discount: {_receiptDiscount:N2}");
            Debug.WriteLine($"   - Net Total (ที่ลูกค้าจ่าย): {grossSubtotal - comDiscount - _receiptDiscount:N2}");

            // Generate receipt code
            string receiptCode;
            try
            {
                receiptCode = await ReceiptNumberService.GenerateNextReceiptCodeAsync();
            }
            catch (Exception genEx)
            {
                var detail = genEx.InnerException != null ? genEx.InnerException.Message : genEx.Message;
                await ShowErrorDialog($"เกิดข้อผิดพลาด: ไม่สามารถสร้างหมายเลขใบเสร็จได้\n\nรายละเอียด: {detail}");
                return;
            }

            // Build a ReceiptModel
            var receipt = new ReceiptModel
            {
                ReceiptCode = receiptCode,
                ReceiptDate = DateTime.Now,
                CustomerName = customerName,
                CustomerPhoneNumber = phoneNumber,
                Discount = _receiptDiscount,     // ส่วนลดเพิ่มเติมจาก UI
                TotalAmount = grossSubtotal,     // ✅ gross subtotal รวมทั้ง COM (2,400)
                SalesPersonId = (SalesPersonComboBox.SelectedItem as SalesPerson)?.ID,
                PaymentMethodId = GetSelectedPaymentMethod()?.Id
            };

            // Call stored procedure wrapper
            int createdReceiptId = 0;
            try
            {
                createdReceiptId = await SaveReceiptViaStoredProcAsync(receipt, _selectedItems.ToList());
            }
            catch (Exception ex)
            {
                // Recycle receipt code and show error
                try { await ReceiptNumberService.RecycleReceiptCodeAsync(receiptCode, "SP failed: " + ex.Message); } catch { }
                await ShowErrorDialog($"เกิดข้อผิดพลาดในการบันทึกใบเสร็จ: {ex.Message}");
                return;
            }

            if (createdReceiptId <= 0)
            {
                try { await ReceiptNumberService.RecycleReceiptCodeAsync(receiptCode, "SP returned invalid id"); } catch { }
                await ShowErrorDialog("ไม่สามารถสร้างใบเสร็จได้ (ไม่ได้รับหมายเลขจากฐานข้อมูล)");
                return;
            }

            // Remove reservations made by this session (best-effort; SP may already handle this)
            try
            {
                var reservations = _context.ReservedCoupons.Where(r => r.SessionId == _reservationSessionId);
                _context.ReservedCoupons.RemoveRange(reservations);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clear reservations: {ex.Message}");
                // not fatal
            }

            // Success: notify user & printing flow
            // Pass comDiscount to the print confirmation so the PrintService can show COM in discount display (but COM is NOT saved in DB)
            await ShowPrintConfirmationDialog(createdReceiptId, receiptCode, selectedPaymentMethod.Name, comDiscount);
            _receiptDiscount = 0m;
            _selectedItems.Clear();
            ClearPaymentMethodSelection();
            UpdateTotalPrice();
            await PerformSearch();
        }

        // C# helper that calls the stored procedure using a TVP.
        private async Task<int> SaveReceiptViaStoredProcAsync(ReceiptModel receiptModel, List<ReceiptItem> items)
        {
            var table = new DataTable();
            table.Columns.Add("CouponDefinitionId", typeof(int));
            table.Columns.Add("Quantity", typeof(int));
            table.Columns.Add("UnitPrice", typeof(decimal));
            table.Columns.Add("IsCOM", typeof(bool));
            table.Columns.Add("SelectedGeneratedIds", typeof(string));

            foreach (var it in items)
            {
                var selectedCsv = (it.SelectedGeneratedIds != null && it.SelectedGeneratedIds.Any())
                    ? string.Join(',', it.SelectedGeneratedIds)
                    : null;
                table.Rows.Add(it.CouponDefinition.Id, it.Quantity, it.CouponDefinition.Price, it.IsCOM, (object)selectedCsv! ?? DBNull.Value);
            }

            var conn = _context.Database.GetDbConnection();
            try
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "dbo.sp_CreateReceipt";
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add(new SqlParameter("@ReceiptCode", SqlDbType.NVarChar, 50) { Value = receiptModel.ReceiptCode ?? string.Empty });
                cmd.Parameters.Add(new SqlParameter("@ReceiptDate", SqlDbType.DateTime2) { Value = receiptModel.ReceiptDate });
                cmd.Parameters.Add(new SqlParameter("@CustomerName", SqlDbType.NVarChar, 200) { Value = receiptModel.CustomerName ?? string.Empty });
                cmd.Parameters.Add(new SqlParameter("@CustomerPhoneNumber", SqlDbType.NVarChar, 50) { Value = receiptModel.CustomerPhoneNumber ?? string.Empty });

                // IMPORTANT: ส่ง @Discount เป็น NULL ถ้าไม่มีส่วนลดเพิ่มเติม (ผู้ใช้ไม่ได้กรอก)
                var discountParam = new SqlParameter("@Discount", SqlDbType.Decimal)
                {
                    Precision = 18,
                    Scale = 2,
                    // Always send a numeric value (0 when no additional discount was entered).
                    Value = receiptModel.Discount
                };
                cmd.Parameters.Add(discountParam);

                cmd.Parameters.Add(new SqlParameter("@TotalAmount", SqlDbType.Decimal) { Value = receiptModel.TotalAmount, Precision = 18, Scale = 2 });

                cmd.Parameters.Add(new SqlParameter("@SalesPersonId", SqlDbType.Int) { Value = receiptModel.SalesPersonId.HasValue ? (object)receiptModel.SalesPersonId.Value : DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@PaymentMethodId", SqlDbType.Int) { Value = receiptModel.PaymentMethodId.HasValue ? (object)receiptModel.PaymentMethodId.Value : DBNull.Value });

                var tvp = new SqlParameter("@Items", SqlDbType.Structured)
                {
                    TypeName = "dbo.ReceiptItemType",
                    Value = table
                };
                cmd.Parameters.Add(tvp);

                var outParam = new SqlParameter("@CreatedReceiptId", SqlDbType.Int) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(outParam);

                if (cmd is SqlCommand sqlCmd)
                {
                    await sqlCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    using var sqlCommand = new SqlCommand(cmd.CommandText, (SqlConnection)conn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    foreach (SqlParameter p in cmd.Parameters)
                        sqlCommand.Parameters.Add(p);
                    await sqlCommand.ExecuteNonQueryAsync();
                }

                var createdId = outParam.Value == DBNull.Value ? 0 : (int)outParam.Value;
                return createdId;
            }
            finally
            {
                try { if (conn.State == ConnectionState.Open) await conn.CloseAsync(); } catch { }
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
            // คำนวณราคารวมก่อนลด (รวมทุกรายการ รวมถึง COM)
            decimal subtotal = _selectedItems.Sum(item => item.TotalPrice);

            // คำนวณส่วนลดรวม: ราคาของรายการ COM + ส่วนลดเพิ่มเติม
            decimal comDiscount = _selectedItems
                .Where(item => item.IsCOM)
                .Sum(item => item.TotalPrice);

            decimal totalDiscount = comDiscount + _receiptDiscount;

            // ราคาสุทธิ = ราคารวมก่อนลด - ส่วนลดรวม
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
        private async Task ShowPrintConfirmationDialog(int receiptId, string receiptCode, string paymentMethodName, decimal comDiscount)
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
                    bool printSuccess = await ReceiptPrintService.PrintReceiptAsync(receiptId, this.XamlRoot, comDiscount);
                    if (printSuccess)
                    {
                        Debug.WriteLine("เรียกใช้งานการพิมพ์สำเร็จ");
                    }

                    return; // printed -> exit
                }

                // กดปุ่มยกเลิก - ขอเหตุผลการยกเลิกการพิมพ์ก่อน และเก็บหมายเลขใบเสร็จเพื่อรีไซเคิลเฉพาะเครื่องนี้
                while (true)
                {
                    var reasonPanel = new StackPanel { Spacing = 8 };
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

                void Recalc()
                {
                    var q = quantityBox.Value > 0 ? (int)quantityBox.Value : 0;
                    var total = selectedItem.CouponDefinition.Price * (decimal)q;
                    if (total < 0) total = 0;
                    totalPriceTextBlock.Text = $"รวมเป็นเงิน: {total} บาท";
                }

                quantityBox.ValueChanged += (s, args) => Recalc();

                var contentPanel = new StackPanel();
                contentPanel.Children.Add(new TextBlock { Text = $"คูปอง: {selectedItem.CouponDefinition.Name}" });
                contentPanel.Children.Add(new TextBlock { Text = $"ราคา: {selectedItem.CouponDefinition.Price} บาท/ใบ", Margin = new Thickness(0, 6, 0, 6) });
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

                    int index = _selectedItems.IndexOf(selectedItem);
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