using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    public sealed partial class SalesReportPage : Page
    {
        public SalesReportViewModel ViewModel { get; } = new SalesReportViewModel();
        private bool _isInitialLoad = true;

        public SalesReportPage()
        {
            this.InitializeComponent();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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
                else if (tag == "RemainingCoupons") ViewModel.ReportMode = SalesReportViewModel.ReportModes.RemainingCoupons;
                else if (tag == "CancelledReceipts") ViewModel.ReportMode = SalesReportViewModel.ReportModes.CancelledReceipts;
                else if (tag == "CancelledCoupons") ViewModel.ReportMode = SalesReportViewModel.ReportModes.CancelledCoupons;

                ViewModel.ResetToFirstPage();
                await ViewModel.SearchDataAsync();
                UpdateReportModeButtonVisuals();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.ReportMode))
            {
                DispatcherQueue.TryEnqueue(() => { UpdateReportModeButtonVisuals(); });
            }
        }

        private void UpdateReportModeButtonVisuals()
        {
            try
            {
                var normalBg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                var normalFg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                var normalBorder = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

                var activeBg = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlBackgroundAccentBrush"];
                var activeFg = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                var activeBorder = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];

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
                ApplyNormal(RemainingCouponsButton);
                ApplyNormal(CancelledReceiptsButton);

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
                    case SalesReportViewModel.ReportModes.RemainingCoupons:
                        ApplyActive(RemainingCouponsButton);
                        break;
                    case SalesReportViewModel.ReportModes.CancelledReceipts:
                        ApplyActive(CancelledReceiptsButton);
                        break;
                }
            }
            catch { }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            try
            {
                _isInitialLoad = true;

                if (!ViewModel.StartDate.HasValue)
                {
                    ViewModel.StartDate = new DateTimeOffset(DateTime.Today.AddDays(-30));
                }

                if (!ViewModel.EndDate.HasValue)
                {
                    ViewModel.EndDate = new DateTimeOffset(DateTime.Today);
                }

                await ViewModel.LoadDataAsync();
                await ViewModel.SearchDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnNavigatedTo: {ex.Message}");
                await ShowErrorDialog("เกิดข้อผิดพลาด", $"ไม่สามารถโหลดข้อมูลได้: {ex.Message}");
            }
            finally
            {
                _isInitialLoad = false;
            }
        }

        // Paging button handlers
        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PrevPage();
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NextPage();
        }

        private void BranchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.UpdateFilteredCoupons();
        }

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
            try
            {
                // ✅ แสดง loading indicator
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                SearchButton.IsEnabled = false;

                ViewModel.ResetToFirstPage();
                await ViewModel.SearchDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchButton] Error: {ex.Message}");
                await ShowErrorDialog("เกิดข้อผิดพลาด", 
                    $"ไม่สามารถค้นหาข้อมูลได้: {ex.Message}\n\n" +
                    $"แนะนำ: ลองลดช่วงเวลาการค้นหา หรือเพิ่มตัวกรองเพื่อจำกัดข้อมูล");
            }
            finally
            {
                // ✅ ซ่อน loading indicator
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
                SearchButton.IsEnabled = true;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearFilters();
        }

        private async void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ViewModel.HasData)
                {
                    await ShowErrorDialog("ไม่มีข้อมูล", "ไม่มีข้อมูลให้ส่งออก กรุณาค้นหาข้อมูลก่อน");
                    return;
                }

                var savePicker = new FileSavePicker();
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

                var successMessage = $"ส่งออกข้อมูลเรียบร้อยแล้ว\nไฟล์: {file.Name}\nจำนวนรายการ: {ViewModel.TotalItems:N0} รายการ\nยอดรวมสุทธิ: {ViewModel.AllResults.Sum(x => x.TotalPrice - x.Discount):N2} บาท\n" +
                                   $"ช่วงวันที่: {ViewModel.StartDate?.ToString("dd/MM/yyyy")} - {ViewModel.EndDate?.ToString("dd/MM/yyyy")}";

                // ✅ แก้ไข: ใช้ข้อความสำเร็จที่เรียบง่ายกว่า
                var successMessageSimple = $"ส่งออกข้อมูลเรียบร้อยแล้ว\nไฟล์: {file.Name}";

                await ShowSuccessDialog(successMessageSimple);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("ผิดพลาด", $"เกิดข้อผิดพลาดในการส่งออกข้อมูล: {ex.Message}");
            }
        }

        private async void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ViewModel.HasData)
                {
                    await ShowErrorDialog("ไม่มีข้อมูล", "ไม่มีข้อมูลให้ส่งออก กรุณาค้นหาข้อมูลก่อน");
                    return;
                }

                bool printSuccess = await SalesReportPrintService.PrintSalesReportAsync(ViewModel, this.XamlRoot);

                if (printSuccess)
                {
                    Debug.WriteLine("ส่งออก PDF/พิมพ์รายงานเรียบร้อย");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("ผิดพลาด", $"เกิดข้อผิดพลาดในการส่งออก PDF: {ex.Message}");
            }
        }

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ViewModel.HasData)
                {
                    await ShowErrorDialog("ไม่มีข้อมูล", "ไม่มีข้อมูลให้พิมพ์ กรุณาค้นหาข้อมูลก่อน");
                    return;
                }

                bool printSuccess = await SalesReportPrintService.PrintSalesReportAsync(ViewModel, this.XamlRoot);

                if (printSuccess)
                {
                    Debug.WriteLine("เรียกใช้งานการพิมพ์รายงานสำเร็จ");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("ผิดพลาด", $"เกิดข้อผิดพลาดในการพิมพ์: {ex.Message}");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(MainPage));
        }

        private async Task ShowErrorDialog(string message)
        {
            await ShowErrorDialog("แจ้งเตือน", message);
        }

        private async Task ShowErrorDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
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

    public class SalesReportViewModel : INotifyPropertyChanged
    {
        public enum ReportModes { ByReceipt, LimitedCoupons, UnlimitedGrouped, SummaryByCoupon, RemainingCoupons, CancelledReceipts, CancelledCoupons }
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
        private sealed record ReceiptAggregateData
        {
            public int ReceiptId { get; init; }
            public int PaidGeneratedCount { get; init; }  // คูปองจำกัดที่ขาย
            public int FreeGeneratedCount { get; init; }  // คูปองจำกัดที่ฟรี (COM)
            public int UnlimitedCount { get; init; }      // คูปองไม่จำกัดที่ขาย
            public int FreeUnlimitedCount { get; init; }  // ⭐ คูปองไม่จำกัดที่ฟรี - เพิ่มใหม่
            public decimal PaidGeneratedAmount { get; init; }    // ราคาคูปองจำกัดที่ขาย
            public decimal FreeGeneratedAmount { get; init; }    // ราคาคูปองจำกัดที่ฟรี
            public decimal UnlimitedAmount { get; init; }        // ราคาคูปองไม่จำกัดที่ขาย
            public decimal FreeUnlimitedAmount { get; init; }    // ⭐ ราคาคูปองไม่จำกัดที่ฟรี - เพิ่มใหม่
        }

        private DateTimeOffset? _startDate = DateTime.Today.AddDays(-30);
        private DateTimeOffset? _endDate = DateTime.Today;
        private SalesPerson? _selectedSalesPerson;
        private Branch? _selectedBranch;
        private CouponDefinition? _selectedCoupon;
        private PaymentMethod? _selectedPaymentMethod;
        private SaleEvent? _selectedJob;
        private ObservableCollection<SalesReportItem> _reportData = new();
        private ObservableCollection<CouponDefinition> _filteredCoupons = new();
        private bool _isUpdatingFilters = false;
        private bool _isInitialLoad = true;
        public IReadOnlyList<SalesReportItem> AllResults => (_allResults ?? new List<SalesReportItem>()).AsReadOnly();
        public bool HasData => (_allResults?.Count ?? 0) > 0;
        private List<SalesReportItem> _allResults = new();
        private int _pageSize = 25;
        private int _currentPage = 1;
        private int _totalItems = 0;
        private int _totalPages = 1;

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value && value > 0)
                {
                    _pageSize = value;
                    _currentPage = 1;
                    ApplyPagination();
                    OnPropertyChanged();
                }
            }
        }

        public int CurrentPage => _currentPage;
        public int TotalItems => _totalItems;
        public int TotalPages => _totalPages;
        public string PageInfoText => $"หน้า {_currentPage} / {_totalPages}";

        public void ResetToFirstPage()
        {
            _currentPage = 1;
            ApplyPagination();
        }

        public void NextPage()
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                ApplyPagination();
            }
        }

        public void PrevPage()
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                ApplyPagination();
            }
        }

        private void ApplyPagination()
        {
            if (_allResults == null) _allResults = new List<SalesReportItem>();

            _totalItems = _allResults.Count;
            _totalPages = Math.Max(1, (_totalItems + _pageSize - 1) / _pageSize);
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            var pageItems = _allResults.Skip((_currentPage - 1) * _pageSize).Take(_pageSize).ToList();

            _reportData.Clear();
            foreach (var it in pageItems) _reportData.Add(it);

            OnPropertyChanged(nameof(ReportData));
            OnPropertyChanged(nameof(HasData));
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(TotalItems));
            OnPropertyChanged(nameof(PageInfoText));

            UpdateSummary();
        }

        private void SetFullResults(List<SalesReportItem> results)
        {
            _allResults = results ?? new List<SalesReportItem>();
            _currentPage = 1;
            ApplyPagination();
        }

        public DateTimeOffset? StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate != value)
                {
                    _startDate = value;
                    OnPropertyChanged();
                    System.Diagnostics.Debug.WriteLine($"StartDate set -> {_startDate?.ToString() ?? "null"}");
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
                    System.Diagnostics.Debug.WriteLine($"EndDate set -> {_endDate?.ToString() ?? "null"}");
                    _ = TriggerAutoSearchAsync();
                }
            }
        }
        // ✅ แทนที่ method GetActiveReceiptAggregatesAsync ทั้งหมด
        private async Task<List<ReceiptAggregateData>> GetActiveReceiptAggregatesAsync(
            CouponContext context,
            IQueryable<DatabaseReceiptItem> baseReceiptItemsQuery)
        {
            System.Diagnostics.Debug.WriteLine("[GetActiveReceiptAggregatesAsync] Starting...");

            // ⭐ ดึง ReceiptItem พื้นฐาน พร้อม IsCOM
            var receiptItemsBase = await (
                from ri in baseReceiptItemsQuery
                join cd in context.CouponDefinitions on ri.CouponId equals cd.Id into cdj
                from cd in cdj.DefaultIfEmpty()
                select new
                {
                    ri.ReceiptId,
                    ri.ReceiptItemId,
                    ri.Quantity,
                    ri.UnitPrice,
                    ri.TotalPrice,
                    ri.IsCOM,  // ⭐ เพิ่ม IsCOM
                    IsLimited = cd != null && cd.IsLimited
                }
            ).ToListAsync();

            System.Diagnostics.Debug.WriteLine($"[Active] Found {receiptItemsBase.Count} base receipt items");

            // ⭐ Aggregate ตาม ReceiptId โดยใช้ IsCOM แทน IsComplimentary
            var aggregates = receiptItemsBase
                .GroupBy(x => x.ReceiptId)
                .Select(g => new ReceiptAggregateData
                {
                    ReceiptId = g.Key,

                    // ⭐ คูปองจำกัดที่ขาย (จ่ายเงิน)
                    PaidGeneratedCount = g.Count(x => x.IsLimited && !x.IsCOM),

                    // ⭐ คูปองจำกัดที่ฟรี (COM)
                    FreeGeneratedCount = g.Count(x => x.IsLimited && x.IsCOM),

                    // ⭐ คูปองไม่จำกัดที่ขาย (จ่ายเงิน)
                    UnlimitedCount = g
                        .Where(x => !x.IsLimited && !x.IsCOM)
                        .Sum(x => x.Quantity),

                    // ⭐ คูปองไม่จำกัดที่ฟรี (COM)
                    FreeUnlimitedCount = g
                        .Where(x => !x.IsLimited && x.IsCOM)
                        .Sum(x => x.Quantity),

                    // ⭐ ราคาคูปองจำกัดที่ขาย
                    PaidGeneratedAmount = g
                        .Where(x => x.IsLimited && !x.IsCOM)
                        .Sum(x => x.UnitPrice),

                    // ⭐ ราคาคูปองจำกัดที่ฟรี
                    FreeGeneratedAmount = g
                        .Where(x => x.IsLimited && x.IsCOM)
                        .Sum(x => x.UnitPrice),

                    // ⭐ ราคาคูปองไม่จำกัดที่ขาย
                    UnlimitedAmount = g
                        .Where(x => !x.IsLimited && !x.IsCOM)
                        .Sum(x => x.TotalPrice),

                    // ⭐ ราคาคูปองไม่จำกัดที่ฟรี
                    FreeUnlimitedAmount = g
                        .Where(x => !x.IsLimited && x.IsCOM)
                        .Sum(x => x.UnitPrice * x.Quantity)
                })
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[Active] Processed {aggregates.Count} receipts");
            foreach (var agg in aggregates.Take(3))
            {
                System.Diagnostics.Debug.WriteLine($"  ReceiptId={agg.ReceiptId}: PaidLimited={agg.PaidGeneratedCount}, FreeLimited={agg.FreeGeneratedCount}, PaidUnlimited={agg.UnlimitedCount}, FreeUnlimited={agg.FreeUnlimitedCount}");
            }

            return aggregates;
        }
        // ✅ แทนที่ method GetCancelledReceiptAggregatesAsync ทั้งหมด
        private async Task<List<ReceiptAggregateData>> GetCancelledReceiptAggregatesAsync(
            CouponContext context,
            IQueryable<DatabaseReceiptItem> baseReceiptItemsQuery)
        {
            System.Diagnostics.Debug.WriteLine("[GetCancelledReceiptAggregatesAsync] Starting...");

            // ⭐ ดึง ReceiptItem พื้นฐาน พร้อม IsCOM
            var receiptItemsBase = await (
                from ri in baseReceiptItemsQuery
                join cd in context.CouponDefinitions on ri.CouponId equals cd.Id into cdj
                from cd in cdj.DefaultIfEmpty()
                select new
                {
                    ri.ReceiptId,
                    ri.ReceiptItemId,
                    ri.Quantity,
                    ri.UnitPrice,
                    ri.TotalPrice,
                    ri.IsCOM,  // ⭐ เพิ่ม IsCOM
                    IsLimited = cd != null && cd.IsLimited
                }
            ).ToListAsync();

            System.Diagnostics.Debug.WriteLine($"[Cancelled] Found {receiptItemsBase.Count} base receipt items");

            // ⭐ Aggregate ตาม ReceiptId โดยใช้ IsCOM (เหมือน Active)
            var aggregates = receiptItemsBase
                .GroupBy(x => x.ReceiptId)
                .Select(g => new ReceiptAggregateData
                {
                    ReceiptId = g.Key,
                    PaidGeneratedCount = g.Count(x => x.IsLimited && !x.IsCOM),
                    FreeGeneratedCount = g.Count(x => x.IsLimited && x.IsCOM),
                    UnlimitedCount = g
                        .Where(x => !x.IsLimited && !x.IsCOM)
                        .Sum(x => x.Quantity),
                    FreeUnlimitedCount = g
                        .Where(x => !x.IsLimited && x.IsCOM)
                        .Sum(x => x.Quantity),
                    PaidGeneratedAmount = g
                        .Where(x => x.IsLimited && !x.IsCOM)
                        .Sum(x => x.UnitPrice),
                    FreeGeneratedAmount = g
                        .Where(x => x.IsLimited && x.IsCOM)
                        .Sum(x => x.UnitPrice),
                    UnlimitedAmount = g
                        .Where(x => !x.IsLimited && !x.IsCOM)
                        .Sum(x => x.TotalPrice),
                    FreeUnlimitedAmount = g
                        .Where(x => !x.IsLimited && x.IsCOM)
                        .Sum(x => x.UnitPrice * x.Quantity)
                })
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[Cancelled] Processed {aggregates.Count} receipts");
            foreach (var agg in aggregates.Take(3))
            {
                System.Diagnostics.Debug.WriteLine($"  ReceiptId={agg.ReceiptId}: PaidLimited={agg.PaidGeneratedCount}, FreeLimited={agg.FreeGeneratedCount}, PaidUnlimited={agg.UnlimitedCount}, FreeUnlimited={agg.FreeUnlimitedCount}");
            }

            return aggregates;
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

        public Branch? SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (_selectedBranch != value)
                {
                    _selectedBranch = value;
                    OnPropertyChanged();
                    UpdateFilteredCoupons();
                    _ = TriggerAutoSearchAsync();
                }
            }
        }

        public CouponDefinition? SelectedCoupon
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

        public ObservableCollection<SaleEvent> Jobs { get; } = new();
        public SaleEvent? SelectedJob
        {
            get => _selectedJob;
            set
            {
                if (_selectedJob != value)
                {
                    _selectedJob = value;
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
        public ObservableCollection<Branch> Branches { get; } = new();
        public ObservableCollection<CouponDefinition> AllCoupons { get; } = new();
        public ObservableCollection<CouponDefinition> FilteredCoupons
        {
            get => _filteredCoupons;
            set { _filteredCoupons = value; OnPropertyChanged(); }
        }
        public ObservableCollection<PaymentMethod> PaymentMethods { get; } = new();

        public string TotalRecordsText { get; private set; } = "ไม่มีข้อมูล";
        public string TotalAmountText { get; private set; } = "";
        public string TotalFreeCouponPriceText { get; private set; } = "";
        public string TotalPaidCouponPriceText { get; private set; } = "";
        public string TotalGrandPriceText { get; private set; } = "";

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

                // Load Branches with "ทั้งหมด" option
                var branches = await context.Branches
                    .OrderBy(b => b.Name)
                    .ToListAsync();

                // Debug: print how many branches DB returned and their names
                System.Diagnostics.Debug.WriteLine($"DB branches count: {branches.Count}");
                foreach (var b in branches)
                {
                    System.Diagnostics.Debug.WriteLine($"Branch: Id={b.Id}, Name='{b.Name}'");
                }

                Branches.Clear();
                // Add "ทั้งหมด" option first
                Branches.Add(new Branch { Id = 0, Name = "ทั้งหมด" });

                // Add all DB branches
                foreach (var br in branches)
                    Branches.Add(br);

                // Load CouponDefinitions instead of legacy Coupons
                var couponDefinitions = await context.CouponDefinitions.Include(cd => cd.Branch).ToListAsync();
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

                // Load SaleEvents (jobs) with "ทั้งหมด" option
                var events = await context.SaleEvents.Where(e => e.IsActive).OrderByDescending(e => e.StartDate).ToListAsync();
                Jobs.Clear();
                Jobs.Add(new SaleEvent { Id = 0, Name = "ทั้งหมด", StartDate = DateTime.Today, EndDate = DateTime.Today, IsActive = true });
                foreach (var ev in events)
                    Jobs.Add(ev);

                // set defaults
                SelectedJob = Jobs.FirstOrDefault();

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
            System.Diagnostics.Debug.WriteLine($"TriggerAutoSearchAsync called. _isInitialLoad={_isInitialLoad}, _isUpdatingFilters={_isUpdatingFilters}");
            if (_isInitialLoad || _isUpdatingFilters)
            {
                System.Diagnostics.Debug.WriteLine("Auto-search aborted due to flags.");
                return;
            }

            try
            {
                ResetToFirstPage();
                await SearchDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in auto search: {ex}");
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
                newFilteredCoupons.Add(new CouponDefinition { Id = 0, Name = "ทั้งหมด", Price = 0, Code = "", BranchId = 0 });

                var filtered = SelectedBranch == null || SelectedBranch.Id == 0 ?
                    AllCoupons :
                    AllCoupons.Where(c => c.BranchId == SelectedBranch.Id);

                // additionally filter by SelectedJob (sale event) when chosen
                if (SelectedJob != null && SelectedJob.Id != 0)
                {
                    filtered = filtered.Where(c => c.SaleEventId == SelectedJob.Id);
                }

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating filtered coupons: {ex.Message}");
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
                
                // ✅ เพิ่ม timeout สำหรับ query ขนาดใหญ่
                context.Database.SetCommandTimeout(300); // 5 นาที

                var startDateTime = StartDate.HasValue
                    ? StartDate.Value.DateTime.Date
                    : DateTime.Today.AddDays(-30).Date;

                var endDateTime = EndDate.HasValue
                    ? EndDate.Value.DateTime.Date.AddDays(1)
                    : DateTime.Today.Date.AddDays(1);

                // ✅ เพิ่ม logging เพื่อ debug
                System.Diagnostics.Debug.WriteLine($"[SearchData] Date range: {startDateTime:yyyy-MM-dd} to {endDateTime:yyyy-MM-dd}");
                
                // ✅ เช็คจำนวนข้อมูลก่อน query ใหญ่
                var estimatedCount = await context.Receipts
                    .Where(r => r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime)
                    .CountAsync();
                
                System.Diagnostics.Debug.WriteLine($"[SearchData] Estimated records: {estimatedCount}");

                // ✅ แจ้งเตือนถ้าข้อมูลเยอะมาก
                if (estimatedCount > 10000)
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchData] WARNING: Large dataset ({estimatedCount} records). Query may be slow.");
                }

                // ✅ แก้ไข: ตรวจสอบ null และใช้ HasValue แทนการเข้าถึง .DateTime โดยตรง
                startDateTime = StartDate.HasValue
                    ? StartDate.Value.DateTime.Date
                    : DateTime.Today.AddDays(-30).Date;

                endDateTime = EndDate.HasValue
                    ? EndDate.Value.DateTime.Date.AddDays(1)
                    : DateTime.Today.Date.AddDays(1);

                // กำหนดสถานะที่จะแสดงตาม ReportMode
                var statusToShow = new List<string>();

                if (ReportMode == ReportModes.CancelledReceipts || ReportMode == ReportModes.CancelledCoupons)
                {
                    // สำหรับรายงานที่ยกเลิก แสดงเฉพาะ Cancelled
                    statusToShow.Add("Cancelled");
                }
                else
                {
                    // สำหรับรายงานปกติ แสดงเฉพาะ Active
                    statusToShow.Add("Active");
                }

                // Preload sale events for name lookup
                var saleEvents = await context.SaleEvents.ToListAsync();
                var saleEventDict = saleEvents.ToDictionary(se => se.Id, se => se.Name);

                // Base query for receipt items joined with coupon definition and related data
                var baseQuery = from r in context.Receipts.AsNoTracking()
                                where r.ReceiptDate >= startDateTime
                                && r.ReceiptDate < endDateTime
                                && statusToShow.Contains(r.Status)
                                from item in r.Items
                                join c in context.CouponDefinitions on item.CouponId equals c.Id into cj
                                from c in cj.DefaultIfEmpty()
                                join ct in context.Branches on c.BranchId equals ct.Id into ctj
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
                                    ReceiptStatus = r.Status,
                                    CustomerName = r.CustomerName,
                                    CustomerPhone = r.CustomerPhoneNumber,
                                    SalesPersonName = sp != null ? sp.Name : null,
                                    CouponId = c != null ? c.Id : 0,
                                    CouponName = c != null ? c.Name : null,
                                    BranchId = ct != null ? ct.Id : 0,
                                    BranchName = ct != null ? ct.Name : null,
                                    IsLimited = c != null ? c.IsLimited : false,
                                    PaymentMethodName = pm != null ? pm.Name : null,
                                    Quantity = item.Quantity,
                                    UnitPrice = item.UnitPrice,
                                    TotalPrice = item.TotalPrice,
                                    GeneratedCouponId = gc != null ? gc.Id : 0,
                                    GeneratedCode = gc != null ? gc.GeneratedCode : null,
                                    CouponValidTo = c != null ? c.ValidTo : (DateTime?)null,
                                    SaleEventId = (c != null && c.SaleEventId.HasValue) ? c.SaleEventId!.Value : 0
                                };

                // Apply simple filters before shaping
                if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
                    baseQuery = baseQuery.Where(x => x.SalesPersonName == SelectedSalesPerson.Name);
                if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
                    baseQuery = baseQuery.Where(x => x.PaymentMethodName == SelectedPaymentMethod.Name);
                if (SelectedBranch != null && SelectedBranch.Id != 0)
                    baseQuery = baseQuery.Where(x => x.BranchId == SelectedBranch.Id);
                if (SelectedCoupon != null && SelectedCoupon.Id != 0)
                    baseQuery = baseQuery.Where(x => x.CouponId == SelectedCoupon.Id);

                // Apply sale event (job) filter when selected
                if (SelectedJob != null && SelectedJob.Id != 0)
                    baseQuery = baseQuery.Where(x => x.SaleEventId == SelectedJob.Id);

                List<SalesReportItem> results = new();

                if (ReportMode == ReportModes.ByReceipt || ReportMode == ReportModes.CancelledReceipts)
                {
                    // Pull receipts in date range with status filter
                    var receiptsQuery = context.Receipts
                        .Where(r => r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime);

                    // Apply status filter based on report mode
                    if (ReportMode == ReportModes.CancelledReceipts)
                    {
                        receiptsQuery = receiptsQuery.Where(r => r.Status == "Cancelled");
                    }
                    else
                    {
                        receiptsQuery = receiptsQuery.Where(r => r.Status == "Active");
                    }

                    var receiptsInRange = await receiptsQuery.ToListAsync();

                    // Apply other filters
                    var filteredReceipts = receiptsInRange.AsEnumerable();

                    if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
                    {
                        var salesPersons = await context.SalesPerson.Where(sp => sp.ID == SelectedSalesPerson.ID).ToListAsync();
                        var spIds = salesPersons.Select(sp => sp.ID).ToHashSet();
                        filteredReceipts = filteredReceipts.Where(r => r.SalesPersonId.HasValue && spIds.Contains(r.SalesPersonId.Value));
                    }

                    if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
                    {
                        filteredReceipts = filteredReceipts.Where(r => r.PaymentMethodId == SelectedPaymentMethod.Id);
                    }

                    var receiptIds = filteredReceipts.Select(r => r.ReceiptID).ToList();
                    var salesPersonIds = filteredReceipts.Where(r => r.SalesPersonId.HasValue).Select(r => r.SalesPersonId!.Value).Distinct().ToList();
                    var paymentMethodIds = filteredReceipts.Where(r => r.PaymentMethodId.HasValue).Select(r => r.PaymentMethodId!.Value).Distinct().ToList();

                    var salesPersonDict = await context.SalesPerson
                        .Where(sp => salesPersonIds.Contains(sp.ID))
                        .ToDictionaryAsync(sp => sp.ID, sp => sp.Name);

                    var paymentMethodDict = await context.PaymentMethods
                        .Where(pm => paymentMethodIds.Contains(pm.Id))
                        .ToDictionaryAsync(pm => pm.Id, pm => pm.Name);

                    // Map receipt -> sale-event ids (to build readable sale-event name per receipt)
                    Dictionary<int, List<int>> receiptEventMap = new();
                    if (receiptIds.Any())
                    {
                        // Replace the existing receiptEventPairs query with the following to ensure SaleEventId is int (not int?)
                        var receiptEventPairs = await (from ri in context.ReceiptItems
                                                       join cd in context.CouponDefinitions on ri.CouponId equals cd.Id into cdj
                                                       from cd in cdj.DefaultIfEmpty()
                                                       where receiptIds.Contains(ri.ReceiptId)
                                                       select new
                                                       {
                                                           ri.ReceiptId,
                                                           SaleEventId = (cd != null && cd.SaleEventId.HasValue) ? cd.SaleEventId.Value : 0
                                                       }).ToListAsync();

                        receiptEventMap = receiptEventPairs
                            .GroupBy(x => x.ReceiptId)
                            .ToDictionary(g => g.Key, g => g.Select(x => x.SaleEventId).Where(id => id != 0).Distinct().ToList());
                    }

                    // If coupon filters are selected, build a single receipt-items query
                    IQueryable<DatabaseReceiptItem> baseReceiptItemsQuery = context.ReceiptItems.Where(ri => receiptIds.Contains(ri.ReceiptId));

                    if ((SelectedCoupon != null && SelectedCoupon.Id != 0) || (SelectedBranch != null && SelectedBranch.Id != 0) || (SelectedJob != null && SelectedJob.Id != 0))
                    {
                        var joined = from ri in context.ReceiptItems
                                     join cd in context.CouponDefinitions on ri.CouponId equals cd.Id into cdj
                                     from cd in cdj.DefaultIfEmpty()
                                     where receiptIds.Contains(ri.ReceiptId)
                                     select new { ri, cd };

                        if (SelectedCoupon != null && SelectedCoupon.Id != 0)
                        {
                            joined = joined.Where(x => x.ri.CouponId == SelectedCoupon.Id);
                        }

                        if (SelectedBranch != null && SelectedBranch.Id != 0)
                        {
                            joined = joined.Where(x => x.cd != null && x.cd.BranchId == SelectedBranch.Id);
                        }

                        if (SelectedJob != null && SelectedJob.Id != 0)
                        {
                            joined = joined.Where(x => x.cd != null && x.cd.SaleEventId == SelectedJob.Id);
                        }

                        var matchingReceiptIds = await joined.Select(x => x.ri.ReceiptId).Distinct().ToListAsync();
                        filteredReceipts = filteredReceipts.Where(r => matchingReceiptIds.Contains(r.ReceiptID));

                        baseReceiptItemsQuery = joined.Select(x => x.ri);
                    }

                    List<ReceiptAggregateData> grouped;

                    if (ReportMode == ReportModes.ByReceipt)
                    {
                        grouped = await GetActiveReceiptAggregatesAsync(context, baseReceiptItemsQuery);
                    }
                    else // CancelledReceipts
                    {
                        grouped = await GetCancelledReceiptAggregatesAsync(context, baseReceiptItemsQuery);
                    }

                    // Debug - โค้ดเดิมคงไว้
                    System.Diagnostics.Debug.WriteLine($"[{ReportMode}] Found {grouped.Count} receipt groups");
                    foreach (var gg in grouped.Take(3))
                    {
                        System.Diagnostics.Debug.WriteLine($"  ReceiptId={gg.ReceiptId}: paidGen={gg.PaidGeneratedCount}, freeGen={gg.FreeGeneratedCount}, paidUnlimited={gg.UnlimitedCount}, freeUnlimited={gg.FreeUnlimitedCount}");
                    }

                    // Map receipts to report rows - โค้ดเดิมยังคงเหมือนเดิม
                    // Map receipts to report rows
                    results = filteredReceipts.Select(r =>
                    {
                        var aggregateData = grouped.FirstOrDefault(x => x.ReceiptId == r.ReceiptID);

                        var paidGenCount = aggregateData?.PaidGeneratedCount ?? 0;
                        var freeGenCount = aggregateData?.FreeGeneratedCount ?? 0;
                        var unlimCount = aggregateData?.UnlimitedCount ?? 0;
                        var freeUnlimCount = aggregateData?.FreeUnlimitedCount ?? 0;  // ⭐ เพิ่มใหม่

                        var paidGenAmount = aggregateData?.PaidGeneratedAmount ?? 0m;
                        var freeGenAmount = aggregateData?.FreeGeneratedAmount ?? 0m;
                        var unlimAmount = aggregateData?.UnlimitedAmount ?? 0m;
                        var freeUnlimAmount = aggregateData?.FreeUnlimitedAmount ?? 0m;  // ⭐ เพิ่มใหม่

                        // ⭐ คำนวณตามสูตรใหม่ที่รวมคูปองไม่จำกัดที่ฟรี
                        var paidCouponCount = paidGenCount + unlimCount;
                        var freeCouponCount = freeGenCount + freeUnlimCount;  // ⭐ รวมฟรีทั้ง 2 ประเภท
                        var totalCouponCount = paidCouponCount + freeCouponCount;

                        var paidCouponPrice = paidGenAmount + unlimAmount;
                        var freeCouponPrice = freeGenAmount + freeUnlimAmount;  // ⭐ รวมฟรีทั้ง 2 ประเภท
                        var grandTotalPrice = paidCouponPrice + freeCouponPrice;

                        return new SalesReportItem
                        {
                            ReceiptDate = r.ReceiptDate,
                            ReceiptCode = r.ReceiptCode,
                            ReceiptStatus = r.Status,
                            CustomerName = r.CustomerName ?? "ไม่ระบุ",
                            CustomerPhone = r.CustomerPhoneNumber ?? string.Empty,
                            SalesPersonName = r.SalesPersonId.HasValue && salesPersonDict.ContainsKey(r.SalesPersonId.Value)
                                ? salesPersonDict[r.SalesPersonId.Value]
                                : "ไม่ระบุ",
                            PaymentMethodName = r.PaymentMethodId.HasValue && paymentMethodDict.ContainsKey(r.PaymentMethodId.Value)
                                ? paymentMethodDict[r.PaymentMethodId.Value]
                                : "ไม่ระบุ",
                            
                            // ✅ เพิ่ม: กำหนดค่าที่คำนวณจาก items
                            PaidCouponCount = paidCouponCount,           // ✅ เพิ่มบรรทัดนี้
                            FreeCouponCount = freeCouponCount,
                            TotalCouponCount = totalCouponCount,

                            PaidCouponPrice = paidCouponPrice,           // ✅ เพิ่มบรรทัดนี้
                            FreeCouponPrice = freeCouponPrice,
                            GrandTotalPrice = grandTotalPrice,           // ✅ เพิ่มบรรทัดนี้
                            
                            TotalPrice = r.TotalAmount,                   // เก็บไว้สำหรับ reference
                            Discount = r.Discount,

                            CancellationReason = r.CancellationReason ?? string.Empty
                        };
                    }).ToList();
                }
                else if (ReportMode == ReportModes.LimitedCoupons || ReportMode == ReportModes.UnlimitedGrouped || ReportMode == ReportModes.CancelledCoupons)
                {
                    // ** สำหรับ CancelledCoupons ใช้วิธีที่แตกต่างออกไป **
                    if (ReportMode == ReportModes.CancelledCoupons)
                    {
                        // Query ใบเสร็จที่ถูกยกเลิกในช่วงเวลาที่เลือก
                        var cancelledReceiptsQuery = context.Receipts
        .Where(r => r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime && r.Status == "Cancelled");

    // Apply filters
    if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
        cancelledReceiptsQuery = cancelledReceiptsQuery.Where(r => r.SalesPersonId == SelectedSalesPerson.ID);

    if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
        cancelledReceiptsQuery = cancelledReceiptsQuery.Where(r => r.PaymentMethodId == SelectedPaymentMethod.Id);

    var cancelledReceipts = await cancelledReceiptsQuery.ToListAsync();
    var cancelledReceiptIds = cancelledReceipts.Select(r => r.ReceiptID).ToList();

    if (!cancelledReceiptIds.Any())
    {
        results = new List<SalesReportItem>();
    }
    else
    {
        // ⭐ ดึงข้อมูลจาก ReceiptItems แทน GeneratedCouponsHistory
        var cancelledItemsQuery = from ri in context.ReceiptItems
                          where cancelledReceiptIds.Contains(ri.ReceiptId)
                          join r in context.Receipts on ri.ReceiptId equals r.ReceiptID
                          join cd in context.CouponDefinitions on ri.CouponId equals cd.Id into cdj
                          from cd in cdj.DefaultIfEmpty()
                          join ct in context.Branches on cd.BranchId equals ct.Id into ctj
                          from ct in ctj.DefaultIfEmpty()
                          join sp in context.SalesPerson on r.SalesPersonId equals sp.ID into spj
                          from sp in spj.DefaultIfEmpty()
                          join pm in context.PaymentMethods on r.PaymentMethodId equals pm.Id into pmj
                          from pm in pmj.DefaultIfEmpty()
                          // ⭐ Join GeneratedCouponsHistory เฉพาะสำหรับดึง GeneratedCode (สำหรับคูปองจำกัด)
                          join gch in context.GeneratedCouponsHistory on ri.ReceiptItemId equals gch.ReceiptItemId into gchj
                          from gch in gchj.DefaultIfEmpty()
                          select new
                          {
                              ReceiptDate = r.ReceiptDate,
                              ReceiptCode = r.ReceiptCode,
                              ReceiptStatus = r.Status,
                              CustomerName = r.CustomerName,
                              CustomerPhone = r.CustomerPhoneNumber,
                              SalesPersonName = sp != null ? sp.Name : null,
                              CouponDefinitionId = cd != null ? cd.Id : 0,
                              CouponName = cd != null ? cd.Name : null,
                              BranchTypeName = ct != null ? ct.Name : string.Empty,
                              BranchId = ct != null ? ct.Id : 0,
                              IsLimited = cd != null ? cd.IsLimited : false,
                              // ⭐ ดึง GeneratedCode จาก History (สำหรับคูปองจำกัดเท่านั้น)
                              GeneratedCode = gch != null ? gch.GeneratedCode : null,
                              ExpiresAt = gch != null ? gch.ExpiresAt : (cd != null ? cd.ValidTo : (DateTime?)null),
                              PaymentMethodName = pm != null ? pm.Name : null,
                              PaymentMethodId = pm != null ? pm.Id : 0,
                              SaleEventId = (cd != null && cd.SaleEventId.HasValue) ? cd.SaleEventId!.Value : 0,  // ✅ แก้ไข: ใช้ .Value
                              UnitPrice = ri.UnitPrice,
                              TotalPrice = ri.TotalPrice,
                              Quantity = ri.Quantity,
                              // ⭐ ใช้ IsCOM จาก ReceiptItems แทน IsComplimentary จาก History
                              IsCOM = ri.IsCOM,
                              CancellationReason = r.CancellationReason ?? ""
                          };

        // Apply coupon filters
        if (SelectedBranch != null && SelectedBranch.Id != 0)
            cancelledItemsQuery = cancelledItemsQuery.Where(x => x.BranchId == SelectedBranch.Id);

        if (SelectedCoupon != null && SelectedCoupon.Id != 0)
            cancelledItemsQuery = cancelledItemsQuery.Where(x => x.CouponDefinitionId == SelectedCoupon.Id);

        if (SelectedJob != null && SelectedJob.Id != 0)
            cancelledItemsQuery = cancelledItemsQuery.Where(x => x.SaleEventId == SelectedJob.Id);

        var cancelledItems = await cancelledItemsQuery.ToListAsync();

        results = cancelledItems.Select(x => new SalesReportItem
        {
            ReceiptDate = x.ReceiptDate,
            ReceiptCode = x.ReceiptCode,
            ReceiptStatus = x.ReceiptStatus,
            CustomerName = x.CustomerName ?? "ไม่ระบุ",
            CustomerPhone = x.CustomerPhone ?? string.Empty,
            SalesPersonName = x.SalesPersonName ?? string.Empty,
            CouponName = x.CouponName ?? "ไม่พบข้อมูล",
            BranchTypeName = x.BranchTypeName ?? string.Empty,
            PaymentMethodName = x.PaymentMethodName ?? string.Empty,
            Quantity = x.Quantity,  // ⭐ ใช้ Quantity จริง
            // ⭐ ถ้าเป็น COM ราคาจะเป็น 0
            UnitPrice = x.IsCOM ? 0m : x.UnitPrice,
            TotalPrice = x.IsCOM ? 0m : x.TotalPrice,
            GeneratedCode = x.GeneratedCode,
            ExpiresAt = x.ExpiresAt,
            IsComplimentary = x.IsCOM,  // ⭐ ใช้ IsCOM แทน IsComplimentary
            SaleEventName = (x.SaleEventId != 0 && saleEventDict.TryGetValue(x.SaleEventId, out var cn)) ? cn : string.Empty,
            CancellationReason = x.CancellationReason
        })
        .OrderBy(x => x.ReceiptDate)
        .ThenBy(x => x.ReceiptCode)
        .ThenBy(x => x.CouponName)
        .ToList();
    }
}
else if (ReportMode == ReportModes.UnlimitedGrouped)
{
    // ** แก้ไข: ดึงข้อมูลคูปองไม่จำกัดจาก ReceiptItems โดยตรง **
    var unlimitedItemsQuery = from ri in context.ReceiptItems
          join r in context.Receipts on ri.ReceiptId equals r.ReceiptID
          join cd in context.CouponDefinitions on ri.CouponId equals cd.Id into cdj
          from cd in cdj.DefaultIfEmpty()
          join ct in context.Branches on cd.BranchId equals ct.Id into ctj
          from ct in ctj.DefaultIfEmpty()
          join sp in context.SalesPerson on r.SalesPersonId equals sp.ID into spj
          from sp in spj.DefaultIfEmpty()
          join pm in context.PaymentMethods on r.PaymentMethodId equals pm.Id into pmj
          from pm in pmj.DefaultIfEmpty()
          where r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime
               && r.Status == "Active"
               && cd != null && cd.IsLimited == false  // กรองเฉพาะคูปองไม่จำกัด
          select new
          {
              ReceiptDate = r.ReceiptDate,
              ReceiptCode = r.ReceiptCode,
              ReceiptStatus = r.Status,
              CustomerName = r.CustomerName,
              CustomerPhone = r.CustomerPhoneNumber,
              SalesPersonName = sp != null ? sp.Name : null,
              CouponDefinitionId = cd != null ? cd.Id : 0,
              CouponName = cd != null ? cd.Name : null,
              BranchTypeName = ct != null ? ct.Name : string.Empty,
              BranchId = ct != null ? ct.Id : 0,
              IsLimited = cd != null ? cd.IsLimited : false,
              ExpiresAt = cd != null ? cd.ValidTo : (DateTime?)null,
              UnitPrice = ri.UnitPrice,
              TotalPrice = ri.TotalPrice,
              Quantity = ri.Quantity,
              PaymentMethodName = pm != null ? pm.Name : null,
              PaymentMethodId = pm != null ? pm.Id : 0,
              SaleEventId = cd != null ? cd.SaleEventId : 0,
              IsCOM = ri.IsCOM  // ⭐ เพิ่ม IsCOM
          };

    // Apply filters
    if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.SalesPersonName == SelectedSalesPerson.Name);
    if (SelectedBranch != null && SelectedBranch.Id != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.BranchId == SelectedBranch.Id);
    if (SelectedCoupon != null && SelectedCoupon.Id != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.CouponDefinitionId == SelectedCoupon.Id);
    if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.PaymentMethodId == SelectedPaymentMethod.Id);

    if (SelectedJob != null && SelectedJob.Id != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.SaleEventId == SelectedJob.Id);

    var unlimitedItems = await unlimitedItemsQuery.ToListAsync();

    results = unlimitedItems.Select(x => new SalesReportItem
    {
        ReceiptDate = x.ReceiptDate,
        ReceiptCode = x.ReceiptCode,
        ReceiptStatus = x.ReceiptStatus,
        CustomerName = x.CustomerName ?? "ไม่ระบุ",
        CustomerPhone = x.CustomerPhone ?? string.Empty,
        SalesPersonName = x.SalesPersonName ?? string.Empty,
        CouponName = x.CouponName ?? "ไม่พบข้อมูล",
        BranchTypeName = x.BranchTypeName ?? string.Empty,
        PaymentMethodName = x.PaymentMethodName ?? string.Empty,
        Quantity = x.Quantity,
        UnitPrice = x.UnitPrice,
        TotalPrice = x.TotalPrice,
        ExpiresAt = x.ExpiresAt,
        IsComplimentary = x.IsCOM,  // ⭐ ใช้ IsCOM แทน IsComplimentary
        SaleEventName = (x.SaleEventId.HasValue && x.SaleEventId.Value != 0 && saleEventDict.TryGetValue(x.SaleEventId.Value, out var un)) ? un : string.Empty
    })
    .OrderBy(x => x.ReceiptDate)
    .ThenBy(x => x.ReceiptCode)
    .ThenBy(x => x.CouponName)
    .ToList();
}
else
{
    // โค้ดเดิมสำหรับ LimitedCoupons (Active receipts only)
    var gcQuery = from gc in context.GeneratedCoupons
                  where gc.ReceiptItemId != null
                  join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                  join r in context.Receipts on ri.ReceiptId equals r.ReceiptID
                  join cd in context.CouponDefinitions on gc.CouponDefinitionId equals cd.Id into cdj
                  from cd in cdj.DefaultIfEmpty()
                  join ct in context.Branches on cd.BranchId equals ct.Id into ctj
                  from ct in ctj.DefaultIfEmpty()
                  join sp in context.SalesPerson on r.SalesPersonId equals sp.ID into spj
                  from sp in spj.DefaultIfEmpty()
                  join pm in context.PaymentMethods on r.PaymentMethodId equals pm.Id into pmj
                  from pm in pmj.DefaultIfEmpty()
                  where r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime
                          && r.Status == "Active"
                  select new
                  {
                      ReceiptDate = r.ReceiptDate,
                      ReceiptCode = r.ReceiptCode,
                      ReceiptStatus = r.Status,
                      CustomerName = r.CustomerName,
                      CustomerPhone = r.CustomerPhoneNumber,
                      SalesPersonName = sp != null ? sp.Name : null,
                      CouponDefinitionId = cd != null ? cd.Id : 0,
                      CouponName = cd != null ? cd.Name : null,
                      BranchTypeName = ct != null ? ct.Name : string.Empty,
                      BranchId = ct != null ? ct.Id : 0,
                      IsLimited = cd != null ? cd.IsLimited : false,
                      GeneratedCode = gc.GeneratedCode,
                      ExpiresAt = gc.ExpiresAt,
                      UnitPrice = cd != null ? cd.Price : 0m,
                      TotalPrice = cd != null ? cd.Price : 0m,
                      PaymentMethodName = pm != null ? pm.Name : null,
                      PaymentMethodId = pm != null ? pm.Id : 0,
                      SaleEventId = cd != null ? cd.SaleEventId : 0,
                      // ⭐ เปลี่ยนจาก gc.IsComplimentary เป็น ri.IsCOM
                      IsComplimentary = ri != null ? ri.IsCOM : false
                  };

    // Apply filters
    if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
        gcQuery = gcQuery.Where(x => x.SalesPersonName == SelectedSalesPerson.Name);
    if (SelectedBranch != null && SelectedBranch.Id != 0)
        gcQuery = gcQuery.Where(x => x.BranchId == SelectedBranch.Id);
    if (SelectedCoupon != null && SelectedCoupon.Id != 0)
        gcQuery = gcQuery.Where(x => x.CouponDefinitionId == SelectedCoupon.Id);
    if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
        gcQuery = gcQuery.Where(x => x.PaymentMethodId == SelectedPaymentMethod.Id);

    if (SelectedJob != null && SelectedJob.Id != 0)
        gcQuery = gcQuery.Where(x => x.SaleEventId == SelectedJob.Id);

    // Filter by coupon type (limited/unlimited) based on report mode
    if (ReportMode == ReportModes.LimitedCoupons)
    {
        gcQuery = gcQuery.Where(x => x.IsLimited == true);
    }
    else if (ReportMode == ReportModes.UnlimitedGrouped)
    {
        gcQuery = gcQuery.Where(x => x.IsLimited == false);
    }

    var usedCodes = await gcQuery.ToListAsync();

    // Map live generated coupons
    results = usedCodes.Select(x => new SalesReportItem
    {
        ReceiptDate = x.ReceiptDate,
        ReceiptCode = x.ReceiptCode,
        ReceiptStatus = x.ReceiptStatus,
        CustomerName = x.CustomerName ?? "ไม่ระบุ",
        CustomerPhone = x.CustomerPhone ?? string.Empty,
        SalesPersonName = x.SalesPersonName ?? string.Empty,
        CouponName = x.CouponName ?? "ไม่พบข้อมูล",
        BranchTypeName = x.BranchTypeName ?? string.Empty,
        PaymentMethodName = x.PaymentMethodName ?? string.Empty,
        Quantity = 1,
        // ⭐ ไม่ต้องเปลี่ยนเป็น 0 แล้ว - แสดงราคาดั้งเดิม
        UnitPrice = x.UnitPrice,
        TotalPrice = x.TotalPrice,
        GeneratedCode = x.GeneratedCode,
        ExpiresAt = x.ExpiresAt,
        // set IsComplimentary so the UI can color the GeneratedCode when needed
        IsComplimentary = x.IsComplimentary,
        // Sale event name
        SaleEventName = (x.SaleEventId.HasValue && x.SaleEventId.Value != 0 && saleEventDict.TryGetValue(x.SaleEventId.Value, out var ln)) ? ln : string.Empty
    })
    .OrderBy(x => x.ReceiptDate)
    .ThenBy(x => x.ReceiptCode)
    .ThenBy(x => x.CouponName)
    .ToList();  // ✅ เพิ่ม .ToList() และกำหนดให้ results
                }
            }
            else if (ReportMode == ReportModes.SummaryByCoupon)
            {
                // Summary (only Active)
                var statusFilter = "Active";

                // ⭐ Unlimited coupons aggregation - แยก COM และไม่ COM
                var unlimitedItemsQuery = from ri in context.ReceiptItems
                             join r in context.Receipts on ri.ReceiptId equals r.ReceiptID
                             join cd in context.CouponDefinitions on ri.CouponId equals cd.Id into cdj
                             from cd in cdj.DefaultIfEmpty()
                             join ct in context.Branches on cd.BranchId equals ct.Id into ctj
                             from ct in ctj.DefaultIfEmpty()
                             join sp in context.SalesPerson on r.SalesPersonId equals sp.ID into spj
                             from sp in spj.DefaultIfEmpty()
                             join pm in context.PaymentMethods on r.PaymentMethodId equals pm.Id into pmj
                             from pm in pmj.DefaultIfEmpty()
                             where r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime
                                  && r.Status == statusFilter
                                  && cd != null && cd.IsLimited == false
                             select new
                             {
                                 CouponId = cd.Id,
                                 CouponName = cd.Name,
                                 BranchName = ct != null ? ct.Name : string.Empty,
                                 BranchId = ct != null ? ct.Id : 0,
                                 SaleEventId = cd.SaleEventId ?? 0,
                                 Quantity = ri.Quantity,
                                 UnitPrice = ri.UnitPrice,
                                 TotalPrice = ri.TotalPrice,
                                 IsCOM = ri.IsCOM,  // ⭐ เพิ่ม IsCOM
                                 SalesPersonName = sp != null ? sp.Name : null,
                                 PaymentMethodId = pm != null ? pm.Id : 0
                             };

    // Apply filters
    if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.SalesPersonName == SelectedSalesPerson.Name);
    if (SelectedBranch != null && SelectedBranch.Id != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.BranchId == SelectedBranch.Id);
    if (SelectedCoupon != null && SelectedCoupon.Id != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.CouponId == SelectedCoupon.Id);
    if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.PaymentMethodId == SelectedPaymentMethod.Id);
    if (SelectedJob != null && SelectedJob.Id != 0)
        unlimitedItemsQuery = unlimitedItemsQuery.Where(x => x.SaleEventId == SelectedJob.Id);

    var unlimitedItems = await unlimitedItemsQuery.ToListAsync();

    // ⭐ Aggregate โดยแยก COM และไม่ COM
    var unlimitedAgg = unlimitedItems
        .GroupBy(x => new { x.CouponId, x.CouponName, x.BranchName, x.SaleEventId })
        .Select(g => new
        {
            CouponId = g.Key.CouponId,
            CouponName = g.Key.CouponName,
            BranchName = g.Key.BranchName,
            SaleEventId = g.Key.SaleEventId,
            SoldCount = g.Where(x => !x.IsCOM).Sum(x => x.Quantity),      // ⭐ นับเฉพาะที่ไม่ COM
            FreeCount = g.Where(x => x.IsCOM).Sum(x => x.Quantity),       // ⭐ นับเฉพาะที่ COM
            PaidAmount = g.Where(x => !x.IsCOM).Sum(x => x.TotalPrice),   // ⭐ ราคาที่ขาย
            FreeAmount = g.Where(x => x.IsCOM).Sum(x => x.UnitPrice * x.Quantity), // ⭐ มูลค่า COM
            IsLimited = false
        }).ToList();

    // Limited coupons aggregation (from GeneratedCoupons), include SaleEventId from CouponDefinition
    var limitedQuery = from gc in context.GeneratedCoupons
                       where gc.ReceiptItemId != null
                       join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                       join r in context.Receipts on ri.ReceiptId equals r.ReceiptID
                       join cd in context.CouponDefinitions on gc.CouponDefinitionId equals cd.Id into cdj
                       from cd in cdj.DefaultIfEmpty()
                       join ct in context.Branches on cd.BranchId equals ct.Id into ctj
                       from ct in ctj.DefaultIfEmpty()
                       join sp in context.SalesPerson on r.SalesPersonId equals sp.ID into spj
                       from sp in spj.DefaultIfEmpty()
                       join pm in context.PaymentMethods on r.PaymentMethodId equals pm.Id into pmj
                       from pm in pmj.DefaultIfEmpty()
                       where r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime && r.Status == statusFilter
                       select new
                       {
                           CouponId = cd != null ? cd.Id : 0,
                           CouponName = cd != null ? cd.Name : string.Empty,
                           BranchName = ct != null ? ct.Name : string.Empty,
                           BranchId = ct != null ? ct.Id : 0,
                           Price = cd != null ? cd.Price : 0m,
                           IsComplimentary = gc != null ? gc.IsComplimentary : false,
                           SaleEventId = (cd != null && cd.SaleEventId.HasValue) ? cd.SaleEventId!.Value : 0,
                           SalesPersonName = sp != null ? sp.Name : null,
                           PaymentMethodId = pm != null ? pm.Id : 0
                       };

    // Apply filters to limitedQuery (same as other modes)
    if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
        limitedQuery = limitedQuery.Where(x => x.SalesPersonName == SelectedSalesPerson.Name);
    if (SelectedBranch != null && SelectedBranch.Id != 0)
        limitedQuery = limitedQuery.Where(x => x.BranchId == SelectedBranch.Id);
    if (SelectedCoupon != null && SelectedCoupon.Id != 0)
        limitedQuery = limitedQuery.Where(x => x.CouponId == SelectedCoupon.Id);
    if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
        limitedQuery = limitedQuery.Where(x => x.PaymentMethodId == SelectedPaymentMethod.Id);
    if (SelectedJob != null && SelectedJob.Id != 0)
        limitedQuery = limitedQuery.Where(x => x.SaleEventId == SelectedJob.Id);

    var limitedData = await limitedQuery.ToListAsync();

    var limitedAgg = limitedData
        .Where(x => x.CouponId != 0)
        .GroupBy(x => new { x.CouponId, x.CouponName, x.BranchName, x.Price, x.SaleEventId })
        .Select(g => new
        {
            CouponId = g.Key.CouponId,
            CouponName = g.Key.CouponName,
            BranchName = g.Key.BranchName,
            Price = g.Key.Price,
            SaleEventId = g.Key.SaleEventId,
            FreeCount = g.Count(x => x.IsComplimentary),
            SoldCount = g.Count(x => !x.IsComplimentary),
            IsLimited = true
        }).ToList();

    // ⭐ Merge unlimited + limited รวม FreeCount และ FreeAmount
    var map = new Dictionary<int, (string Name, string Branch, bool IsLimited, int SoldCount, int FreeCount, decimal PaidAmount, decimal FreeAmount, int SaleEventId)>();

    foreach (var u in unlimitedAgg)
    {
        map[u.CouponId] = (
            u.CouponName ?? "ไม่พบข้อมูล", 
            u.BranchName ?? string.Empty, 
            u.IsLimited, 
            u.SoldCount,      // ⭐ จำนวนที่ขาย
            u.FreeCount,      // ⭐ จำนวน COM
            u.PaidAmount,     // ⭐ ราคาที่ขายได้
            u.FreeAmount,     // ⭐ มูลค่า COM
            u.SaleEventId
        );
    }

    foreach (var l in limitedAgg)
    {
        var paid = l.SoldCount * l.Price;
        var freeAmt = l.FreeCount * l.Price;

        if (map.TryGetValue(l.CouponId, out var existing))
        {
            var saleEventId = existing.SaleEventId != 0 ? existing.SaleEventId : l.SaleEventId;
            map[l.CouponId] = (
                existing.Name, 
                existing.Branch, 
                existing.IsLimited || l.IsLimited, 
                existing.SoldCount + l.SoldCount,      // ⭐ รวมจำนวนที่ขาย
                existing.FreeCount + l.FreeCount,      // ⭐ รวมจำนวน COM
                existing.PaidAmount + paid,            // ⭐ รวมราคาที่ขาย
                existing.FreeAmount + freeAmt,         // ⭐ รวมมูลค่า COM
                saleEventId
            );
        }
        else
        {
            map[l.CouponId] = (
                l.CouponName ?? "ไม่พบข้อมูล", 
                l.BranchName ?? string.Empty, 
                l.IsLimited, 
                l.SoldCount, 
                l.FreeCount, 
                paid, 
                freeAmt, 
                l.SaleEventId
            );
        }
    }

    // ✅ แก้ไขส่วน SummaryByCoupon results mapping (ประมาณบรรทัด 1510-1535)
    results = map.Select(kv =>
    {
        var saleEventName = string.Empty;
        if (kv.Value.SaleEventId != 0 && saleEventDict.TryGetValue(kv.Value.SaleEventId, out var evName))
            saleEventName = evName;

        return new SalesReportItem
        {
            ReceiptDate = DateTime.MinValue,
            ReceiptCode = string.Empty,
            CustomerName = string.Empty,
            SalesPersonName = string.Empty,
            CouponName = kv.Value.Name,
            BranchTypeName = kv.Value.Branch,
            IsLimited = kv.Value.IsLimited,
            Quantity = kv.Value.SoldCount,                          // จำนวนคูปองที่ขาย
            FreeCouponCount = kv.Value.FreeCount,                   // จำนวนคูปองที่ฟรี
            TotalCouponCount = kv.Value.SoldCount + kv.Value.FreeCount, // จำนวนทั้งหมด
            PaidCouponPrice = kv.Value.PaidAmount,                  // ราคาที่ขายได้
            FreeCouponPrice = kv.Value.FreeAmount,                  // ราคาของคูปองฟรี
            GrandTotalPrice = kv.Value.PaidAmount + kv.Value.FreeAmount,  // ✅ เพิ่มบรรทัดนี้
            TotalPrice = kv.Value.PaidAmount + kv.Value.FreeAmount, // มูลค่ารวม (เก็บไว้สำหรับ backward compatibility)
            SaleEventName = saleEventName
        };
    })
    .OrderBy(x => x.BranchTypeName)
    .ThenBy(x => x.CouponName)
    .ToList();
}
else if (ReportMode == ReportModes.RemainingCoupons)
{
    // ✅ RemainingCoupons - แสดงคูปองที่คงเหลือ โดยนับเฉพาะที่ขายในช่วงวันที่เลือก
    var limitedCoupons = await (from cd in context.CouponDefinitions
                                join ct in context.Branches on cd.BranchId equals ct.Id into ctj
                                from ct in ctj.DefaultIfEmpty()
                                where cd.IsLimited == true
                                select new
                                {
                                    CouponId = cd.Id,
                                    CouponCode = cd.Code,
                                    CouponName = cd.Name,
                                    BranchTypeName = ct != null ? ct.Name : string.Empty,
                                    BranchId = ct != null ? ct.Id : 0,
                                    UnitPrice = cd.Price,
                                    SaleEventId = cd.SaleEventId
                                }).ToListAsync();

    System.Diagnostics.Debug.WriteLine($"[RemainingCoupons] limitedCoupons.Count={limitedCoupons.Count}");
    foreach (var c in limitedCoupons)
    {
        System.Diagnostics.Debug.WriteLine($"[RemainingCoupons] CouponId={c.CouponId}, Code={c.CouponCode}, BranchId={c.BranchId}, Branch='{c.BranchTypeName}'");
    }

    // Apply filters (existing)
    if (SelectedBranch != null && SelectedBranch.Id != 0)
        limitedCoupons = limitedCoupons.Where(x => x.BranchId == SelectedBranch.Id).ToList();

    if (SelectedCoupon != null && SelectedCoupon.Id != 0)
        limitedCoupons = limitedCoupons.Where(x => x.CouponId == SelectedCoupon.Id).ToList();

    if (SelectedJob != null && SelectedJob.Id != 0)
        limitedCoupons = limitedCoupons.Where(x => x.SaleEventId == SelectedJob.Id).ToList();

    System.Diagnostics.Debug.WriteLine($"[RemainingCoupons] After filters: count={limitedCoupons.Count}");

    // Build results and log counts
    foreach (var coupon in limitedCoupons)
    {
        // ✅ จำนวนคูปองทั้งหมดที่สร้างไว้
        var totalCount = await context.GeneratedCoupons
                                     .Where(gc => gc.CouponDefinitionId == coupon.CouponId)
                                     .CountAsync();

        // ✅ จำนวนคูปองที่ขายในช่วงวันที่ที่เลือก (กรองด้วย ReceiptDate)
        var soldCount = await (from gc in context.GeneratedCoupons
                               where gc.CouponDefinitionId == coupon.CouponId 
                                    && gc.ReceiptItemId != null
                               join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                               join r in context.Receipts on ri.ReceiptId equals r.ReceiptID
                               where r.ReceiptDate >= startDateTime 
                                    && r.ReceiptDate < endDateTime
                                    && r.Status == "Active"  // นับเฉพาะใบเสร็จที่ยังไม่ยกเลิก
                               select gc).CountAsync();

        var remaining = totalCount - soldCount;

        System.Diagnostics.Debug.WriteLine($"[RemainingCoupons] CouponId={coupon.CouponId} Total={totalCount} Sold={soldCount} (in date range) Remaining={remaining}");

        results.Add(new SalesReportItem
        {
            CouponCode = coupon.CouponCode,
            CouponName = coupon.CouponName,
            BranchTypeName = coupon.BranchTypeName,
            UnitPrice = coupon.UnitPrice,
            TotalQuantity = totalCount,
            SoldQuantity = soldCount,
            RemainingQuantity = remaining,
            SaleEventName = (coupon.SaleEventId.HasValue && coupon.SaleEventId.Value != 0 && saleEventDict.TryGetValue(coupon.SaleEventId.Value, out var rn)) ? rn : string.Empty
        });
    }

    System.Diagnostics.Debug.WriteLine($"[RemainingCoupons] results list count={results.Count}");
}

