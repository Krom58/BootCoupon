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

namespace BootCoupon
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SalesReportPage : Page
    {
        public SalesReportViewModel ViewModel { get; } = new SalesReportViewModel();

        public SalesReportPage()
        {
            this.InitializeComponent();
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
        private DateTimeOffset? _startDate = DateTime.Today.AddDays(-30);
        private DateTimeOffset? _endDate = DateTime.Today;
        private SalesPerson? _selectedSalesPerson;
        private CouponType? _selectedCouponType;
        private Coupon? _selectedCoupon;
        private PaymentMethod? _selectedPaymentMethod;
        private ObservableCollection<SalesReportItem> _reportData = new();
        private ObservableCollection<Coupon> _filteredCoupons = new();
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

        public Coupon? SelectedCoupon
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
        public ObservableCollection<Coupon> AllCoupons { get; } = new();
        public ObservableCollection<Coupon> FilteredCoupons
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

                // Load Coupons
                var coupons = await context.Coupons.Include(c => c.CouponType).ToListAsync();
                AllCoupons.Clear();
                foreach (var c in coupons)
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

                var newFilteredCoupons = new List<Coupon>();
                
                // เพิ่มตัวเลือก "ทั้งหมด" เป็นรายการแรก
                newFilteredCoupons.Add(new Coupon { Id = 0, Name = "ทั้งหมด", Price = 0, Code = "", CouponTypeId = 0 });
                
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

                var query = context.Receipts
                    .Include(r => r.Items)
                    .Where(r => r.ReceiptDate >= startDateTime && 
                               r.ReceiptDate < endDateTime && 
                               r.Status == "Active")
                    .SelectMany(r => r.Items.Select(item => new
                    {
                        ReceiptDate = r.ReceiptDate,
                        ReceiptCode = r.ReceiptCode,
                        CustomerName = r.CustomerName,
                        SalesPersonId = r.SalesPersonId,
                        PaymentMethodId = r.PaymentMethodId,
                        CouponId = item.CouponId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.TotalPrice
                    }));

        // Apply filters - ตรวจสอบว่าไม่ใช่ "ทั้งหมด" (ID = 0)
        if (SelectedSalesPerson != null && SelectedSalesPerson.ID != 0)
            query = query.Where(x => x.SalesPersonId == SelectedSalesPerson.ID);

        if (SelectedPaymentMethod != null && SelectedPaymentMethod.Id != 0)
            query = query.Where(x => x.PaymentMethodId == SelectedPaymentMethod.Id);

        var rawData = await query.ToListAsync();

        // Join with related data
        var couponIds = rawData.Select(x => x.CouponId).Distinct().ToList();
        var coupons = await context.Coupons
            .Include(c => c.CouponType)
            .Where(c => couponIds.Contains(c.Id))
            .ToListAsync();

        var salesPersonIds = rawData.Where(x => x.SalesPersonId.HasValue)
            .Select(x => x.SalesPersonId!.Value).Distinct().ToList();
        var salesPersons = await context.SalesPerson
            .Where(sp => salesPersonIds.Contains(sp.ID))
            .ToListAsync();

        var paymentMethodIds = rawData.Where(x => x.PaymentMethodId.HasValue)
            .Select(x => x.PaymentMethodId!.Value).Distinct().ToList();
        var paymentMethods = await context.PaymentMethods
            .Where(pm => paymentMethodIds.Contains(pm.Id))
            .ToListAsync();

        var reportItems = rawData.Select(item => {
            var coupon = coupons.FirstOrDefault(c => c.Id == item.CouponId);
            var salesPerson = salesPersons.FirstOrDefault(sp => sp.ID == item.SalesPersonId);
            var paymentMethod = paymentMethods.FirstOrDefault(pm => pm.Id == item.PaymentMethodId);

            return new SalesReportItem
            {
                ReceiptDate = item.ReceiptDate,
                ReceiptCode = item.ReceiptCode,
                CustomerName = item.CustomerName,
                SalesPersonName = salesPerson?.Name ?? "ไม่ระบุ",
                CouponName = coupon?.Name ?? "ไม่พบข้อมูล",
                CouponTypeName = coupon?.CouponType?.Name ?? "ไม่พบข้อมูล",
                PaymentMethodName = paymentMethod?.Name ?? "ไม่ระบุ",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            };
        }).ToList();

        // Apply coupon filters - ตรวจสอบว่าไม่ใช่ "ทั้งหมด" (ID = 0)
        if (SelectedCouponType != null && SelectedCouponType.Id != 0)
            reportItems = reportItems.Where(x => x.CouponTypeName == SelectedCouponType.Name).ToList();

        if (SelectedCoupon != null && SelectedCoupon.Id != 0)
            reportItems = reportItems.Where(x => x.CouponName == SelectedCoupon.Name).ToList();

        ReportData.Clear();
        // !! แก้ไข: เปลี่ยนจาก OrderByDescending เป็น OrderBy เพื่อให้วันที่น้อยสุดมาก่อน !!
        foreach (var item in reportItems.OrderBy(x => x.ReceiptDate))
            ReportData.Add(item);

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
            
            csvContent.AppendLine($"\"จำนวนรายการทั้งหมด: {ReportData.Count:N0} รายการ\"");
            csvContent.AppendLine($"\"ยอดรวมทั้งหมด: {ReportData.Sum(x => x.TotalPrice):N2} บาท\"");
            csvContent.AppendLine(); // Empty line

            // Column headers
            var headers = new[]
            {
                "วันที่", "เลขที่ใบเสร็จ", "ลูกค้า", "เซล", "ชื่อคูปอง",
                "ประเภทคูปอง", "วิธีการชำระเงิน", "จำนวน", "ราคา/หน่วย", "รวม"
            };
            csvContent.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));

            // Data rows
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

        public string ReceiptDateDisplay => ReceiptDate.ToString("dd/MM/yyyy HH:mm");
        public string UnitPriceDisplay => UnitPrice.ToString("N2");
        public string TotalPriceDisplay => TotalPrice.ToString("N2");
    }
}
