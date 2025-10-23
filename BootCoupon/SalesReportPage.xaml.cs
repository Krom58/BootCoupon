using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics; // เพิ่มบรรทัดนี้
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Text;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml.Printing;
using Windows.Graphics.Printing;
using Microsoft.UI.Xaml.Documents;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;

namespace BootCoupon
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SalesReportPage : Page
    {
        public SalesReportViewModel ViewModel { get; } = new SalesReportViewModel();
        // (Report mode changes now apply immediately and trigger search)

        public SalesReportPage()
        {
            this.InitializeComponent();
            // constructor
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            // apply initial visual state after components loaded
            this.Loaded += (_, __) => UpdateReportModeButtonVisuals();
        }

        private async void ReportModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                if (tag == "ByReceipt") ViewModel.ReportMode = SalesReportViewModel.ReportModes.ByReceipt;
                else if (tag == "LimitedCoupons") ViewModel.ReportMode = SalesReportViewModel.ReportModes.LimitedCoupons;
                else if (tag == "UnlimitedGrouped") ViewModel.ReportMode = SalesReportViewModel.ReportModes.UnlimitedGrouped;
                else if (tag == "SummaryByCoupon") ViewModel.ReportMode = SalesReportViewModel.ReportModes.SummaryByCoupon;

                await ViewModel.SearchDataAsync();

                UpdateReportModeButtonVisuals();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.ReportMode))
            {
                // Ensure update on UI thread
                DispatcherQueue.TryEnqueue(() => { UpdateReportModeButtonVisuals(); });
            }
        }

        private void UpdateReportModeButtonVisuals()
        {
            try
            {
                // default style
                var normalBg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                var normalFg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                var normalBorder = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

                // active style
                var activeBg = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlBackgroundAccentBrush"];
                var activeFg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                var activeBorder = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];

                // reset all
                void ApplyNormal(Button b)
                {
                    if (b == null) return;
                    b.Background = normalBg;
                    b.Foreground = normalFg;
                    b.BorderBrush = normalBorder;
                }

                void ApplyActive(Button b)
                {
                    if (b == null) return;
                    b.Background = activeBg;
                    b.Foreground = activeFg;
                    b.BorderBrush = activeBorder;
                }

                ApplyNormal(ByReceiptButton);
                ApplyNormal(LimitedCouponsButton);
                ApplyNormal(UnlimitedGroupedButton);
                ApplyNormal(SummaryByCouponButton);

                switch (ViewModel.ReportMode)
                {
                    case SalesReportViewModel.ReportModes.ByReceipt:
                        ApplyActive(ByReceiptButton);
                        break;
                    case SalesReportViewModel.ReportModes.LimitedCoupons:
                        ApplyActive(LimitedCouponsButton);
                        break;
                    case SalesReportViewModel.ReportModes.UnlimitedGrouped:
                        ApplyActive(UnlimitedGroupedButton);
                        break;
                    case SalesReportViewModel.ReportModes.SummaryByCoupon:
                        ApplyActive(SummaryByCouponButton);
                        break;
                }
            }
            catch { }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.LoadDataAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        private void CouponTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.UpdateFilteredCoupons();
        }

        // Quick Date Buttons
        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.StartDate = DateTime.Today;
            ViewModel.EndDate = DateTime.Today;
        }

        private void Last7DaysButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.StartDate = DateTime.Today.AddDays(-7);
            ViewModel.EndDate = DateTime.Today;
        }

        private void Last30DaysButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.StartDate = DateTime.Today.AddDays(-30);
            ViewModel.EndDate = DateTime.Today;
        }

        private void ThisMonthButton_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            ViewModel.StartDate = new DateTime(today.Year, today.Month, 1);
            ViewModel.EndDate = today;
        }

        private void LastMonthButton_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            var firstDayLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            var lastDayLastMonth = firstDayLastMonth.AddMonths(1).AddDays(-1);
            ViewModel.StartDate = firstDayLastMonth;
            ViewModel.EndDate = lastDayLastMonth;
        }

        private void ThisYearButton_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            ViewModel.StartDate = new DateTime(today.Year, 1, 1);
            ViewModel.EndDate = today;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.SearchDataAsync();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearFilters();
        }

        private async void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if there's data to export
                if (!ViewModel.HasData)
                {
                    await ShowErrorDialog("ไม่มีข้อมูลให้ส่งออก กรุณาค้นหาข้อมูลก่อน");
                    return;
                }

                var savePicker = new FileSavePicker();
                
                // Generate filename with filter information
                var filterInfo = ViewModel.GetFilterSummary();
                var baseFileName = $"รายงานการขาย_{DateTime.Now:yyyyMMdd_HHmmss}";
                if (!string.IsNullOrEmpty(filterInfo))
                {
                    baseFileName += $"_{filterInfo}";
                }
                
                savePicker.SuggestedFileName = $"{baseFileName}.csv";
                savePicker.FileTypeChoices.Add("CSV Files", new List<string> { ".csv" });
                
                var hwnd = WindowNative.GetWindowHandle(MainWindow.Instance);
                InitializeWithWindow.Initialize(savePicker, hwnd);

                var file = await savePicker.PickSaveFileAsync();
                if (file == null) return;

                await ViewModel.ExportToCsvAsync(file);
                
                var successMessage = $"ส่งออกข้อมูลเรียบร้อยแล้ว\n\n" +
                                   $"ไฟล์: {file.Name}\n" +
                                   $"จำนวนรายการ: {ViewModel.ReportData.Count:N0} รายการ\n" +
                                   $"ยอดรวม: {ViewModel.ReportData.Sum(x => x.TotalPrice):N2} บาท\n" +
                                   $"ช่วงวันที่: {ViewModel.StartDate?.ToString("dd/MM/yyyy")} - {ViewModel.EndDate?.ToString("dd/MM/yyyy")}";

                await ShowSuccessDialog(successMessage);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"เกิดข้อผิดพลาดในการส่งออกข้อมูล: {ex.Message}");
            }
        }

        private async void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            // Trigger Print UI which can select Print to PDF
            try
            {
                // Check if there's data to export
                if (!ViewModel.HasData)
                {
                    await ShowErrorDialog("ไม่มีข้อมูลให้ส่งออก กรุณาค้นหาข้อมูลก่อน");
                    return;
                }

                // ใช้ SalesReportPrintService แทนการพิมพ์แบบเดิม
                bool printSuccess = await SalesReportPrintService.PrintSalesReportAsync(ViewModel, this.XamlRoot);

                if (printSuccess)
                {
                    Debug.WriteLine("ส่งออก PDF/พิมพ์รายงานเรียบร้อย");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"เกิดข้อผิดพลาดในการส่งออก PDF: {ex.Message}");
            }
        }

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if there's data to print
                if (!ViewModel.HasData)
                {
                    await ShowErrorDialog("ไม่มีข้อมูลให้พิมพ์ กรุณาค้นหาข้อมูลก่อน");
                    return;
                }

                // ใช้ SalesReportPrintService แทนการพิมพ์แบบเดิม
                bool printSuccess = await SalesReportPrintService.PrintSalesReportAsync(ViewModel, this.XamlRoot);

                if (printSuccess)
                {
                    Debug.WriteLine("เรียกใช้งานการพิมพ์รายงานสำเร็จ");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"เกิดข้อผิดพลาดในการพิมพ์: {ex.Message}");
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
                Title = "แจ้งเตือน",
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

    // ViewModel for the Sales Report Page
    public class SalesReportViewModel : INotifyPropertyChanged
    {
        public enum ReportModes { ByReceipt, LimitedCoupons, UnlimitedGrouped, SummaryByCoupon }
        private ReportModes _reportMode = ReportModes.ByReceipt;
        public ReportModes ReportMode
        {
            get => _reportMode;
            set
            {
                if (_reportMode != value)
                {
                    _reportMode = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTimeOffset? _startDate = DateTime.Today.AddDays(-30);
        private DateTimeOffset? _endDate = DateTime.Today;
        private SalesPerson? _selectedSalesPerson;
        private CouponType? _selectedCouponType;
        private CouponDefinition? _selectedCoupon; // changed to CouponDefinition
        private PaymentMethod? _selectedPaymentMethod;
        private ObservableCollection<SalesReportItem> _reportData = new();
        private ObservableCollection<CouponDefinition> _filteredCoupons = new(); // changed type
        private bool _isUpdatingFilters = false;
        private bool _isInitialLoad = true;

        public DateTimeOffset? StartDate
        {
            get => _startDate;
            set 
            { 
                if (_startDate != value)
                {
                    _startDate = value; 
                    OnPropertyChanged();
                    _ = TriggerAutoSearchAsync();
                }
            }
        }

        public DateTimeOffset? EndDate
        {
            get => _endDate;
            set 
            { 
                if (_endDate != value)
                {
                    _endDate = value; 
                    OnPropertyChanged();
                    _ = TriggerAutoSearchAsync();
                }
            }
        }

        public SalesPerson? SelectedSalesPerson
        {
            get => _selectedSalesPerson;
            set 
            { 
                if (_selectedSalesPerson != value)
                {
                    _selectedSalesPerson = value; 
                    OnPropertyChanged();
                    _ = TriggerAutoSearchAsync();
                }
            }
        }

        public CouponType? SelectedCouponType
        {
            get => _selectedCouponType;
            set 
            { 
                if (_selectedCouponType != value)
                {
                    _selectedCouponType = value; 
                    OnPropertyChanged(); 
                    UpdateFilteredCoupons();
                    _ = TriggerAutoSearchAsync();
                }
            }
        }

        public CouponDefinition? SelectedCoupon // changed
        {
            get => _selectedCoupon;
            set 
            { 
                if (_selectedCoupon != value)
                {
                    _selectedCoupon = value; 
                    OnPropertyChanged();
                    _ = TriggerAutoSearchAsync();
                }
            }
        }

        public PaymentMethod? SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set 
            { 
                if (_selectedPaymentMethod != value)
                {
                    _selectedPaymentMethod = value; 
                    OnPropertyChanged();
                    _ = TriggerAutoSearchAsync();
                }
            }
        }

        public ObservableCollection<SalesReportItem> ReportData
        {
            get => _reportData;
            set { _reportData = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasData)); UpdateSummary(); }
        }

        public ObservableCollection<SalesPerson> SalesPersons { get; } = new();
        public ObservableCollection<CouponType> CouponTypes { get; } = new();
        public ObservableCollection<CouponDefinition> AllCoupons { get; } = new(); // changed
        public ObservableCollection<CouponDefinition> FilteredCoupons
        {
            get => _filteredCoupons;
            set { _filteredCoupons = value; OnPropertyChanged(); }
        }
        public ObservableCollection<PaymentMethod> PaymentMethods { get; } = new();

        public bool HasData => ReportData.Count > 0;

        public string TotalRecordsText { get; private set; } = "ไม่มีข้อมูล";
        public string TotalAmountText { get; private set; } = "";

        public async Task LoadDataAsync()
        {
            try
            {
                _isInitialLoad = true;
                
                using var context = new CouponContext();

                // Load SalesPersons with "ทั้งหมด" option
                var salesPersons = await context.SalesPerson.ToListAsync();
                SalesPersons.Clear();
                // เพิ่มตัวเลือก "ทั้งหมด" เป็นรายการแรก
                SalesPersons.Add(new SalesPerson { ID = 0, Name = "ทั้งหมด", Branch = "", Telephone = "" });
                foreach (var sp in salesPersons)
                    SalesPersons.Add(sp);

                // Load CouponTypes with "ทั้งหมด" option
                var couponTypes = await context.CouponTypes.ToListAsync();
                CouponTypes.Clear();
                // เพิ่มตัวเลือก "ทั้งหมด" เป็นรายการแรก
                CouponTypes.Add(new CouponType { Id = 0, Name = "ทั้งหมด" });
                foreach (var ct in couponTypes)
                    CouponTypes.Add(ct);

                // Load CouponDefinitions instead of legacy Coupons
                var couponDefinitions = await context.CouponDefinitions.Include(cd => cd.CouponType).ToListAsync();
                AllCoupons.Clear();
                foreach (var c in couponDefinitions)
                    AllCoupons.Add(c);

                UpdateFilteredCoupons();

                // Load PaymentMethods with "ทั้งหมด" option
                var paymentMethods = await context.PaymentMethods.Where(pm => pm.IsActive).ToListAsync();
                PaymentMethods.Clear();
                // เพิ่มตัวเลือก "ทั้งหมด" เป็นรายการแรก
                PaymentMethods.Add(new PaymentMethod { Id = 0, Name = "ทั้งหมด", IsActive = true, CreatedDate = DateTime.Now });
                foreach (var pm in paymentMethods)
                    PaymentMethods.Add(pm);

                _isInitialLoad = false;
                
                // Perform initial search
                await SearchDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
                _isInitialLoad = false;
            }
        }

        private async Task TriggerAutoSearchAsync()
        {
            if (_isInitialLoad || _isUpdatingFilters) return;
            
            try
            {
                await SearchDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in auto search: {ex.Message}");
            }
        }

        public void UpdateFilteredCoupons()
        {
            if (_isUpdatingFilters) return;
            
            try
            {
                _isUpdatingFilters = true;

                var newFilteredCoupons = new List<CouponDefinition>();
                
                // เพิ่มตัวเลือก "ทั้งหมด" เป็นรายการแรก
                newFilteredCoupons.Add(new CouponDefinition { Id = 0, Name = "ทั้งหมด", Price = 0, Code = "", CouponTypeId = 0 });
                
                var filtered = SelectedCouponType == null || SelectedCouponType.Id == 0 ? 
                    AllCoupons : 
                    AllCoupons.Where(c => c.CouponTypeId == SelectedCouponType.Id);

                newFilteredCoupons.AddRange(filtered);

                FilteredCoupons.Clear();
                foreach (var coupon in newFilteredCoupons)
                    FilteredCoupons.Add(coupon);

                // Reset selected coupon if it's not in the filtered list
                if (SelectedCoupon != null && !newFilteredCoupons.Contains(SelectedCoupon))
                {
                    _selectedCoupon = null;
                    OnPropertyChanged(nameof(SelectedCoupon));
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }
        }

        public async Task SearchDataAsync()
        {
            try
            {
                using var context = new CouponContext();
                var startDateTime = (StartDate?.DateTime ?? DateTime.Today.AddDays(-30)).Date;
                var endDateTime = (EndDate?.DateTime ?? DateTime.Today).Date.AddDays(1);

                // Base query for receipt items joined with coupon definition and related data
                var baseQuery = from r in context.Receipts
                                where r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime && r.Status == "Active"
                                from item in r.Items
                                join c in context.CouponDefinitions on item.CouponId equals c.Id into cj
                                from c in cj.DefaultIfEmpty()
                                join ct in context.CouponTypes on c.CouponTypeId equals ct.Id into ctj
                                from ct in ctj.DefaultIfEmpty()
                                join sp in context.SalesPerson on r.SalesPersonId equals sp.ID into spj
                                from sp in spj.DefaultIfEmpty()
                                join pm in context.PaymentMethods on r.PaymentMethodId equals pm.Id into pmj
                                from pm in pmj.DefaultIfEmpty()
                                join gc in context.GeneratedCoupons on item.ReceiptItemId equals gc.ReceiptItemId into gcj
                                from gc in gcj.DefaultIfEmpty()
                                select new
                                {
                                    ReceiptItemId = item.ReceiptItemId,
                                     ReceiptDate = r.ReceiptDate,
                                     ReceiptCode = r.ReceiptCode,
                                     CustomerName = r.CustomerName,
                                     SalesPersonName = sp != null ? sp.Name : null,
                                     CouponId = c != null ? c.Id : 0,
                                     CouponName = c != null ? c.Name : null,
                                     CouponTypeId = ct != null ? ct.Id : 0,
                                     CouponTypeName = ct != null ? ct.Name : null,
                                     IsLimited = c != null ? c.IsLimited : false,
                                     PaymentMethodName = pm != null ? pm.Name : null,
                                     Quantity = item.Quantity,
                                     UnitPrice = item.UnitPrice,
                                     TotalPrice = item.TotalPrice,
                                     GeneratedCouponId = gc != null ? gc.Id : 0,
                                     GeneratedCode = gc != null ? gc.GeneratedCode : null
                                 };

                // Apply simple filters before shaping
                if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
                    baseQuery = baseQuery.Where(x => x.SalesPersonName == SelectedSalesPerson.Name);
                if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
                    baseQuery = baseQuery.Where(x => x.PaymentMethodName == SelectedPaymentMethod.Name);
                if (SelectedCouponType != null && SelectedCouponType.Id != 0)
                    baseQuery = baseQuery.Where(x => x.CouponTypeId == SelectedCouponType.Id);
                if (SelectedCoupon != null && SelectedCoupon.Id != 0)
                    baseQuery = baseQuery.Where(x => x.CouponId == SelectedCoupon.Id);

                List<SalesReportItem> results = new();

                if (ReportMode == ReportModes.ByReceipt)
                {
                    // Return one row per receipt item (each item on its own row)
                    var raw = await baseQuery.ToListAsync();
                    // deduplicate by ReceiptItemId (if multiple GeneratedCoupons caused duplicates)
                    var unique = raw.GroupBy(x => x.ReceiptItemId).Select(g => g.First()).ToList();
                    results = unique.Select(item => new SalesReportItem
                    {
                        ReceiptDate = item.ReceiptDate,
                        ReceiptCode = item.ReceiptCode,
                        CustomerName = item.CustomerName,
                        SalesPersonName = item.SalesPersonName ?? "ไม่ระบุ",
                        CouponName = item.CouponName ?? "ไม่พบข้อมูล",
                        CouponTypeName = item.CouponTypeName ?? "ไม่พบข้อมูล",
                        PaymentMethodName = item.PaymentMethodName ?? "ไม่ระบุ",
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.TotalPrice
                    }).OrderBy(x => x.ReceiptDate).ToList();
                }
                else if (ReportMode == ReportModes.LimitedCoupons)
                {
                    // Pull used generated coupons joined to receipts so we can show generated code + customer
                    var gcQuery = from gc in context.GeneratedCoupons
                                  where gc.IsUsed && gc.ReceiptItemId != null
                                  join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                                  join r in context.Receipts on ri.ReceiptId equals r.ReceiptID
                                  join cd in context.CouponDefinitions on gc.CouponDefinitionId equals cd.Id into cdj
                                  from cd in cdj.DefaultIfEmpty()
                                  join ct in context.CouponTypes on cd.CouponTypeId equals ct.Id into ctj
                                  from ct in ctj.DefaultIfEmpty()
                                  join sp in context.SalesPerson on r.SalesPersonId equals sp.ID into spj
                                  from sp in spj.DefaultIfEmpty()
                                  join pm in context.PaymentMethods on r.PaymentMethodId equals pm.Id into pmj
                                  from pm in pmj.DefaultIfEmpty()
                                  where r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime && r.Status == "Active"
                                  select new
                                  {
                                      ReceiptDate = r.ReceiptDate,
                                      ReceiptCode = r.ReceiptCode,
                                      CustomerName = r.CustomerName,
                                      SalesPersonName = sp != null ? sp.Name : null,
                                      CouponDefinitionId = cd != null ? cd.Id : 0,
                                      CouponName = cd != null ? cd.Name : null,
                                      CouponTypeName = ct != null ? ct.Name : null,
                                      GeneratedCode = gc.GeneratedCode,
                                      UnitPrice = cd != null ? cd.Price : 0m,
                                      TotalPrice = cd != null ? cd.Price : 0m
                                  };

                    // apply filters
                    if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
                        gcQuery = gcQuery.Where(x => x.SalesPersonName == SelectedSalesPerson.Name);
                    if (SelectedCouponType != null && SelectedCouponType.Id != 0)
                        gcQuery = gcQuery.Where(x => x.CouponTypeName == SelectedCouponType.Name || x.CouponDefinitionId == SelectedCouponType.Id);
                    if (SelectedCoupon != null && SelectedCoupon.Id != 0)
                        gcQuery = gcQuery.Where(x => x.CouponDefinitionId == SelectedCoupon.Id);
                    if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
                        gcQuery = gcQuery.Where(x => x.UnitPrice == SelectedPaymentMethod.Id); // fallback - payment filter not available here

                    var usedCodes = await gcQuery.ToListAsync();

                    results = usedCodes.Select(x => new SalesReportItem
                    {
                        ReceiptDate = x.ReceiptDate,
                        ReceiptCode = x.ReceiptCode,
                        CustomerName = x.CustomerName ?? "ไม่ระบุ",
                        SalesPersonName = x.SalesPersonName ?? string.Empty,
                        CouponName = x.CouponName ?? "ไม่พบข้อมูล",
                        CouponTypeName = x.CouponTypeName ?? string.Empty,
                        PaymentMethodName = string.Empty,
                        Quantity = 1,
                        UnitPrice = x.UnitPrice,
                        TotalPrice = x.TotalPrice,
                        GeneratedCode = x.GeneratedCode
                    }).OrderBy(x => x.ReceiptDate).ThenBy(x => x.CouponName).ToList();
                }
                else if (ReportMode == ReportModes.SummaryByCoupon)
                {
                    // Summary across all coupons (limited + unlimited) without customer breakdown
                    // 1) unlimited coupons: sum quantities from receipt items where coupon is not limited
                    var unlimited = await baseQuery.Where(x => x.IsLimited == false)
                        .GroupBy(x => new { x.CouponId, x.CouponName, x.CouponTypeName })
                        .Select(g => new
                        {
                            CouponId = g.Key.CouponId,
                            CouponName = g.Key.CouponName,
                            CouponTypeName = g.Key.CouponTypeName,
                            Quantity = g.Sum(x => x.Quantity),
                            TotalPrice = g.Sum(x => x.TotalPrice)
                        }).ToListAsync();

                    // 2) limited coupons: count used generated coupons that are attached to receipts in the date range
                    var limited = await (from gc in context.GeneratedCoupons
                                         where gc.IsUsed && gc.ReceiptItemId != null
                                         join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                                         join r in context.Receipts on ri.ReceiptId equals r.ReceiptID
                                         join cd in context.CouponDefinitions on gc.CouponDefinitionId equals cd.Id into cdj
                                         from cd in cdj.DefaultIfEmpty()
                                         join ct in context.CouponTypes on cd.CouponTypeId equals ct.Id into ctj
                                         from ct in ctj.DefaultIfEmpty()
                                         where r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime && r.Status == "Active"
                                         group gc by new { cd.Id, cd.Name, CouponTypeName = ct != null ? ct.Name : string.Empty } into g
                                         select new
                                         {
                                             CouponId = g.Key.Id,
                                             CouponName = g.Key.Name,
                                             CouponTypeName = g.Key.CouponTypeName,
                                             Quantity = g.Count(),
                                             TotalPrice = 0m
                                         }).ToListAsync();

                    // merge unlimited and limited lists by CouponId (some limited may have id=0 if missing)
                    var map = new Dictionary<int, (string Name, string Type, int Qty, decimal Total)>();

                    foreach (var u in unlimited)
                    {
                        map[u.CouponId] = (u.CouponName ?? "ไม่พบข้อมูล", u.CouponTypeName ?? string.Empty, (int)u.Quantity, u.TotalPrice);
                    }

                    foreach (var l in limited)
                    {
                        if (map.TryGetValue(l.CouponId, out var existing))
                        {
                            map[l.CouponId] = (existing.Name, existing.Type, existing.Qty + l.Quantity, existing.Total + l.TotalPrice);
                        }
                        else
                        {
                            map[l.CouponId] = (l.CouponName ?? "ไม่พบข้อมูล", l.CouponTypeName ?? string.Empty, l.Quantity, l.TotalPrice);
                        }
                    }

                    results = map.Select(kv => new SalesReportItem
                    {
                        ReceiptDate = DateTime.MinValue,
                        ReceiptCode = string.Empty,
                        CustomerName = string.Empty,
                        SalesPersonName = string.Empty,
                        CouponName = kv.Value.Name,
                        CouponTypeName = kv.Value.Type,
                        PaymentMethodName = string.Empty,
                        Quantity = kv.Value.Qty,
                        UnitPrice = 0,
                        TotalPrice = kv.Value.Total
                    }).OrderBy(x => x.CouponName).ToList();
                }
                else // UnlimitedGrouped
                 {
                     var unlimitedQuery = baseQuery.Where(x => x.IsLimited == false);
                     var grouped = await unlimitedQuery
                         .GroupBy(x => new { x.CouponId, x.CouponName, x.CustomerName, x.CouponTypeName })
                         .Select(g => new
                         {
                             CouponId = g.Key.CouponId,
                             CouponName = g.Key.CouponName,
                             CustomerName = g.Key.CustomerName,
                             CouponTypeName = g.Key.CouponTypeName,
                             Quantity = g.Sum(x => x.Quantity),
                             TotalPrice = g.Sum(x => x.TotalPrice)
                         }).ToListAsync();

                     results = grouped.Select(g => new SalesReportItem
                     {
                         ReceiptDate = DateTime.MinValue,
                         ReceiptCode = string.Empty,
                         CustomerName = g.CustomerName ?? "ไม่ระบุ",
                         SalesPersonName = string.Empty,
                         CouponName = g.CouponName ?? "ไม่พบข้อมูล",
                         CouponTypeName = g.CouponTypeName ?? string.Empty,
                         PaymentMethodName = string.Empty,
                         Quantity = g.Quantity,
                         UnitPrice = 0,
                         TotalPrice = g.TotalPrice
                     }).OrderBy(x => x.CouponName).ThenBy(x => x.CustomerName).ToList();
                 }

                ReportData.Clear();
                foreach (var it in results)
                    ReportData.Add(it);

                UpdateSummary();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching data: {ex.Message}");
            }
        }

        public void ClearFilters()
        {
            _isUpdatingFilters = true;
            try
            {
                StartDate = DateTime.Today.AddDays(-30);
                EndDate = DateTime.Today;
                SelectedSalesPerson = SalesPersons.FirstOrDefault(); // เลือก "ทั้งหมด"
                SelectedCouponType = CouponTypes.FirstOrDefault(); // เลือก "ทั้งหมด"
                SelectedCoupon = FilteredCoupons.FirstOrDefault(); // เลือก "ทั้งหมด"
                SelectedPaymentMethod = PaymentMethods.FirstOrDefault(); // เลือก "ทั้งหมด"
                ReportData.Clear();
                UpdateSummary();
            }
            finally
            {
                _isUpdatingFilters = false;
            }
        }

        private void UpdateSummary()
        {
            if (ReportData.Count == 0)
            {
                TotalRecordsText = "ไม่มีข้อมูล";
                TotalAmountText = "";
            }
            else
            {
                var totalAmount = ReportData.Sum(x => x.TotalPrice);
                TotalRecordsText = $"จำนวนรายการ: {ReportData.Count:N0} รายการ";
                TotalAmountText = $"ยอดรวม: {totalAmount:N2} บาท";
            }
            
            OnPropertyChanged(nameof(TotalRecordsText));
            OnPropertyChanged(nameof(TotalAmountText));
        }

        public string GetFilterSummary()
        {
            var filters = new List<string>();
            
            if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
                filters.Add($"เซล{SelectedSalesPerson.Name}");
            
            if (SelectedCouponType != null && SelectedCouponType.Id != 0)
                filters.Add($"ประเภท{SelectedCouponType.Name}");
            
            if (SelectedCoupon != null && SelectedCoupon.Id != 0)
                filters.Add($"คูปอง{SelectedCoupon.Name}");
            
            if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
                filters.Add($"ชำระ{SelectedPaymentMethod.Name}");
            
            filters.Add(ReportMode.ToString());
            return string.Join("_", filters);
        }

        public async Task ExportToCsvAsync(StorageFile file)
        {
            var csvContent = new StringBuilder();
            
            // Add header with filter information
            csvContent.AppendLine($"\"รายงานการขาย - {DateTime.Now:dd/MM/yyyy HH:mm}\"");
            csvContent.AppendLine($"\"ช่วงวันที่: {StartDate?.ToString("dd/MM/yyyy")} - {EndDate?.ToString("dd/MM/yyyy")}\"");
            
            // Add filter details if any
            if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
                csvContent.AppendLine($"\"เซลที่เลือก: {SelectedSalesPerson.Name}\"");
            if (SelectedCouponType != null && SelectedCouponType.Id != 0)
                csvContent.AppendLine($"\"ประเภทคูปอง: {SelectedCouponType.Name}\"");
            if (SelectedCoupon != null && SelectedCoupon.Id != 0)
                csvContent.AppendLine($"\"คูปอง: {SelectedCoupon.Name}\"");
            if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
                csvContent.AppendLine($"\"วิธีการชำระเงิน: {SelectedPaymentMethod.Name}\"");
            
            csvContent.AppendLine($"\"รายงานแบบ: {ReportMode}\"");
            csvContent.AppendLine($"\"จำนวนรายการทั้งหมด: {ReportData.Count:N0} รายการ\"");
            csvContent.AppendLine($"\"ยอดรวมทั้งหมด: {ReportData.Sum(x => x.TotalPrice):N2} บาท\"");
            csvContent.AppendLine(); // Empty line

            // Column headers and rows vary by report mode
            if (ReportMode == ReportModes.ByReceipt)
            {
                var headers = new[]
                {
                    "วันที่", "เลขที่ใบเสร็จ", "ลูกค้า", "เซล", "ชื่อคูปอง",
                    "ประเภทคูปอง", "วิธีการพิมพ์", "จำนวน", "ราคา/หน่วย", "รวม"
                };
                csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

                foreach (var item in ReportData)
                {
                    var row = new[]
                    {
                        item.ReceiptDateDisplay, item.ReceiptCode, item.CustomerName,
                        item.SalesPersonName, item.CouponName, item.CouponTypeName,
                        item.PaymentMethodName, item.Quantity.ToString(), 
                        item.UnitPrice.ToString("F2"), item.TotalPrice.ToString("F2")
                    };
                    csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{field?.Replace("\"", "\"\"")}\"")));
                }
            }
            else
            {
                // Grouped reports (UnlimitedGrouped or LimitedCoupons): show Coupon, Customer, Quantity, Total, Type
                var headers = new[] { "ชื่อคูปอง", "ประเภทคูปอง", "ลูกค้า", "จำนวน", "รวม" };
                csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

                foreach (var item in ReportData)
                {
                    var row = new[]
                    {
                        item.CouponName, item.CouponTypeName, item.CustomerName,
                        item.Quantity.ToString(), item.TotalPrice.ToString("F2")
                    };
                    csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{field?.Replace("\"", "\"\"")}\"")));
                }
            }

            // Add summary at the end
            csvContent.AppendLine();
            csvContent.AppendLine($"\"สรุป\"");
            csvContent.AppendLine($"\"จำนวนรายการ: {ReportData.Count:N0}\"");
            csvContent.AppendLine($"\"ยอดรวม: {ReportData.Sum(x => x.TotalPrice):N2} บาท\"");

            await FileIO.WriteTextAsync(file, csvContent.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Data model for report items
    public class SalesReportItem
    {
        public DateTime ReceiptDate { get; set; }
        public string ReceiptCode { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string SalesPersonName { get; set; } = "";
        public string CouponName { get; set; } = "";
        public string CouponTypeName { get; set; } = "";
        public string PaymentMethodName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        public string ReceiptDateDisplay => ReceiptDate == DateTime.MinValue ? "" : ReceiptDate.ToString("dd/MM/yyyy HH:mm");
        public string UnitPriceDisplay => UnitPrice.ToString("N2");
        public string TotalPriceDisplay => TotalPrice.ToString("N2");
        public string GeneratedCode { get; set; } = ""; // สำหรับเก็บรหัสคูปองที่สร้าง
    }
}