// IMPORTANT: we don't assign results directly to ReportData anymore.
// Instead, we set the full results list and apply pagination.
SetFullResults(results);
        }
        catch (Exception ex)
        {
            // log full exception including stack trace to Output
            System.Diagnostics.Debug.WriteLine($"Error searching data: {ex}");
            // optionally surface to user (call ShowErrorDialog via page) — or raise event
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
            SelectedBranch = Branches.FirstOrDefault(); // เลือก "ทั้งหมด"
            SelectedCoupon = FilteredCoupons.FirstOrDefault(); // เลือก "ทั้งหมด"
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault(); // เลือก "ทั้งหมด"
            SelectedJob = Jobs.FirstOrDefault(); // เลือก "ทั้งหมด"
                                                     // ไม่ลบ ReportData ที่นี่ เพราะจะค้นหาใหม่
        }
        finally
        {
            _isUpdatingFilters = false;
        }

        // เรียกค้นหาข้อมูลใหม่หลังจากล้าง filter (นอก try-finally)
        ResetToFirstPage();
        _ = SearchDataAsync();
    }

    // ✅ แก้ไข UpdateSummary() method (แทนที่บรรทัด 1770-1813)
private void UpdateSummary()
{
    if (_totalItems == 0)
    {
        TotalRecordsText = "ไม่มีข้อมูล";
        TotalAmountText = "";
        TotalFreeCouponPriceText = "";
        TotalPaidCouponPriceText = "";
        TotalGrandPriceText = "";
    }
    else
    {
        TotalRecordsText = $"จำนวนรายการ: {_totalItems:N0} รายการ";
        
        decimal totalFree = 0m;
        decimal totalPaid = 0m;
        decimal totalGrand = 0m;
        
        // ✅ คำนวณสำหรับทุกประเภทรายงาน
        if (ReportMode == ReportModes.ByReceipt || 
            ReportMode == ReportModes.CancelledReceipts || 
            ReportMode == ReportModes.SummaryByCoupon)
        {
            // รายงานที่มี FreeCouponPrice, PaidCouponPrice, GrandTotalPrice
            totalFree = _allResults.Sum(x => x.FreeCouponPrice);
            totalPaid = _allResults.Sum(x => x.PaidCouponPrice);
            totalGrand = _allResults.Sum(x => x.GrandTotalPrice);
        }
        else if (ReportMode == ReportModes.LimitedCoupons || 
                 ReportMode == ReportModes.UnlimitedGrouped || 
                 ReportMode == ReportModes.CancelledCoupons)
        {
            // ✅ รายงานที่มี TotalPrice - แยกตาม IsComplimentary
            totalFree = _allResults.Where(x => x.IsComplimentary).Sum(x => x.TotalPrice);
            totalPaid = _allResults.Where(x => !x.IsComplimentary).Sum(x => x.TotalPrice);
            totalGrand = totalFree + totalPaid;
        }
        else if (ReportMode == ReportModes.RemainingCoupons)
        {
            // ✅ RemainingCoupons - คำนวณจาก UnitPrice * quantities
            totalFree = 0m; // ไม่มีคูปองฟรี
            totalPaid = _allResults.Sum(x => x.UnitPrice * x.SoldQuantity);
            totalGrand = totalPaid;
        }
        
        // ✅ แสดงผลแบบเดียวกันทุกรายงาน
        TotalFreeCouponPriceText = $"ราคาคูปองฟรี (รวมทุกรายการ): {totalFree:N2} บาท";
        TotalPaidCouponPriceText = $"ราคาที่จ่าย (รวมทุกรายการ): {totalPaid:N2} บาท";
        TotalGrandPriceText = $"มูลค่ารวมสุทธิ ({totalFree:N2} + {totalPaid:N2}): {totalGrand:N2} บาท";
        TotalAmountText = ""; // ไม่ใช้แล้ว
    }

    OnPropertyChanged(nameof(TotalRecordsText));
    OnPropertyChanged(nameof(TotalAmountText));
    OnPropertyChanged(nameof(TotalFreeCouponPriceText));
    OnPropertyChanged(nameof(TotalPaidCouponPriceText));
    OnPropertyChanged(nameof(TotalGrandPriceText));
}

    public string GetFilterSummary()
    {
        var filters = new List<string>();

        if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
            filters.Add($"เซล{SelectedSalesPerson.Name}");

        if (SelectedBranch != null && SelectedBranch.Id != 0)
            filters.Add($"สาขา{SelectedBranch.Name}");

        if (SelectedCoupon != null && SelectedCoupon.Id != 0)
            filters.Add($"คูปอง{SelectedCoupon.Name}");

        if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
            filters.Add($"ชำระ{SelectedPaymentMethod.Name}");

        if (SelectedJob != null && SelectedJob.Id != 0)
            filters.Add($"งาน{SelectedJob.Name}");

        filters.Add(ReportMode.ToString());
        return string.Join("_", filters);
    }

    // Replace the existing ExportToCsvAsync method in SalesReportViewModel with this implementation
