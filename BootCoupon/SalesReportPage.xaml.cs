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
       else if (tag == "SummaryByCoupon" ) ViewModel.ReportMode = SalesReportViewModel.ReportModes.SummaryByCoupon;
     else if (tag == "RemainingCoupons" ) ViewModel.ReportMode = SalesReportViewModel.ReportModes.RemainingCoupons;
                else if (tag == "CancelledReceipts") ViewModel.ReportMode = SalesReportViewModel.ReportModes.CancelledReceipts;
  else if (tag == "CancelledCoupons") ViewModel.ReportMode = SalesReportViewModel.ReportModes.CancelledCoupons;

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
            await ViewModel.LoadDataAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        private void BranchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                                   $"ยอดรวมสุทธิ: {ViewModel.ReportData.Sum(x => x.TotalPrice - x.Discount):N2} บาท\n" +
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

        private DateTimeOffset? _startDate = DateTime.Today.AddDays(-30);
        private DateTimeOffset? _endDate = DateTime.Today;
        private SalesPerson? _selectedSalesPerson;
        private Branch? _selectedBranch;
        private CouponDefinition? _selectedCoupon; // changed to CouponDefinition
        private PaymentMethod? _selectedPaymentMethod;
        private ObservableCollection<SalesReportItem> _reportData = new();
        private ObservableCollection<CouponDefinition> _filteredCoupons = new(); // changed type
        private bool _isUpdatingFilters = false;
  private bool _isInitialLoad = true;
        // ลบตัวแปร _showCancelledReceipts และ _showCancelledCoupons

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
        public ObservableCollection<Branch> Branches { get; } = new();
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

        // ลบ properties ShowCancelledReceipts และ ShowCancelledCoupons

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
                var branches = await context.Branches.ToListAsync();
                Branches.Clear();
                // เพิ่มตัวเลือก "ทั้งหมด" เป็นรายการแรก
                Branches.Add(new Branch { Id = 0, Name = "ทั้งหมด" });
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
                newFilteredCoupons.Add(new CouponDefinition { Id = 0, Name = "ทั้งหมด", Price = 0, Code = "", BranchId = 0 });

                var filtered = SelectedBranch == null || SelectedBranch.Id == 0 ?
                    AllCoupons :
                    AllCoupons.Where(c => c.BranchId == SelectedBranch.Id);

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

     // Base query for receipt items joined with coupon definition and related data
            var baseQuery = from r in context.Receipts
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
   CouponValidTo = c != null ? c.ValidTo : (DateTime?)null
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

 // If coupon filters are selected, build a single receipt-items query (joined with CouponDefinitions)
 // and use it both to filter which receipts to include and to compute per-receipt aggregates.
 IQueryable<DatabaseReceiptItem> baseReceiptItemsQuery = context.ReceiptItems.Where(ri => receiptIds.Contains(ri.ReceiptId));

 if ((SelectedCoupon != null && SelectedCoupon.Id !=0) || (SelectedBranch != null && SelectedBranch.Id !=0))
 {
 // join to CouponDefinitions to allow filtering by BranchId when needed
 var joined = from ri in context.ReceiptItems
 join cd in context.CouponDefinitions on ri.CouponId equals cd.Id into cdj
 from cd in cdj.DefaultIfEmpty()
 where receiptIds.Contains(ri.ReceiptId)
 select new { ri, cd };

 if (SelectedCoupon != null && SelectedCoupon.Id !=0)
 {
 joined = joined.Where(x => x.ri.CouponId == SelectedCoupon.Id);
 }

 if (SelectedBranch != null && SelectedBranch.Id != 0)
 {
     joined = joined.Where(x => x.cd != null && x.cd.BranchId == SelectedBranch.Id);
 }

 var matchingReceiptIds = await joined.Select(x => x.ri.ReceiptId).Distinct().ToListAsync();
 filteredReceipts = filteredReceipts.Where(r => matchingReceiptIds.Contains(r.ReceiptID));

 // set baseReceiptItemsQuery to only the filtered receipt items (matching coupon/type)
 baseReceiptItemsQuery = joined.Select(x => x.ri);
 }

 // Compute per-receipt aggregates (quantity & total) using baseReceiptItemsQuery
 var grouped = await baseReceiptItemsQuery
 .GroupBy(ri => ri.ReceiptId)
 .Select(g => new { ReceiptId = g.Key, TotalQty = g.Sum(ri => ri.Quantity), TotalAmount = g.Sum(ri => ri.TotalPrice) })
 .ToListAsync();

 var receiptItemCounts = grouped.ToDictionary(x => x.ReceiptId, x => x.TotalQty);
 var receiptItemTotals = grouped.ToDictionary(x => x.ReceiptId, x => x.TotalAmount);

 // Map receipts to report rows, using filtered item aggregates (fallback to0 if none)
 results = filteredReceipts.Select(r => new SalesReportItem
 {
 ReceiptDate = r.ReceiptDate,
 ReceiptCode = r.ReceiptCode,
 ReceiptStatus = r.Status,
 CustomerName = r.CustomerName ?? "ไม่ระบุ",
 CustomerPhone = r.CustomerPhoneNumber ?? string.Empty,
 SalesPersonName = r.SalesPersonId.HasValue && salesPersonDict.ContainsKey(r.SalesPersonId.Value) ? salesPersonDict[r.SalesPersonId.Value] : "ไม่ระบุ",
 PaymentMethodName = r.PaymentMethodId.HasValue && paymentMethodDict.ContainsKey(r.PaymentMethodId.Value) ? paymentMethodDict[r.PaymentMethodId.Value] : "ไม่ระบุ",
 Quantity = receiptItemCounts.ContainsKey(r.ReceiptID) ? receiptItemCounts[r.ReceiptID] :0,
 UnitPrice =0m,
 TotalPrice = receiptItemTotals.ContainsKey(r.ReceiptID) ? receiptItemTotals[r.ReceiptID] :0m,
 // Use receipt-level Discount when available
 Discount = r.Discount
 }).OrderBy(x => x.ReceiptDate).ThenBy(x => x.ReceiptCode).ToList();
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
           // ดึง ReceiptItems ที่เชื่อมกับใบเสร็จที่ยกเลิก
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
   // Left join กับ GeneratedCoupons เพื่อดึง code (ถ้าหมี)
  join gc in context.GeneratedCoupons on ri.ReceiptItemId equals gc.ReceiptItemId into gcj
         from gc in gcj.DefaultIfEmpty()
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
 GeneratedCode = gc != null ? gc.GeneratedCode : null,
   ExpiresAt = gc != null ? gc.ExpiresAt : (cd != null ? cd.ValidTo : (DateTime?)null),
 UnitPrice = ri.UnitPrice,
  TotalPrice = ri.TotalPrice,
    PaymentMethodName = pm != null ? pm.Name : null,
  PaymentMethodId = pm != null ? pm.Id : 0
         };
  
         // Apply coupon filters
        if (SelectedBranch != null && SelectedBranch.Id != 0)
         cancelledItemsQuery = cancelledItemsQuery.Where(x => x.BranchId == SelectedBranch.Id);

        if (SelectedCoupon != null && SelectedCoupon.Id != 0)
        cancelledItemsQuery = cancelledItemsQuery.Where(x => x.CouponDefinitionId == SelectedCoupon.Id);
         
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
    Quantity = 1 // แต่ละแถว = 1 coupon
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
           PaymentMethodId = pm != null ? pm.Id : 0
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
       ExpiresAt = x.ExpiresAt
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
    PaymentMethodId = pm != null ? pm.Id : 0
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
 UnitPrice = x.UnitPrice,
      TotalPrice = x.TotalPrice,
      GeneratedCode = x.GeneratedCode,
      ExpiresAt = x.ExpiresAt
      })
    .OrderBy(x => x.ReceiptDate)
 .ThenBy(x => x.ReceiptCode)
    .ThenBy(x => x.CouponName)
    .ToList();
     }
   }
     else if (ReportMode == ReportModes.SummaryByCoupon)
     {
        // Summary ไม่รวมใบเสร็จที่ยกเลิก (แสดงเฉพาะ Active)
      var statusFilter = "Active";

     var unlimited = await baseQuery
      .Where(x => x.IsLimited == false && x.ReceiptStatus == statusFilter)
     .GroupBy(x => new { x.CouponId, x.CouponName, x.BranchName })
     .Select(g => new
     {
     CouponId = g.Key.CouponId,
   CouponName = g.Key.CouponName,
        BranchName = g.Key.BranchName,
         Quantity = g.Sum(x => x.Quantity),
      TotalPrice = g.Sum(x => x.TotalPrice),
    IsLimited = false
 }).ToListAsync();

      var limitedQuery = from gc in context.GeneratedCoupons
  where gc.ReceiptItemId != null
    join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
  join r in context.Receipts on ri.ReceiptId equals r.ReceiptID
         join cd in context.CouponDefinitions on gc.CouponDefinitionId equals cd.Id into cdj
        from cd in cdj.DefaultIfEmpty()
         join ct in context.Branches on cd.BranchId equals ct.Id into ctj
             from ct in ctj.DefaultIfEmpty()
          where r.ReceiptDate >= startDateTime && r.ReceiptDate < endDateTime
    && r.Status == statusFilter
            select new { gc, cd, ct, r };

         if (SelectedBranch != null && SelectedBranch.Id != 0)
     {
      limitedQuery = limitedQuery.Where(x => x.cd != null && x.cd.BranchId == SelectedBranch.Id);
          }
  if (SelectedCoupon != null && SelectedCoupon.Id != 0)
       {
            limitedQuery = limitedQuery.Where(x => x.cd != null && x.cd.Id == SelectedCoupon.Id);
       }

 var limitedData = await limitedQuery.ToListAsync();

        var limited = limitedData
   .GroupBy(x => new 
     { 
        Id = x.cd != null ? x.cd.Id : 0,
    Name = x.cd != null ? x.cd.Name : string.Empty,
    BranchName = x.ct != null ? x.ct.Name : string.Empty
                 })
        .Select(g => new
        {
   CouponId = g.Key.Id,
 CouponName = g.Key.Name,
        BranchName = g.Key.BranchName,
         Quantity = g.Count(),
   TotalPrice = g.Sum(x => x.cd != null ? x.cd.Price : 0m),
      IsLimited = true
      }).ToList();

        var map = new Dictionary<int, (string Name, string Type, int Qty, decimal Total, bool IsLimited)>();

      foreach (var u in unlimited)
  {
     map[u.CouponId] = (u.CouponName ?? "ไม่พบข้อมูล", u.BranchName ?? string.Empty, (int)u.Quantity, u.TotalPrice, u.IsLimited);
         }

     foreach (var l in limited)
           {
   if (map.TryGetValue(l.CouponId, out var existing))
         {
     map[l.CouponId] = (existing.Name, existing.Type, existing.Qty + l.Quantity, existing.Total + l.TotalPrice, existing.IsLimited || l.IsLimited);
        }
 else
       {
      map[l.CouponId] = (l.CouponName ?? "ไม่พบข้อมูล", l.BranchName ?? string.Empty, l.Quantity, l.TotalPrice, l.IsLimited);
}
        }

    results = map.Select(kv => new SalesReportItem
       {
   ReceiptDate = DateTime.MinValue,
       ReceiptCode = string.Empty,
    CustomerName = string.Empty,
    SalesPersonName = string.Empty,
 CouponName = kv.Value.Name,
   BranchTypeName = kv.Value.Type,
              PaymentMethodName = string.Empty,
      Quantity = kv.Value.Qty,
           UnitPrice = 0,
       TotalPrice = kv.Value.Total,
               IsLimited = kv.Value.IsLimited
   })
         .OrderBy(x => x.BranchTypeName)
   .ThenBy(x => x.CouponName)
    .ToList();
}
            else if (ReportMode == ReportModes.RemainingCoupons)
       {
 // RemainingCoupons ไม่ได้รับผลกระทบจากใบเสร็จที่ยกเลิก
  // เพราะนับจำนวนคูปองที่มี/ขายทั้งหมด
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
     UnitPrice = cd.Price
   }).ToListAsync();

    if (SelectedBranch != null && SelectedBranch.Id != 0)
     limitedCoupons = limitedCoupons.Where(x => x.BranchId == SelectedBranch.Id).ToList();

  if (SelectedCoupon != null && SelectedCoupon.Id != 0)
    limitedCoupons = limitedCoupons.Where(x => x.CouponId == SelectedCoupon.Id).ToList();

   foreach (var coupon in limitedCoupons)
    {
          var totalCount = await context.GeneratedCoupons
   .Where(gc => gc.CouponDefinitionId == coupon.CouponId)
    .CountAsync();

 var soldCount = await context.GeneratedCoupons
        .Where(gc => gc.CouponDefinitionId == coupon.CouponId && gc.ReceiptItemId != null)
      .CountAsync();

        var remaining = totalCount - soldCount;

       results.Add(new SalesReportItem
 {
      CouponCode = coupon.CouponCode,
             CouponName = coupon.CouponName,
            BranchTypeName = coupon.BranchTypeName,
            UnitPrice = coupon.UnitPrice,
  TotalQuantity = totalCount,
         SoldQuantity = soldCount,
        RemainingQuantity = remaining
 });
 }

      results = results.OrderBy(x => x.CouponCode).ToList();
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
         SelectedBranch = Branches.FirstOrDefault(); // เลือก "ทั้งหมด"
         SelectedCoupon = FilteredCoupons.FirstOrDefault(); // เลือก "ทั้งหมด"
         SelectedPaymentMethod = PaymentMethods.FirstOrDefault(); // เลือก "ทั้งหมด"
  // ไม่ล้าง ReportData ที่นี่ เพราะจะค้นหาใหม่
}
          finally
     {
        _isUpdatingFilters = false;
            }
 
    // เรียกค้นหาข้อมูลใหม่หลังจากล้าง filter (นอก try-finally)
            _ = SearchDataAsync();
  }

        private void UpdateSummary()
        {
            if (ReportData.Count ==0)
            {
                TotalRecordsText = "ไม่มีข้อมูล";
                TotalAmountText = "";
            }
            else
            {
                var totalAmount = ReportData.Sum(x => x.TotalPrice);
 var totalDiscount = ReportData.Sum(x => x.Discount);
 var netAmount = totalAmount - totalDiscount;
                TotalRecordsText = $"จำนวนรายการ: {ReportData.Count:N0} รายการ";
 // Show calculation: รวม - ลดราคา = ยอดรวม
 TotalAmountText = $"ยอดรวม: {netAmount:N2} บาท";
            }

            OnPropertyChanged(nameof(TotalRecordsText));
            OnPropertyChanged(nameof(TotalAmountText));
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
        if (SelectedBranch != null && SelectedBranch.Id != 0)
      csvContent.AppendLine($"\"สาขา: {SelectedBranch.Name}\"");
     if (SelectedCoupon != null && SelectedCoupon.Id != 0)
    csvContent.AppendLine($"\"คูปอง: {SelectedCoupon.Name}\"");
    if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
      csvContent.AppendLine($"\"วิธีการชำระเงิน: {SelectedPaymentMethod.Name}\"");
            
  csvContent.AppendLine($"\"รายงานแบบ: {ReportMode}\"");
 csvContent.AppendLine($"\"จำนวนรายการทั้งหมด: {ReportData.Count:N0} รายการ\"");
 csvContent.AppendLine($"\"ยอดรวมทั้งหมด (สุทธิ): {ReportData.Sum(x => x.TotalPrice - x.Discount):N2} บาท\"");
 csvContent.AppendLine(); // Empty line

        // Column headers and rows vary by report mode
    if (ReportMode == ReportModes.ByReceipt)
    {
          var headers = new[]
     {
         "วันที่", "เลขที่ใบเสร็จ", "ลูกค้า", "เบอร์โทร", "เซล", "การชำระเงิน",
 "จำนวน", "รวม"
+ "จำนวน", "ส่วนลด", "ยอดสุทธิ"
         };
      csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

   foreach (var item in ReportData)
     {
        var row = new[]
          {
     item.ReceiptDateDisplay, item.ReceiptCode, item.CustomerName,
  item.CustomerPhone, item.SalesPersonName, item.PaymentMethodName,
 item.Quantity.ToString(), item.TotalPrice.ToString("F2")
+ item.Quantity.ToString(), item.Discount.ToString("F2"), (item.TotalPrice - item.Discount).ToString("F2")
         };
          csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{field?.Replace("\"", "\"\"")}\"")));
           }
}
            else if (ReportMode == ReportModes.CancelledReceipts)
     {
// CancelledReceipts: เหมือน ByReceipt แต่เพิ่มคอลัมน์สถานะ
       var headers = new[]
    {
             "วันที่", "เลขที่ใบเสร็จ", "สถานะ", "ลูกค้า", "เบอร์โทร", "เซล", 
          "การชำระเงิน", "จำนวน", "ส่วนลด", "ยอดสุทธิ"
    };
  csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

                foreach (var item in ReportData)
     {
       var row = new[]
           {
            item.ReceiptDateDisplay, item.ReceiptCode, item.ReceiptStatusDisplay,
      item.CustomerName, item.CustomerPhone, item.SalesPersonName, 
         item.PaymentMethodName, item.Quantity.ToString(), item.Discount.ToString("F2"), (item.TotalPrice - item.Discount).ToString("F2")
        };
 csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{field?.Replace("\"", "\"\"")}\"")));
      }
            }
   else if (ReportMode == ReportModes.LimitedCoupons)
            {
    // LimitedCoupons: แสดงรายละเอียดคูปอง
         var headers = new[]
   {
         "วันที่", "เลขที่ใบเสร็จ", "รหัสคูปอง", "คูปอง", "ลูกค้า", "เบอร์โทร",
  "เซล", "วันหมดอายุ", "ราคา"
       };
         csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

  foreach (var item in ReportData)
    {
var row = new[]
        {
             item.ReceiptDateDisplay, item.ReceiptCode, item.GeneratedCode,
item.CouponName, item.CustomerName, item.CustomerPhone,
        item.SalesPersonName, item.ExpiresAtDisplay, item.TotalPrice.ToString("F2")
   };
     csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{field?.Replace("\"", "\"\"")}\"")));
    }
       }
            else if (ReportMode == ReportModes.CancelledCoupons)
  {
        // CancelledCoupons: เหมือน LimitedCoupons แต่เพิ่มคอลัมน์สถานะ (ลบรหัสคูปองออก)
            var headers = new[]
     {
        "วันที่", "เลขที่ใบเสร็จ", "สถานะ", "คูปอง", "ลูกค้า", 
"เบอร์โทร", "เซล", "วันหมดอายุ", "ราคา"
        };
      csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

       foreach (var item in ReportData)
    {
        var row = new[]
    {
         item.ReceiptDateDisplay, item.ReceiptCode, item.ReceiptStatusDisplay,
      item.CouponName, item.CustomerName,
       item.CustomerPhone, item.SalesPersonName, item.ExpiresAtDisplay, 
   item.TotalPrice.ToString("F2")
        };
 csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{field?.Replace("\"", "\"\"")}\"")));
   }
    }
     else if (ReportMode == ReportModes.UnlimitedGrouped)
  {
     // UnlimitedGrouped - แสดงจำนวนจริงที่ซื้อ
    var headers = new[]
      {
 "วันที่", "เลขที่ใบเสร็จ", "คูปอง", "ลูกค้า", "เบอร์โทร",
   "เซล", "วันหมดอายุ", "จำนวน", "รวม"
     };
  csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

       foreach (var item in ReportData)
    {
      var row = new[]
     {
  item.ReceiptDateDisplay, item.ReceiptCode, item.CouponName,
  item.CustomerName, item.CustomerPhone, item.SalesPersonName,
      item.ExpiresAtDisplay, item.Quantity.ToString(), item.TotalPrice.ToString("F2")
    };
    csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{field?.Replace("\"", "\"\"")}\"")));
 }
      }
            else if (ReportMode == ReportModes.SummaryByCoupon)
            {
         // SummaryByCoupon
       var headers = new[] { "คูปอง", "สาขา", "จำกัด/ไม่จำกัด", "จำนวนขายรวม", "ยอดรวม (บาท)" };
      csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

       foreach (var item in ReportData)
       {
        var row = new[]
     {
             item.CouponName, item.BranchTypeName, item.IsLimitedDisplay,
       item.Quantity.ToString(), item.TotalPrice.ToString("F2")
      };
     csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{field?.Replace("\"", "\"\"")}\"")));
    }
         }
            else if (ReportMode == ReportModes.RemainingCoupons)
       {
     // RemainingCoupons
         var headers = new[] { "รหัส", "ชื่อคูปอง", "สาขา", "จำนวนรวม", "ขายแล้ว", "คงเหลือ", "ราคา/ใบ" };
                csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

      foreach (var item in ReportData)
     {
    var row = new[]
  {
       item.CouponCode, item.CouponName, item.BranchTypeName,
  item.TotalQuantity.ToString(), item.SoldQuantity.ToString(),
        item.RemainingQuantity.ToString(), item.UnitPrice.ToString("F2")
          };
   csvContent.AppendLine(string.Join(",", row.Select(field => $"\"{field?.Replace("\"", "\"\"")}\"")));
        }
            }

            // Add summary at the end
            csvContent.AppendLine();
      csvContent.AppendLine($"\"สรุป\"");
     csvContent.AppendLine($"\"จำนวนรายการ: {ReportData.Count:N0}\"");
    csvContent.AppendLine($"\"ยอดรวมสุทธิ: {ReportData.Sum(x => x.TotalPrice - x.Discount):N2} บาท\"");

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
        public string ReceiptStatus { get; set; } = "Active"; // สถานะใบเสร็จ
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
     // Discount amount (per receipt) used in summary calculation
     public decimal Discount { get; set; }
     // Computed net total (TotalPrice minus Discount)
     public decimal NetTotal => TotalPrice - Discount;
 public DateTime? ExpiresAt { get; set; }
    public bool IsLimited { get; set; }

        // สำหรับรายงาน RemainingCoupons
        public int TotalQuantity { get; set; }
    public int SoldQuantity { get; set; }
        public int RemainingQuantity { get; set; }

 public string IsLimitedDisplay => IsLimited ? "จำกัด" : "ไม่จำกัด";
 
        // เพิ่ม property สำหรับแสดงสถานะที่อ่านง่าย
        public string ReceiptStatusDisplay => ReceiptStatus == "Cancelled" ? "ยกเลิก" : "ใช้งาน";
        
        // สีสำหรับแสดงสถานะ
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
     
        // สีสำหรับแสดงจำนวนคงเหลือ
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
