using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;
using BootCoupon.Services;
using Microsoft.EntityFrameworkCore; // เพิ่ม using directives สำหรับ Entity Framework Core
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Text;

namespace BootCoupon
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            // ลบการตั้งค่า EPPlus license ออก
        }
        
        private void AddCouponButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(AddCoupon));
        }

        private void CreateReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(Receipt));
        }
        
        // เพิ่ม event handler สำหรับปุ่มสั่งพิมพ์ใหม่
        private void ReprintReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(ReprintReceiptPage));
        }

        // เพิ่ม event handler สำหรับปุ่มรายงานการขาย
        private async void SalesReportButton_Click(object sender, RoutedEventArgs e)
        {
            // สร้าง dialog เลือกประเภทรายงาน
            var reportPanel = new StackPanel { Spacing = 15 };

            reportPanel.Children.Add(new TextBlock 
            { 
                Text = "เลือกประเภทรายงานการขาย", 
                FontSize = 16, 
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold 
            });

            var reportTypeRadio1 = new RadioButton 
            { 
                Content = "รายงานตามวันที่และประเภทคูปอง", 
                GroupName = "ReportType",
                IsChecked = true 
            };
            reportPanel.Children.Add(reportTypeRadio1);

            var reportTypeRadio2 = new RadioButton 
            { 
                Content = "รายงานตามเซลที่ขาย", 
                GroupName = "ReportType" 
            };
            reportPanel.Children.Add(reportTypeRadio2);

            var dialog = new ContentDialog
            {
                Title = "รายงานการขาย",
                Content = reportPanel,
                PrimaryButtonText = "ต่อไป",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (reportTypeRadio1.IsChecked == true)
                {
                    await ShowDateAndCouponTypeReportDialog();
                }
                else if (reportTypeRadio2.IsChecked == true)
                {
                    await ShowSalesPersonReportDialog();
                }
            }
        }

        // รายงานตามวันที่และประเภทคูปอง
        private async Task ShowDateAndCouponTypeReportDialog()
        {
            var reportPanel = new StackPanel { Spacing = 15 };

            reportPanel.Children.Add(new TextBlock 
            { 
                Text = "รายงานการขายตามวันที่และประเภทคูปอง", 
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold 
            });

            // วันที่เริ่มต้น
            reportPanel.Children.Add(new TextBlock { Text = "วันที่เริ่มต้น:" });
            var startDatePicker = new DatePicker { SelectedDate = DateTime.Today.AddDays(-30) };
            reportPanel.Children.Add(startDatePicker);

            // วันที่สิ้นสุด
            reportPanel.Children.Add(new TextBlock { Text = "วันที่สิ้นสุด:" });
            var endDatePicker = new DatePicker { SelectedDate = DateTime.Today };
            reportPanel.Children.Add(endDatePicker);

            // ประเภทคูปอง
            reportPanel.Children.Add(new TextBlock { Text = "ประเภทคูปอง (เว้นว่างสำหรับทุกประเภท):" });
            var couponTypeCombo = new ComboBox 
            { 
                PlaceholderText = "ทุกประเภท",
                DisplayMemberPath = "Name"
            };

            try
            {
                using var context = new CouponContext();
                var couponTypes = await context.CouponTypes.ToListAsync();
                couponTypeCombo.ItemsSource = couponTypes;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"ไม่สามารถโหลดประเภทคูปองได้: {ex.Message}");
                return;
            }

            reportPanel.Children.Add(couponTypeCombo);

            var dialog = new ContentDialog
            {
                Title = "กำหนดช่วงเวลาและประเภทคูปอง",
                Content = reportPanel,
                PrimaryButtonText = "สร้างรายงาน CSV",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var startDate = startDatePicker.SelectedDate?.DateTime ?? DateTime.Today.AddDays(-30);
                var endDate = endDatePicker.SelectedDate?.DateTime.AddDays(1) ?? DateTime.Today.AddDays(1);
                var selectedCouponType = couponTypeCombo.SelectedItem as CouponType;

                await GenerateDateAndCouponTypeReport(startDate, endDate, selectedCouponType);
            }
        }

        // รายงานตามเซลที่ขาย
        private async Task ShowSalesPersonReportDialog()
        {
            var reportPanel = new StackPanel { Spacing = 15 };

            reportPanel.Children.Add(new TextBlock 
            { 
                Text = "รายงานการขายตามเซล", 
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold 
            });

            // วันที่เริ่มต้น
            reportPanel.Children.Add(new TextBlock { Text = "วันที่เริ่มต้น:" });
            var startDatePicker = new DatePicker { SelectedDate = DateTime.Today.AddDays(-30) };
            reportPanel.Children.Add(startDatePicker);

            // วันที่สิ้นสุด
            reportPanel.Children.Add(new TextBlock { Text = "วันที่สิ้นสุด:" });
            var endDatePicker = new DatePicker { SelectedDate = DateTime.Today };
            reportPanel.Children.Add(endDatePicker);

            // เซล
            reportPanel.Children.Add(new TextBlock { Text = "เซล (เว้นว่างสำหรับทุกคน):" });
            var salesPersonCombo = new ComboBox 
            { 
                PlaceholderText = "ทุกคน",
                DisplayMemberPath = "Name"
            };

            try
            {
                using var context = new CouponContext();
                var salesPersons = await context.SalesPerson.ToListAsync();
                salesPersonCombo.ItemsSource = salesPersons;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"ไม่สามารถโหลดข้อมูลเซลได้: {ex.Message}");
                return;
            }

            reportPanel.Children.Add(salesPersonCombo);

            var dialog = new ContentDialog
            {
                Title = "กำหนดช่วงเวลาและเซล",
                Content = reportPanel,
                PrimaryButtonText = "สร้างรายงาน CSV",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var startDate = startDatePicker.SelectedDate?.DateTime ?? DateTime.Today.AddDays(-30);
                var endDate = endDatePicker.SelectedDate?.DateTime.AddDays(1) ?? DateTime.Today.AddDays(1);
                var selectedSalesPerson = salesPersonCombo.SelectedItem as SalesPerson;

                await GenerateSalesPersonReport(startDate, endDate, selectedSalesPerson);
            }
        }

        // สร้างรายงานตามวันที่และประเภทคูปอง
        private async Task GenerateDateAndCouponTypeReport(DateTime startDate, DateTime endDate, CouponType? couponType)
        {
            try
            {
                using var context = new CouponContext();
                
                var query = context.Receipts
                    .Include(r => r.Items)
                    .Where(r => r.ReceiptDate >= startDate && r.ReceiptDate < endDate && r.Status == "Active")
                    .SelectMany(r => r.Items.Select(item => new
                    {
                        ReceiptDate = r.ReceiptDate,
                        ReceiptCode = r.ReceiptCode,
                        CustomerName = r.CustomerName,
                        CouponId = item.CouponId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        TotalPrice = item.TotalPrice
                    }));

                var receiptItems = await query.ToListAsync();

                // เชื่อมข้อมูลคูปอง
                var couponIds = receiptItems.Select(x => x.CouponId).Distinct().ToList();
                var coupons = await context.Coupons
                    .Include(c => c.CouponType)
                    .Where(c => couponIds.Contains(c.Id))
                    .ToListAsync();

                var reportData = receiptItems.Select(item => {
                    var coupon = coupons.FirstOrDefault(c => c.Id == item.CouponId);
                    return new
                    {
                        ReceiptDate = item.ReceiptDate.ToString("dd/MM/yyyy HH:mm:ss"),
                        ReceiptCode = item.ReceiptCode,
                        CustomerName = item.CustomerName,
                        CouponName = coupon?.Name ?? "ไม่พบข้อมูล",
                        CouponCode = coupon?.Code ?? "ไม่พบข้อมูล",
                        CouponType = coupon?.CouponType?.Name ?? "ไม่พบข้อมูล",
                        Quantity = item.Quantity.ToString(),
                        UnitPrice = item.UnitPrice.ToString("F2"),
                        TotalPrice = item.TotalPrice.ToString("F2")
                    };
                }).ToList();

                // กรองตามประเภทคูปองถ้าเลือก
                if (couponType != null)
                {
                    reportData = reportData.Where(x => x.CouponType == couponType.Name).ToList();
                }

                var headers = new[]
                {
                    "วันที่", "เลขที่ใบเสร็จ", "ลูกค้า", "ชื่อคูปอง", 
                    "รหัสคูปอง", "ประเภทคูปอง", "จำนวน", "ราคา/หน่วย", "รวม"
                };

                var csvData = new List<string[]> { headers };
                csvData.AddRange(reportData.Select(item => new[]
                {
                    item.ReceiptDate, item.ReceiptCode, item.CustomerName, item.CouponName,
                    item.CouponCode, item.CouponType, item.Quantity, item.UnitPrice, item.TotalPrice
                }));

                await CreateCsvFile($"รายงานการขายตามวันที่และประเภทคูปอง_{DateTime.Now:yyyyMMdd_HHmmss}.csv", csvData);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"เกิดข้อผิดพลาดในการสร้างรายงาน: {ex.Message}");
            }
        }

        // สร้างรายงานตามเซล
        private async Task GenerateSalesPersonReport(DateTime startDate, DateTime endDate, SalesPerson? salesPerson)
        {
            try
            {
                using var context = new CouponContext();
                
                var query = context.Receipts
                    .Include(r => r.Items)
                    .Where(r => r.ReceiptDate >= startDate && r.ReceiptDate < endDate && r.Status == "Active");

                if (salesPerson != null)
                {
                    query = query.Where(r => r.SalesPersonId == salesPerson.ID);
                }

                var receipts = await query.ToListAsync();

                // เชื่อมข้อมูลเซลและคูปอง
                var salesPersonIds = receipts.Where(r => r.SalesPersonId.HasValue)
                    .Select(r => r.SalesPersonId!.Value).Distinct().ToList();
                var salesPersons = await context.SalesPerson
                    .Where(sp => salesPersonIds.Contains(sp.ID))
                    .ToListAsync();

                var couponIds = receipts.SelectMany(r => r.Items.Select(i => i.CouponId)).Distinct().ToList();
                var coupons = await context.Coupons
                    .Include(c => c.CouponType)
                    .Where(c => couponIds.Contains(c.Id))
                    .ToListAsync();

                var reportData = receipts.SelectMany(receipt => {
                    var salesPersonName = salesPersons.FirstOrDefault(sp => sp.ID == receipt.SalesPersonId)?.Name ?? "ไม่ระบุ";
                    return receipt.Items.Select(item => {
                        var coupon = coupons.FirstOrDefault(c => c.Id == item.CouponId);
                        return new
                        {
                            ReceiptDate = receipt.ReceiptDate.ToString("dd/MM/yyyy HH:mm:ss"),
                            ReceiptCode = receipt.ReceiptCode,
                            CustomerName = receipt.CustomerName,
                            SalesPersonName = salesPersonName,
                            CouponName = coupon?.Name ?? "ไม่พบข้อมูล",
                            CouponCode = coupon?.Code ?? "ไม่พบข้อมูล",
                            CouponType = coupon?.CouponType?.Name ?? "ไม่พบข้อมูล",
                            Quantity = item.Quantity.ToString(),
                            UnitPrice = item.UnitPrice.ToString("F2"),
                            TotalPrice = item.TotalPrice.ToString("F2")
                        };
                    });
                }).ToList();

                var headers = new[]
                {
                    "วันที่", "เลขที่ใบเสร็จ", "ลูกค้า", "เซล", "ชื่อคูปอง", 
                    "รหัสคูปอง", "ประเภทคูปอง", "จำนวน", "ราคา/หน่วย", "รวม"
                };

                var csvData = new List<string[]> { headers };
                csvData.AddRange(reportData.Select(item => new[]
                {
                    item.ReceiptDate, item.ReceiptCode, item.CustomerName, item.SalesPersonName, item.CouponName,
                    item.CouponCode, item.CouponType, item.Quantity, item.UnitPrice, item.TotalPrice
                }));

                await CreateCsvFile($"รายงานการขายตามเซล_{DateTime.Now:yyyyMMdd_HHmmss}.csv", csvData);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"เกิดข้อผิดพลาดในการสร้างรายงาน: {ex.Message}");
            }
        }

        // สร้างไฟล์ CSV
        private async Task CreateCsvFile(string fileName, List<string[]> data)
        {
            try
            {
                // เลือกที่จัดเก็บไฟล์
                var savePicker = new FileSavePicker();
                savePicker.SuggestedFileName = fileName;
                savePicker.FileTypeChoices.Add("CSV Files", new List<string> { ".csv" });
                
                // Initialize the file picker with the window handle
                var hwnd = WindowNative.GetWindowHandle(MainWindow.Instance);
                InitializeWithWindow.Initialize(savePicker, hwnd);

                var file = await savePicker.PickSaveFileAsync();
                if (file == null) return;

                // สร้างเนื้อหา CSV
                var csvContent = new StringBuilder();
                
                foreach (var row in data)
                {
                    // เพิ่ม quote รอบแต่ละฟิลด์และ escape quote ภายใน
                    var escapedRow = row.Select(field => 
                    {
                        if (field == null) return "\"\"";
                        
                        // Escape quotes ในข้อมูล
                        var escaped = field.Replace("\"", "\"\"");
                        return $"\"{escaped}\"";
                    });
                    
                    csvContent.AppendLine(string.Join(",", escapedRow));
                }

                // บันทึกไฟล์ด้วย UTF-8 encoding พร้อม BOM เพื่อให้ Excel เปิดภาษาไทยได้ถูกต้อง
                var utf8WithBom = new UTF8Encoding(true);
                await FileIO.WriteTextAsync(file, csvContent.ToString(), Windows.Storage.Streams.UnicodeEncoding.Utf8);

                await ShowSuccessDialog($"สร้างรายงาน CSV เรียบร้อยแล้ว\n\nไฟล์: {file.Name}\nจำนวนรายการ: {data.Count - 1} รายการ\n\nสามารถเปิดไฟล์ด้วย Excel หรือโปรแกรมตารางคำนวณอื่นๆ ได้");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"เกิดข้อผิดพลาดในการสร้างไฟล์ CSV: {ex.Message}");
            }
        }
        
        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var context = new CouponContext())
                {
                    // ทดสอบการเชื่อมต่อก่อน
                    await context.Database.EnsureCreatedAsync();
                    
                    var numberManager = await context.ReceiptNumberManagers.FirstOrDefaultAsync();
                    var canceledNumbers = await context.CanceledReceiptNumbers
                        .OrderBy(c => c.CanceledDate)
                        .Select(c => c.ReceiptCode)
                        .ToListAsync();

                    if (numberManager == null)
                    {
                        numberManager = new ReceiptNumberManager
                        {
                            Prefix = "INV",
                            CurrentNumber = 5001,
                            UpdatedBy = Environment.MachineName
                        };
                        context.ReceiptNumberManagers.Add(numberManager);
                        await context.SaveChangesAsync();
                    }

                    // สร้างหน้าต่างตั้งค่าที่สามารถแก้ไขได้
                    var settingsPanel = new StackPanel { Spacing = 10 };

                    // หัวข้อ
                    settingsPanel.Children.Add(new TextBlock 
                    { 
                        Text = "การตั้งค่าหมายเลขใบเสร็จ", 
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 10)
                    });

                    // คำนำหน้า
                    settingsPanel.Children.Add(new TextBlock { Text = "คำนำหน้ารหัสใบเสร็จ:" });
                    var prefixTextBox = new TextBox 
                    { 
                        Text = numberManager.Prefix,
                        PlaceholderText = "เช่น INV, RC, หรือ ใบเสร็จ",
                        Width = 200,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        MaxLength = 10
                    };
                    settingsPanel.Children.Add(prefixTextBox);

                    // หมายเลขถัดไป
                    settingsPanel.Children.Add(new TextBlock 
                    { 
                        Text = "หมายเลขถัดไป:",
                        Margin = new Thickness(0, 10, 0, 0)
                    });
                    var numberTextBox = new TextBox 
                    { 
                        Text = numberManager.CurrentNumber.ToString(),
                        PlaceholderText = "เช่น 5001",
                        Width = 200,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    settingsPanel.Children.Add(numberTextBox);

                    // ตัวอย่างรหัสใบเสร็จ
                    var previewTextBlock = new TextBlock 
                    { 
                        Text = $"ตัวอย่าง: {numberManager.Prefix}{numberManager.CurrentNumber}",
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        Margin = new Thickness(0, 5, 0, 10),
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                    };
                    settingsPanel.Children.Add(previewTextBlock);

                    // อัปเดตตัวอย่างเมื่อมีการเปลี่ยนแปลง
                    prefixTextBox.TextChanged += (s, args) => {
                        var prefix = prefixTextBox.Text;
                        var number = int.TryParse(numberTextBox.Text, out int num) ? num : numberManager.CurrentNumber;
                        previewTextBlock.Text = $"ตัวอย่าง: {prefix}{number}";
                    };

                    numberTextBox.TextChanged += (s, args) => {
                        var prefix = prefixTextBox.Text;
                        var number = int.TryParse(numberTextBox.Text, out int num) ? num : numberManager.CurrentNumber;
                        previewTextBlock.Text = $"ตัวอย่าง: {prefix}{number}";
                    };

                    // ข้อมูลปัจจุบัน
                    var separator = new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    settingsPanel.Children.Add(separator);

                    settingsPanel.Children.Add(new TextBlock 
                    { 
                        Text = "ข้อมูลปัจจุบัน:",
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    });
                    settingsPanel.Children.Add(new TextBlock { Text = $"อัปเดตล่าสุด: {numberManager.LastUpdated:dd/MM/yyyy HH:mm:ss}" });
                    settingsPanel.Children.Add(new TextBlock { Text = $"อัปเดตโดย: {numberManager.UpdatedBy ?? "ไม่ระบุ"}" });

                    if (canceledNumbers.Count > 0)
                    {
                        settingsPanel.Children.Add(new TextBlock 
                        { 
                            Text = "หมายเลขที่รอใช้ใหม่:", 
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Margin = new Thickness(0, 10, 0, 5)
                        });
                        
                        var recycleNumbersText = new TextBlock 
                        { 
                            Text = string.Join(", ", canceledNumbers),
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkOrange)
                        };
                        settingsPanel.Children.Add(recycleNumbersText);
                    }

                    settingsPanel.Children.Add(new TextBlock 
                    { 
                        Text = $"เครื่องปัจจุบัน: {Environment.MachineName}",
                        Margin = new Thickness(0, 10, 0, 0),
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                    });

                    // แสดง dialog พร้อมปุ่มบันทึก
                    var dialog = new ContentDialog
                    {
                        Title = "ตั้งค่าระบบ",
                        Content = settingsPanel,
                        PrimaryButtonText = "บันทึก",
                        CloseButtonText = "ยกเลิก",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await dialog.ShowAsync();

                    // หากกดบันทึก
                    if (result == ContentDialogResult.Primary)
                    {
                        // ตรวจสอบข้อมูลที่กรอก
                        string newPrefix = prefixTextBox.Text.Trim();
                        string numberText = numberTextBox.Text.Trim();

                        // ตรวจสอบคำนำหน้า
                        if (string.IsNullOrWhiteSpace(newPrefix))
                        {
                            await ShowErrorDialog("กรุณากรอกคำนำหน้ารหัสใบเสร็จ");
                            return;
                        }

                        // ตรวจสอบหมายเลข
                        if (!int.TryParse(numberText, out int newNumber) || newNumber < 1)
                        {
                            await ShowErrorDialog("กรุณากรอกหมายเลขที่ถูกต้อง (ต้องเป็นตัวเลขมากกว่า 0)");
                            return;
                        }

                        try
                        {
                            // อัปเดตข้อมูลในฐานข้อมูล
                            numberManager.Prefix = newPrefix;
                            numberManager.CurrentNumber = newNumber;
                            numberManager.LastUpdated = DateTime.Now;
                            numberManager.UpdatedBy = Environment.MachineName;

                            context.ReceiptNumberManagers.Update(numberManager);
                            await context.SaveChangesAsync();

                            await ShowSuccessDialog($"บันทึกการตั้งค่าเรียบร้อยแล้ว\n\nรหัสใบเสร็จถัดไป: {newPrefix}{newNumber}");
                        }
                        catch (Exception saveEx)
                        {
                            await ShowErrorDialog($"ไม่สามารถบันทึกการตั้งค่าได้: {saveEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"ไม่สามารถโหลดข้อมูลการตั้งค่าได้: {ex.Message}\n\nรายละเอียด: {ex.InnerException?.Message}");
            }
        }
        
        // Use System.Threading.Tasks.Task explicitly to avoid any ambiguity
        private async System.Threading.Tasks.Task ShowMessageDialog(string message, string title)
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
        
        private async System.Threading.Tasks.Task ShowErrorDialog(string message)
        {
            await ShowMessageDialog(message, "แจ้งเตือน");
        }
        
        private async System.Threading.Tasks.Task ShowSuccessDialog(string message)
        {
            await ShowMessageDialog(message, "สำเร็จ");
        }
    }
}