public async Task ExportToCsvAsync(StorageFile file)
{
    var csvContent = new StringBuilder();

    // Add header with filter information
    csvContent.AppendLine($"\"รายงานการขาย - {DateTime.Now:dd/MM/yyyy HH:mm}\"");
    csvContent.AppendLine($"\"ช่วงวันที่: {StartDate?.ToString("dd/MM/yyyy")} - {EndDate?.ToString("dd/MM/yyyy")}\"");

    // Add filter details if any
    if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
        csvContent.AppendLine($"\"เซลที่เลือก: {SelectedSalesPerson.Name}\"");
    if (SelectedBranch != null && SelectedBranch.Id != 0)
        csvContent.AppendLine($"\"สาขา: {SelectedBranch.Name}\"");
    if (SelectedCoupon != null && SelectedCoupon.Id != 0)
        csvContent.AppendLine($"\"คูปอง: {SelectedCoupon.Name}\"");
    if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
        csvContent.AppendLine($"\"วิธีการชำระเงิน: {SelectedPaymentMethod.Name}\"");
    if (SelectedJob != null && SelectedJob.Id != 0)
        csvContent.AppendLine($"\"ชื่องาน: {SelectedJob.Name}\"");

    csvContent.AppendLine($"\"รายงานแบบ: {ReportMode}\"");

    // ✅ คำนวณยอดรวมตามประเภทรายงาน (เหมือน UpdateSummary())
    decimal totalFree = 0m;
    decimal totalPaid = 0m;
    decimal totalGrand = 0m;

    if (ReportMode == ReportModes.ByReceipt || 
        ReportMode == ReportModes.CancelledReceipts || 
        ReportMode == ReportModes.SummaryByCoupon)
    {
        totalFree = AllResults.Sum(x => x.FreeCouponPrice);
        totalPaid = AllResults.Sum(x => x.PaidCouponPrice);
        totalGrand = AllResults.Sum(x => x.GrandTotalPrice);
    }
    else if (ReportMode == ReportModes.LimitedCoupons || 
             ReportMode == ReportModes.UnlimitedGrouped || 
             ReportMode == ReportModes.CancelledCoupons)
    {
        totalFree = AllResults.Where(x => x.IsComplimentary).Sum(x => x.TotalPrice);
        totalPaid = AllResults.Where(x => !x.IsComplimentary).Sum(x => x.TotalPrice);
        totalGrand = totalFree + totalPaid;
    }
    else if (ReportMode == ReportModes.RemainingCoupons)
    {
        totalFree = 0m;
        totalPaid = AllResults.Sum(x => x.UnitPrice * x.SoldQuantity);
        totalGrand = totalPaid;
    }

    // ✅ แสดงผลเหมือนกันทุกรายงาน
    csvContent.AppendLine($"\"จำนวนรายการทั้งหมด: {TotalItems:N0} รายการ\"");
    csvContent.AppendLine($"\"ราคาคูปองฟรี (รวมทุกรายการ): {totalFree:N2} บาท\"");
    csvContent.AppendLine($"\"ราคาที่จ่าย (รวมทุกรายการ): {totalPaid:N2} บาท\"");
    csvContent.AppendLine($"\"มูลค่ารวมสุทธิ ({totalFree:N2} + {totalPaid:N2}): {totalGrand:N2} บาท\"");

    csvContent.AppendLine(); // Empty line

    // Column headers and rows vary by report mode (match XAML columns)
    if (ReportMode == ReportModes.ByReceipt)
    {
        // Match XAML ByReceipt columns (Active receipts - no cancellation reason)
        var headers = new[]
        {
            "วันที่", "เลขที่ใบเสร็จ", "ลูกค้า", "เบอร์โทร", "เซล", "การชำระเงิน",
            "จำนวนคูปองที่จ่าย", "จำนวนคูปองฟรี", "จำนวนทั้งหมด",
            "ราคาคูปองฟรี", "ราคาที่จ่าย", "มูลค่ารวม"
        };
        csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

        foreach (var item in AllResults)
        {
            var row = new[]
            {
                item.ReceiptDateDisplay,
                item.ReceiptCode,
                item.CustomerName,
                item.CustomerPhone,
                item.SalesPersonName,
                item.PaymentMethodName,
                item.PaidCouponCount.ToString(),
                item.FreeCouponCount.ToString(),
                item.TotalCouponCount.ToString(),
                item.FreeCouponPrice.ToString("F2"),
                item.PaidCouponPrice.ToString("F2"),
                item.GrandTotalPrice.ToString("F2")
            };
            csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{(field ?? "").Replace("\"", "\"\"")}\"")));
        }
        
        // ✅ เพิ่มแถวสรุปท้ายตาราง
        csvContent.AppendLine(); // Empty line before summary
        csvContent.AppendLine($"\"รวม\",\"\",\"\",\"\",\"\",\"\"," +
            $"\"{AllResults.Sum(x => x.PaidCouponCount)}\"," +
            $"\"{AllResults.Sum(x => x.FreeCouponCount)}\"," +
            $"\"{AllResults.Sum(x => x.TotalCouponCount)}\"," +
            $"\"{AllResults.Sum(x => x.FreeCouponPrice):F2}\"," +
            $"\"{AllResults.Sum(x => x.PaidCouponPrice):F2}\"," +
            $"\"{AllResults.Sum(x => x.GrandTotalPrice):F2}\"");
    }
    else if (ReportMode == ReportModes.CancelledReceipts)
    {
        // ⭐ Match XAML CancelledReceipts columns (include cancellation reason)
        var headers = new[]
        {
            "วันที่", "เลขที่ใบเสร็จ", "ลูกค้า", "เบอร์โทร", "เซล", "การชำระเงิน",
            "จำนวนคูปองที่จ่าย", "จำนวนคูปองฟรี", "จำนวนทั้งหมด",
            "ราคาคูปองฟรี", "ราคาที่จ่าย", "มูลค่ารวม", "เหตุผลยกเลิก"
        };
        csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

        foreach (var item in AllResults)
        {
            var row = new[]
            {
                item.ReceiptDateDisplay,
                item.ReceiptCode,
                item.CustomerName,
                item.CustomerPhone,
                item.SalesPersonName,
                item.PaymentMethodName,
                item.PaidCouponCount.ToString(),
                item.FreeCouponCount.ToString(),
                item.TotalCouponCount.ToString(),
                item.FreeCouponPrice.ToString("F2"),
                item.PaidCouponPrice.ToString("F2"),
                item.GrandTotalPrice.ToString("F2"),
                item.CancellationReason  // ⭐ เพิ่มเหตุผลยกเลิก
            };
            csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{(field ?? "").Replace("\"", "\"\"")}\"")));
        }
        
        // ✅ เพิ่มแถวสรุปท้ายตาราง
        csvContent.AppendLine(); // Empty line before summary
        csvContent.AppendLine($"\"รวม\",\"\",\"\",\"\",\"\",\"\"," +
            $"\"{AllResults.Sum(x => x.PaidCouponCount)}\"," +
            $"\"{AllResults.Sum(x => x.FreeCouponCount)}\"," +
            $"\"{AllResults.Sum(x => x.TotalCouponCount)}\"," +
            $"\"{AllResults.Sum(x => x.FreeCouponPrice):F2}\"," +
            $"\"{AllResults.Sum(x => x.PaidCouponPrice):F2}\"," +
            $"\"{AllResults.Sum(x => x.GrandTotalPrice):F2}\"," +
            $"\"\"");  // Empty cell for cancellation reason
    }
    else if (ReportMode == ReportModes.LimitedCoupons || ReportMode == ReportModes.CancelledCoupons)
    {
        // Match XAML LimitedCoupons/CancelledCoupons columns (added SaleEvent)
        var headers = new[]
        {
            "วันที่", "เลขที่ใบเสร็จ", "รหัสคูปอง", "คูปอง", "ลูกค้า", "เบอร์โทร",
            "เซล", "วันหมดอายุ", "ราคา", "งานที่ออกขาย", "สถานะ"  // ✅ เพิ่มคอลัมน์สถานะ
        };
        
        // ⭐ เพิ่มเหตุผลยกเลิกสำหรับ CancelledCoupons
        if (ReportMode == ReportModes.CancelledCoupons)
        {
            headers = new[]
            {
                "วันที่", "เลขที่ใบเสร็จ", "รหัสคูปอง", "คูปอง", "ลูกค้า", "เบอร์โทร",
                "เซล", "วันหมดอายุ", "ราคา", "งานที่ออกขาย", "สถานะ", "เหตุผลยกเลิก"
            };
        }
        
        csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

        foreach (var item in AllResults)
        {
            // ✅ ถ้า IsCOM ให้ราคาเป็น 0 และระบุสถานะ
            var displayPrice = item.IsComplimentary ? "0.00" : item.TotalPrice.ToString("F2");
            var status = item.IsComplimentary ? "COM (ฟรี)" : "ขายปกติ";
            
            var row = ReportMode == ReportModes.CancelledCoupons
                ? new[]
                {
                    item.ReceiptDateDisplay,
                    item.ReceiptCode,
                    item.GeneratedCode,
                    item.CouponName,
                    item.CustomerName,
                    item.CustomerPhone,
                    item.SalesPersonName,
                    item.ExpiresAtDisplay,
                    displayPrice,  // ✅ ใช้ displayPrice แทน item.TotalPrice
                    item.SaleEventName,
                    status,  // ✅ เพิ่มสถานะ
                    item.CancellationReason
                }
                : new[]
                {
                    item.ReceiptDateDisplay,
                    item.ReceiptCode,
                    item.GeneratedCode,
                    item.CouponName,
                    item.CustomerName,
                    item.CustomerPhone,
                    item.SalesPersonName,
                    item.ExpiresAtDisplay,
                    displayPrice,  // ✅ ใช้ displayPrice แทน item.TotalPrice
                    item.SaleEventName,
                    status  // ✅ เพิ่มสถานะ
                };
            csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{(field ?? "").Replace("\"", "\"\"")}\"")));
        }
    }
    else if (ReportMode == ReportModes.UnlimitedGrouped)
    {
        // Match XAML UnlimitedGrouped columns
        var headers = new[]
        {
            "วันที่", "เลขที่ใบเสร็จ", "คูปอง", "ลูกค้า", "เบอร์โทร",
            "เซล", "วันหมดอายุ", "จำนวน", "รวม", "สถานะ"  // ✅ เพิ่มคอลัมน์สถานะ
        };
        csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

        foreach (var item in AllResults)
        {
            // ✅ ถ้า IsCOM ให้ราคาเป็น 0 และระบุสถานะ
            var displayPrice = item.IsComplimentary ? "0.00" : item.TotalPrice.ToString("F2");
            var status = item.IsComplimentary ? "COM (ฟรี)" : "ขายปกติ";
            
            var row = new[]
            {
                item.ReceiptDateDisplay,
                item.ReceiptCode,
                item.CouponName,
                item.CustomerName,
                item.CustomerPhone,
                item.SalesPersonName,
                item.ExpiresAtDisplay,
                item.Quantity.ToString(),
                displayPrice,  // ✅ ใช้ displayPrice แทน item.TotalPrice
                status  // ✅ เพิ่มสถานะ
            };
            csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{(field ?? "").Replace("\"", "\"\"")}\"")));
        }
    }
    else if (ReportMode == ReportModes.SummaryByCoupon)
    {
        // Extended Summary to match XAML (includes SaleEvent, free count, totals, paid/free amounts)
        var headers = new[]
        {
            "คูปอง", "สาขา", "งานที่ออกขาย", "จำกัด/ไม่จำกัด",
            "จำนวนคูปองที่ขาย", "จำนวนคูปองที่ฟรี", "จำนวนคูปองทั้งหมด",
            "ราคาที่ขายได้", "ราคาของคูปองฟรี", "มูลค่ารวม"
        };
        csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

        foreach (var item in AllResults)
        {
            var row = new[]
            {
                item.CouponName,
                item.BranchTypeName,
                item.SaleEventName,
                item.IsLimitedDisplay,
                item.Quantity.ToString(),                // sold count
                item.FreeCouponCount.ToString(),         // free count (may be 0)
                item.TotalCouponCount.ToString(),        // total
                item.PaidCouponPrice.ToString("F2"),     // paid amount
                item.FreeCouponPrice.ToString("F2"),     // free amount
                item.GrandTotalPrice.ToString("F2")      // ✅ เปลี่ยนจาก TotalPrice เป็น GrandTotalPrice
            };
            csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{(field ?? "").Replace("\"", "\"\"")}\"")));
        }
        
        // ✅ เพิ่มแถวสรุปท้ายตาราง
        csvContent.AppendLine(); // Empty line before summary
        csvContent.AppendLine($"\"รวม\",\"\",\"\",\"\"," +
            $"\"{AllResults.Sum(x => x.Quantity)}\"," +
            $"\"{AllResults.Sum(x => x.FreeCouponCount)}\"," +
            $"\"{AllResults.Sum(x => x.TotalCouponCount)}\"," +
            $"\"{AllResults.Sum(x => x.PaidCouponPrice):F2}\"," +
            $"\"{AllResults.Sum(x => x.FreeCouponPrice):F2}\"," +
            $"\"{AllResults.Sum(x => x.GrandTotalPrice):F2}\"");
    }
            else if (ReportMode == ReportModes.RemainingCoupons)
            {
                // Match XAML RemainingCoupons columns
                var headers = new[]
                {
            "รหัสคูปอง", "ชื่อคูปอง", "สาขา", "ราคา",
            "จำนวนทั้งหมด", "จำนวนที่ขายแล้ว", "จำนวนคงเหลือ",
            "งานที่ออกขาย"
        };
                csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

                foreach (var item in AllResults)
                {
                    var row = new[]
                    {
                item.CouponCode,
                item.CouponName,
                item.BranchTypeName,
                item.UnitPrice.ToString("F2"),
                item.TotalQuantity.ToString(),
                item.SoldQuantity.ToString(),
                item.RemainingQuantity.ToString(),
                item.SaleEventName
            };
                    csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{(field ?? "").Replace("\"", "\"\"")}\"")));
                }

                // เพิ่มแถวสรุปท้ายตาราง
                csvContent.AppendLine(); // Empty line before summary
                csvContent.AppendLine($"\"รวม\",\"\",\"\"," +
                    $"\"{AllResults.Sum(x => x.UnitPrice * x.TotalQuantity):F2}\"," +
                    $"\"{AllResults.Sum(x => x.TotalQuantity)}\"," +
                    $"\"{AllResults.Sum(x => x.SoldQuantity)}\"," +
                    $"\"{AllResults.Sum(x => x.RemainingQuantity)}\"," +
                    $"\"\"");
            }

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
        public string ReceiptStatus { get; set; } = "Active";
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string SalesPersonName { get; set; } = "";
        public string CouponCode { get; set; } = "";
        public string CouponName { get; set; } = "";
        public string BranchTypeName { get; set; } = "";
        public string PaymentMethodName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal NetTotal => TotalPrice - Discount;
        public DateTime? ExpiresAt { get; set; }
        public bool IsLimited { get; set; }

        public string SaleEventName { get; set; } = "";
        public string CancellationReason { get; set; } = "";

        public int TotalCouponCount { get; set; }
        public int FreeCouponCount { get; set; }
        
        // ✅ เปลี่ยนเป็น property ที่ set ได้
        public int PaidCouponCount { get; set; }

        public decimal FreeCouponPrice { get; set; }
        public decimal PaidCouponPrice { get; set; }
        
        // ✅ เปลี่ยนเป็น property ที่ set ได้
        public decimal GrandTotalPrice { get; set; }

        public int TotalQuantity { get; set; }
        public int SoldQuantity { get; set; }
        public int RemainingQuantity { get; set; }

        public string IsLimitedDisplay => IsLimited ? "จำกัด" : "ไม่จำกัด";
        public string ReceiptStatusDisplay => ReceiptStatus == "Cancelled" ? "ยกเลิก" : "ใช้งาน";

        public Microsoft.UI.Xaml.Media.Brush ReceiptStatusColor
        {
            get
            {
                return ReceiptStatus == "Cancelled"
                     ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red)
                       : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
            }
        }

        public string ReceiptDateDisplay => ReceiptDate == DateTime.MinValue ? "" : ReceiptDate.ToString("dd/MM/yyyy HH:mm");
        public string UnitPriceDisplay => UnitPrice.ToString("N2");
        public string TotalPriceDisplay => TotalPrice.ToString("N2");
        public string DiscountDisplay => Discount.ToString("N2");
        public string NetTotalDisplay => NetTotal.ToString("N2");
        public string ExpiresAtDisplay => ExpiresAt.HasValue ? ExpiresAt.Value.ToString("dd/MM/yyyy") : string.Empty;
        public string GeneratedCode { get; set; } = "";

        public string FreeCouponPriceDisplay => FreeCouponPrice.ToString("N2");
        public string PaidCouponPriceDisplay => PaidCouponPrice.ToString("N2");
        public string GrandTotalPriceDisplay => GrandTotalPrice.ToString("N2");

        public bool IsComplimentary { get; set; } = false;

        public Microsoft.UI.Xaml.Media.Brush CouponCodeColor
        {
            get
            {
                var defaultBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as Microsoft.UI.Xaml.Media.Brush
                           ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
                if (IsComplimentary)
                {
                    return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Yellow);
                }
                return defaultBrush;
            }
        }

        public Microsoft.UI.Xaml.Media.Brush PriceColor
        {
            get
            {
                var defaultBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as Microsoft.UI.Xaml.Media.Brush
                           ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);

                // color price yellow when price equals 0 (user requested) — applies for TotalPrice or UnitPrice
                if (TotalPrice == 0m || UnitPrice == 0m)
                    return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Yellow);

                return defaultBrush;
            }
        }

        public Microsoft.UI.Xaml.Media.Brush RemainingQuantityColor
        {
            get
            {
                if (TotalQuantity == 0) return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);

                var percentage = (double)RemainingQuantity / TotalQuantity * 100;

                if (percentage <= 10)
                    return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                else if (percentage <= 30)
                    return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                else
                    return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
            }
        }
    }
}