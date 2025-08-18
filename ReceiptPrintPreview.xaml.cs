using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
using Windows.Graphics.Printing.OptionDetails;
using System.Diagnostics;
using Windows.ApplicationModel.Core;
using Microsoft.UI.Xaml.Printing;
using WinRT.Interop;
using Microsoft.UI.Xaml.Media; // เพิ่มบรรทัดนี้สำหรับ SolidColorBrush
using Microsoft.UI.Xaml.Media.Imaging;

namespace BootCoupon
{
    public class ReceiptItemDisplay
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        // Add formatted properties
        public string UnitPriceFormatted => UnitPrice.ToString("N2");
        public string TotalPriceFormatted => TotalPrice.ToString("N2");
    }

    public class Settings
    {
        public int ID { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public sealed partial class ReceiptPrintPreview : Page
    {
        // Mark as nullable since it's initialized in OnNavigatedTo or LoadReceiptAsync
        private ReceiptModel? _receipt;
        private readonly ObservableCollection<ReceiptItemDisplay> _displayItems = new ObservableCollection<ReceiptItemDisplay>();

        // เปลี่ยนการจัดการ printing
        private PrintManager? printManager;
        private PrintDocument? printDocument;
        private IPrintDocumentSource? printDocumentSource;
        private bool hasPrinted = false;

        // เพิ่มตัวแปรเพื่อเก็บ Print Canvas
        private Canvas? printCanvas;

        public ReceiptPrintPreview()
        {
            this.InitializeComponent();
            ItemsListView.ItemsSource = _displayItems;

            // เพิ่ม event handler สำหรับการ unload page
            this.Unloaded += ReceiptPrintPreview_Unloaded;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // รับข้อมูล receipt จากหน้าที่เรียกมา
            if (e.Parameter is int receiptId)
            {
                await LoadReceiptDataAsync(receiptId);
            }
            else if (e.Parameter is ReceiptModel receipt)
            {
                _receipt = receipt;
                await LoadReceiptItemsAsync(_receipt.ReceiptID);
            }
        }

        private async Task LoadReceiptDataAsync(int receiptId)
        {
            using (var context = new CouponContext())
            {
                // ดึงข้อมูลใบเสร็าจากฐานข้อมูล
                _receipt = await context.Receipts.FirstOrDefaultAsync(r => r.ReceiptID == receiptId);

                if (_receipt != null)
                {
                    await LoadReceiptItemsAsync(receiptId);
                }
            }
        }

        private async Task LoadReceiptItemsAsync(int receiptId)
        {
            if (_receipt == null) return;

            // แสดงข้อมูลลูกค้าและรายละเอียดใบเสร็จ
            CustomerNameTextBlock.Text = _receipt.CustomerName;
            CustomerPhoneTextBlock.Text = _receipt.CustomerPhoneNumber;
            ReceiptNumberTextBlock.Text = _receipt.ReceiptCode;

            // แสดงวันที่ในรูปแบบไทย (พ.ศ.)
            var thaiCulture = new CultureInfo("th-TH");
            thaiCulture.DateTimeFormat.Calendar = new ThaiBuddhistCalendar();
            ReceiptDateTextBlock.Text = _receipt.ReceiptDate.ToString("dd / MM / yyyy", thaiCulture);

            // แสดงยอดรวมทั้งหมด
            TotalAmountTextBlock.Text = _receipt.TotalAmount.ToString("N2");

            // ดึงข้อมูลรายการสินค้า
            using (var context = new CouponContext())
            {
                // ดึงข้อมูล receipt items พร้อมกับข้อมูล coupon
                var items = await context.ReceiptItems
                    .Where(ri => ri.ReceiptId == receiptId)
                    .ToListAsync();

                _displayItems.Clear();

                // สร้างข้อมูลสำหรับแสดงในตาราง
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var coupon = await context.Coupons.FindAsync(item.CouponId);

                    if (coupon != null)
                    {
                        _displayItems.Add(new ReceiptItemDisplay
                        {
                            Index = i + 1,
                            Name = coupon.Name,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TotalPrice = item.TotalPrice
                        });
                    }
                }
            }
        }

        // เมื่อกดปุ่มพิมพ์
        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            await InitiatePrintProcess();
        }

        // ฟังก์ชันเริ่มต้นการพิมพ์ - แก้ไขใหม่
        private async Task InitiatePrintProcess()
        {
            try
            {
                Debug.WriteLine("เริ่มต้นกระบวนการพิมพ์");

                // รอสักครู่ก่อนเริ่มพิมพ์
                await Task.Delay(200);

                // ทำความสะอาด resources เก่าก่อน
                CleanupPrintResources();

                // สร้าง PrintDocument ใหม่
                printDocument = new PrintDocument();
                printDocumentSource = printDocument.DocumentSource;

                // ผูก events
                printDocument.Paginate += PrintDocument_Paginate;
                printDocument.GetPreviewPage += PrintDocument_GetPreviewPage;
                printDocument.AddPages += PrintDocument_AddPages;

                // วิธีใหม่สำหรับ WinUI 3 - ใช้ MainWindow instance
                if (App.MainWindowInstance != null)
                {
                    var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                    printManager = PrintManagerInterop.GetForWindow(windowHandle);

                    printManager.PrintTaskRequested -= PrintManager_PrintTaskRequested;
                    printManager.PrintTaskRequested += PrintManager_PrintTaskRequested;

                    // แสดง Print UI โดยระบุ window handle - ไปที่ printer dialog โดยตรง
                    await PrintManagerInterop.ShowPrintUIForWindowAsync(windowHandle);

                    Debug.WriteLine("แสดง Print UI เรียบร้อย");
                }
                else
                {
                    throw new Exception("ไม่สามารถหา Main Window ได้");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการพิมพ์: {ex.Message}");

                // จัดการกับข้อผิดพลาด
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "ข้อผิดพลาดในการพิมพ์",
                    Content = $"ไม่สามารถเข้าถึงเครื่องพิมพ์ได้: {ex.Message}\n\nกรุณาตรวจสอบ:\n- เครื่องพิมพ์ต่ออยู่หรือไม่\n- Driver เครื่องพิมพ์ติดตั้งแล้วหรือไม่\n- Windows Print Service ทำงานหรือไม่",
                    CloseButtonText = "ตกลง",
                    XamlRoot = this.XamlRoot
                };

                await errorDialog.ShowAsync();
            }
        }

        private void PrintManager_PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            try
            {
                Debug.WriteLine("PrintTaskRequested เริ่มทำงาน");

                var printTask = args.Request.CreatePrintTask("ใบเสร็จรับเงิน", sourceRequestedArgs =>
                {
                    Debug.WriteLine("กำลังตั้งค่า PrintDocumentSource");
                    if (printDocumentSource != null)
                    {
                        sourceRequestedArgs.SetSource(printDocumentSource);
                    }
                });

                if (printTask != null)
                {
                    printTask.Completed += PrintTask_Completed;

                    // ตั้งค่าเพิ่มเติมสำหรับการพิมพ์ A5
                    printTask.Options.MediaSize = PrintMediaSize.IsoA5; // เปลี่ยนจาก IsoA4 เป็น IsoA5
                    printTask.Options.Orientation = PrintOrientation.Portrait;

                    Debug.WriteLine("ตั้งค่า PrintTask สำหรับ A5 เรียบร้อย");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดใน PrintTaskRequested: {ex.Message}");
            }
        }

        private void PrintTask_Completed(PrintTask sender, PrintTaskCompletedEventArgs args)
        {
            Debug.WriteLine($"Print task completed with status: {args.Completion}");

            // ตรวจสอบผลลัพธ์ของการพิมพ์
            if (args.Completion == PrintTaskCompletion.Submitted)
            {
                // พิมพ์สำเร็จ
                hasPrinted = true;
                Debug.WriteLine("การพิมพ์เสร็จสมบูรณ์");

                // แจ้งให้ผู้ใช้ทราบ (บน UI thread)
                DispatcherQueue.TryEnqueue(() =>
                {
                    ShowPrintSuccessMessage();
                });
            }
            else if (args.Completion == PrintTaskCompletion.Failed)
            {
                Debug.WriteLine("การพิมพ์ล้มเหลว");

                DispatcherQueue.TryEnqueue(async () =>
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "การพิมพ์ล้มเหลว",
                        Content = "ไม่สามารถพิมพ์ใบเสร็จได้ กรุณาลองใหม่อีกครั้ง",
                        CloseButtonText = "ตกลง",
                        XamlRoot = this.XamlRoot
                    };

                    await errorDialog.ShowAsync();
                });
            }
            else if (args.Completion == PrintTaskCompletion.Canceled)
            {
                Debug.WriteLine("การพิมพ์ถูกยกเลิก");
            }

            // ทำความสะอาด
            CleanupPrintResources();
        }

        private async void ShowPrintSuccessMessage()
        {
            try
            {
                ContentDialog successDialog = new ContentDialog
                {
                    Title = "พิมพ์สำเร็จ",
                    Content = "ใบเสร็จถูกส่งไปยังเครื่องพิมพ์เรียบร้อยแล้ว",
                    CloseButtonText = "ตกลง",
                    XamlRoot = this.XamlRoot
                };

                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ไม่สามารถแสดงข้อความสำเร็จ: {ex.Message}");
            }
        }

        private void CleanupPrintResources()
        {
            try
            {
                if (printManager != null)
                {
                    printManager.PrintTaskRequested -= PrintManager_PrintTaskRequested;
                }

                if (printDocument != null)
                {
                    printDocument.Paginate -= PrintDocument_Paginate;
                    printDocument.GetPreviewPage -= PrintDocument_GetPreviewPage;
                    printDocument.AddPages -= PrintDocument_AddPages;
                    printDocument = null;
                }

                printDocumentSource = null;
                printManager = null;
                printCanvas = null; // เพิ่มการล้าง printCanvas

                Debug.WriteLine("ทำความสะอาด Print resources เรียบร้อย");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการทำความสะอาด: {ex.Message}");
            }
        }

        // ในกรณีที่หน้าจอถูกปิดโดยที่ยังไม่มีการพิมพ์
        private async void ReceiptPrintPreview_Unloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"ReceiptPrintPreview_Unloaded: hasPrinted = {hasPrinted}");

            CleanupPrintResources();

            if (!hasPrinted && _receipt != null)
            {
                Debug.WriteLine($"จะลบข้อมูลใบเสร็จ {_receipt.ReceiptCode} เนื่องจากยังไม่ได้พิมพ์");
                // ลบข้อมูลที่ดึงมาเนื่องจากไม่มีการพิมพ์
                await CleanupReceiptData();
            }
        }

        // เมื่อกดปุ่มปิด
        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // ถ้ายังไม่ได้พิมพ์ให้แสดงคำเตือน
            if (!hasPrinted && _receipt != null)
            {
                ContentDialog warningDialog = new ContentDialog
                {
                    Title = "ยังไม่ได้พิมพ์",
                    Content = $"คุณยังไม่ได้พิมพ์ใบเสร็จ {_receipt.ReceiptCode} ต้องการปิดหรือไม่?",
                    PrimaryButtonText = "พิมพ์",
                    CloseButtonText = "ปิด",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result = await warningDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // ถ้าเลือกพิมพ์ให้เรียกฟังก์ชันพิมพ์
                    await InitiatePrintProcess();
                    return;
                }

                // ถ้าเลือกปิด ให้ลบข้อมูลที่ดึงมา
                await CleanupReceiptData();
            }

            // ทำความสะอาดก่อนปิด
            CleanupPrintResources();

            // ปิดหน้าใบเสร็จ
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        // ลบข้อมูลใบเสร็จเมื่อไม่มีการพิมพ์
        private async Task CleanupReceiptData()
        {
            try
            {
                if (_receipt == null || string.IsNullOrEmpty(_receipt.ReceiptCode)) return;

                string receiptCodeToDelete = _receipt.ReceiptCode;
                int receiptIdToDelete = _receipt.ReceiptID;

                Debug.WriteLine($"เริ่มลบใบเสร็จ {receiptCodeToDelete} (ID: {receiptIdToDelete})");

                // ลบข้อมูลใบเสร็จจากฐานข้อมูลก่อน
                using (var context = new CouponContext())
                {
                    // ลบ receipt items ก่อน
                    var receiptItems = await context.ReceiptItems
                        .Where(ri => ri.ReceiptId == receiptIdToDelete)
                        .ToListAsync();

                    if (receiptItems.Any())
                    {
                        context.ReceiptItems.RemoveRange(receiptItems);
                        await context.SaveChangesAsync();
                        Debug.WriteLine($"ลบ ReceiptItems จำนวน {receiptItems.Count} รายการแล้ว");
                    }

                    // ลบ receipt
                    var receipt = await context.Receipts.FindAsync(receiptIdToDelete);
                    if (receipt != null)
                    {
                        context.Receipts.Remove(receipt);
                        await context.SaveChangesAsync();
                        Debug.WriteLine($"ลบ Receipt {receiptCodeToDelete} จากฐานข้อมูลแล้ว");
                    }
                }

                // หลังจากลบจากฐานข้อมูลสำเร็จแล้ว ค่อยเก็บหมายเลขเพื่อนำมาใช้ใหม่
                var settings = await AppSettings.GetSettingsAsync();
                settings.RecycleReceiptCode(receiptCodeToDelete);
                await AppSettings.SaveSettingsAsync(settings);
                Debug.WriteLine($"เก็บหมายเลข {receiptCodeToDelete} เพื่อนำกลับมาใช้ใหม่");

                _receipt = null;
                Debug.WriteLine($"ลบข้อมูลใบเสร็จ {receiptCodeToDelete} เรียบร้อยแล้ว");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up receipt data: {ex.Message}");
                // ถ้าเกิดข้อผิดพลาด แสดงข้อความแจ้งเตือน
                try
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "ข้อผิดพลาด",
                        Content = $"ไม่สามารถลบข้อมูลใบเสร็จได้: {ex.Message}",
                        CloseButtonText = "ตกลง",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
                catch
                {
                    // ถ้าแสดง dialog ไม่ได้ ให้เพิกเฉย
                }
            }
        }

        private void PrintDocument_Paginate(object sender, PaginateEventArgs e)
        {
            try
            {
                Debug.WriteLine("PrintDocument_Paginate เริ่มทำงาน");

                // สร้าง print canvas หรือใช้หน้าปัจจุบัน
                if (printCanvas == null)
                {
                    CreatePrintCanvas();
                }

                // กำหนดจำนวนหน้าสำหรับการพิมพ์
                if (printDocument != null)
                {
                    printDocument.SetPreviewPageCount(1, PreviewPageCountType.Final);
                    Debug.WriteLine("กำหนดจำนวนหน้าเป็น 1 หน้า");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดใน Paginate: {ex.Message}");
            }
        }

        private void PrintDocument_GetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            try
            {
                Debug.WriteLine($"PrintDocument_GetPreviewPage หน้า {e.PageNumber}");

                // สร้าง print content ถ้ายังไม่มี
                if (printCanvas == null)
                {
                    CreatePrintCanvas();
                }

                // ให้ print canvas เป็น preview page
                if (printDocument != null && printCanvas != null)
                {
                    printDocument.SetPreviewPage(e.PageNumber, printCanvas);
                    Debug.WriteLine($"กำหนด preview page {e.PageNumber} ด้วย printCanvas");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดใน GetPreviewPage: {ex.Message}");
            }
        }

        private void PrintDocument_AddPages(object sender, AddPagesEventArgs e)
        {
            try
            {
                Debug.WriteLine("PrintDocument_AddPages เริ่มทำงาน");

                // สร้าง print content ถ้ายังไม่มี
                if (printCanvas == null)
                {
                    CreatePrintCanvas();
                }

                // เพิ่มหน้าสำหรับการพิมพ์
                if (printDocument != null && printCanvas != null)
                {
                    printDocument.AddPage(printCanvas);
                    printDocument.AddPagesComplete();
                    Debug.WriteLine("เพิ่มหน้าสำหรับพิมพ์เรียบร้อย");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดใน AddPages: {ex.Message}");
            }
        }

        // แก้ไข CreatePrintCanvas method
        private void CreatePrintCanvas()
        {
            try
            {
                Debug.WriteLine("สร้าง Print Canvas สำหรับ A5");

                // สร้าง Canvas สำหรับพิมพ์ A5 (559 x 794 pixels at 96 DPI)
                printCanvas = new Canvas
                {
                    Width = 559,   // A5 width: 148mm = 559 pixels at 96 DPI
                    Height = 794,  // A5 height: 210mm = 794 pixels at 96 DPI
                    Background = new SolidColorBrush(Microsoft.UI.Colors.White)
                };

                // สร้าง receipt content โดยตรงไม่ต้อง ScrollViewer
                var receiptContent = CreateReceiptContent();

                if (receiptContent != null)
                {
                    printCanvas.Children.Add(receiptContent);
                }

                Debug.WriteLine("สร้าง Print Canvas A5 เรียบร้อย");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Print Canvas: {ex.Message}");
            }
        }

        // แก้ไข CreateReceiptContent method
        private FrameworkElement? CreateReceiptContent()
        {
            try
            {
                if (_receipt == null) return null;

                // สร้าง Grid หลักขนาด A5
                var mainGrid = new Grid
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                    Width = 559,   // A5 width
                    Height = 794   // A5 height
                };

                // สร้าง StackPanel สำหรับเนื้อหา (ตรงกับ XAML)
                var mainStackPanel = new StackPanel
                {
                    Padding = new Thickness(25)
                };

                // เพิ่มหัวกระดาษ
                var headerGrid = CreateHeaderSection();
                if (headerGrid != null)
                    mainStackPanel.Children.Add(headerGrid);

                // เพิ่มข้อมูลลูกค้า
                var customerSection = CreateCustomerSection();
                if (customerSection != null)
                    mainStackPanel.Children.Add(customerSection);

                // เพิ่มตารางรายการสินค้า
                var itemsSection = CreateItemsSection();
                if (itemsSection != null)
                    mainStackPanel.Children.Add(itemsSection);

                // เพิ่มส่วนการชำระเงิน
                var paymentSection = CreatePaymentSection();
                if (paymentSection != null)
                    mainStackPanel.Children.Add(paymentSection);

                // เพิ่มส่วนข้อมูลห้องและรางวัล
                var roomAndRewardsSection = CreateRoomAndRewardsSection();
                if (roomAndRewardsSection != null)
                    mainStackPanel.Children.Add(roomAndRewardsSection);

                mainGrid.Children.Add(mainStackPanel);

                Debug.WriteLine("สร้าง Receipt Content สำหรับ A5 เรียบร้อย");
                return mainGrid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Receipt Content: {ex.Message}");
                return null;
            }
        }

        // Add this method to your ReceiptPrintPreview class
        private void ExportPdfButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            // TODO: Implement PDF export logic here
        }

        // สร้างส่วนหัว
        private Grid? CreateHeaderSection()
        {
            try
            {
                var headerGrid = new Grid
                {
                    Margin = new Thickness(0, 0, 0, 15)
                };

                // เพิ่ม row และ column definitions
                headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Logo (ถ้ามี)
                try
                {
                    var logoImage = new Image
                    {
                        Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/AsiaHotelLogo.jpg")),
                        Height = 80,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    Grid.SetRow(logoImage, 0);
                    Grid.SetColumn(logoImage, 0);
                    Grid.SetColumnSpan(logoImage, 2);
                    headerGrid.Children.Add(logoImage);
                }
                catch (Exception logoEx)
                {
                    Debug.WriteLine($"ไม่สามารถโหลดโลโก้ได้: {logoEx.Message}");
                }

                // ข้อมูลบริษัท
                var companyInfo = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Top
                };

                companyInfo.Children.Add(new TextBlock
                {
                    Text = "บริษัท เอเชียโฮเต็ล จำกัด ( มหาชน ) สำนักงานใหญ่",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                });

                companyInfo.Children.Add(new TextBlock
                {
                    Text = "296 ถนนพญาไท แขวงถนนเพชรบุรี",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                });

                companyInfo.Children.Add(new TextBlock
                {
                    Text = "เขตราชเทวี กรุงเทพมหานคร 10400",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                });

                companyInfo.Children.Add(new TextBlock
                {
                    Text = "เลขประจำตัวผู้เสียภาษี 0107535000346",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    FontSize = 12
                });

                companyInfo.Children.Add(new TextBlock
                {
                    Text = "โทร 02-2170808",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    FontSize = 12
                });

                Grid.SetRow(companyInfo, 1);
                Grid.SetColumn(companyInfo, 0);
                headerGrid.Children.Add(companyInfo);

                // ส่วนชื่อเอกสาร
                var receiptTitleBorder = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 194, 213, 242)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(10, 0, 0, 0),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 80, 80, 80)),
                    BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Top
                };

                var titleTextBlock = new TextBlock
                {
                    Text = "ใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ\nต้นฉบับ",
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    FontSize = 16
                };

                receiptTitleBorder.Child = titleTextBlock;
                Grid.SetRow(receiptTitleBorder, 1);
                Grid.SetColumn(receiptTitleBorder, 1);
                headerGrid.Children.Add(receiptTitleBorder);

                return headerGrid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Header: {ex.Message}");
                return null;
            }
        }

        // สร้างส่วนข้อมูลลูกค้า
        private Border? CreateCustomerSection()
        {
            try
            {
                if (_receipt == null) return null;

                var customerBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 80, 80, 80)),
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var customerGrid = new Grid();

                // เพิ่ม row และ column definitions
                customerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                customerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                customerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                customerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                customerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                customerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

                // ชื่อลูกค้า
                var customerNameLabel = CreateBorderedTextBlock("ชื่อลูกค้า :", true, new Thickness(0, 0, 1, 1));
                Grid.SetRow(customerNameLabel, 0);
                Grid.SetColumn(customerNameLabel, 0);
                customerGrid.Children.Add(customerNameLabel);

                var customerNameValue = CreateBorderedTextBlock(_receipt.CustomerName, false, new Thickness(0, 0, 1, 1));
                Grid.SetRow(customerNameValue, 0);
                Grid.SetColumn(customerNameValue, 1);
                customerGrid.Children.Add(customerNameValue);

                // เลขที่
                var receiptNoLabel = CreateBorderedTextBlock("เลขที่ / No.", true, new Thickness(0, 0, 1, 1));
                Grid.SetRow(receiptNoLabel, 0);
                Grid.SetColumn(receiptNoLabel, 2);
                customerGrid.Children.Add(receiptNoLabel);

                var receiptNoValue = CreateBorderedTextBlock(_receipt.ReceiptCode, false, new Thickness(0, 0, 0, 1));
                receiptNoValue.HorizontalAlignment = HorizontalAlignment.Left;
                Grid.SetRow(receiptNoValue, 0);
                Grid.SetColumn(receiptNoValue, 3);
                customerGrid.Children.Add(receiptNoValue);

                // โทรศัพท์
                var phoneLabel = CreateBorderedTextBlock("โทรศัพท์ :", true, new Thickness(0, 0, 1, 0));
                Grid.SetRow(phoneLabel, 1);
                Grid.SetColumn(phoneLabel, 0);
                customerGrid.Children.Add(phoneLabel);

                var phoneValue = CreateBorderedTextBlock(_receipt.CustomerPhoneNumber, false, new Thickness(0, 0, 1, 0));
                Grid.SetRow(phoneValue, 1);
                Grid.SetColumn(phoneValue, 1);
                customerGrid.Children.Add(phoneValue);

                // วันที่
                var dateLabel = CreateBorderedTextBlock("วันที่ / Date", true, new Thickness(0, 0, 1, 1));
                Grid.SetRow(dateLabel, 1);
                Grid.SetColumn(dateLabel, 2);
                customerGrid.Children.Add(dateLabel);

                var thaiCulture = new CultureInfo("th-TH");
                thaiCulture.DateTimeFormat.Calendar = new ThaiBuddhistCalendar();
                var dateValue = CreateBorderedTextBlock(_receipt.ReceiptDate.ToString("dd / MM / yyyy", thaiCulture), false, new Thickness(0, 0, 0, 1));
                dateValue.HorizontalAlignment = HorizontalAlignment.Left; // เปลี่ยนจาก Right เป็น Left
                Grid.SetRow(dateValue, 1);
                Grid.SetColumn(dateValue, 3);
                customerGrid.Children.Add(dateValue);

                customerBorder.Child = customerGrid;
                return customerBorder;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Customer Section: {ex.Message}");
                return null;
            }
        }

        // Helper method สำหรับสร้าง TextBlock ที่มี Border
        private Border CreateBorderedTextBlock(string text, bool isHeader, Thickness borderThickness)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 80, 80, 80)),
                BorderThickness = borderThickness,
                Padding = new Thickness(6)
            };

            if (isHeader)
            {
                border.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 230, 242, 255));
            }

            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
            };

            if (isHeader)
            {
                textBlock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }

            border.Child = textBlock;
            return border;
        }

        // สร้างส่วนตารางรายการสินค้า
        private Border? CreateItemsSection()
        {
            try
            {
                var itemsBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 80, 80, 80)),
                    BorderThickness = new Thickness(2, 0, 2, 2)
                };

                var mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Items
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Total

                // สร้าง Header
                var headerGrid = CreateItemsHeader();
                if (headerGrid != null)
                {
                    Grid.SetRow(headerGrid, 0);
                    mainGrid.Children.Add(headerGrid);
                }

                // สร้างรายการสินค้า
                var itemsStackPanel = new StackPanel();
                for (int i = 0; i < _displayItems.Count; i++)
                {
                    var item = _displayItems[i];
                    var itemRow = CreateItemRow(item);
                    if (itemRow != null)
                    {
                        itemsStackPanel.Children.Add(itemRow);
                    }
                }
                Grid.SetRow(itemsStackPanel, 1);
                mainGrid.Children.Add(itemsStackPanel);

                // สร้างส่วนรวมเงิน
                var totalSection = CreateTotalSection();
                if (totalSection != null)
                {
                    Grid.SetRow(totalSection, 2);
                    mainGrid.Children.Add(totalSection);
                }

                itemsBorder.Child = mainGrid;
                return itemsBorder;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Items Section: {ex.Message}");
                return null;
            }
        }

        // สร้างหัวตาราง
        private Grid? CreateItemsHeader()
        {
            try
            {
                var headerGrid = new Grid();

                // Column definitions
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

                // Row definitions
                headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Headers
                var headers = new[]
                {
            new { Thai = "ลำดับที่", Eng = "Item", Col = 0 },
            new { Thai = "รายการ", Eng = "Descriptions", Col = 1 },
            new { Thai = "จำนวน", Eng = "Quantity", Col = 2 },
            new { Thai = "ราคาต่อหน่วย", Eng = "Unit price", Col = 3 },
            new { Thai = "จำนวนเงิน", Eng = "Amount", Col = 4 }
        };

                foreach (var header in headers)
                {
                    // Thai header
                    var thaiHeader = CreateHeaderCell(header.Thai, new Thickness(0, 1, header.Col == 4 ? 0 : 1, 0));
                    Grid.SetRow(thaiHeader, 0);
                    Grid.SetColumn(thaiHeader, header.Col);
                    headerGrid.Children.Add(thaiHeader);

                    // English header
                    var engHeader = CreateHeaderCell(header.Eng, new Thickness(0, 1, header.Col == 4 ? 0 : 1, 1), 10);
                    Grid.SetRow(engHeader, 1);
                    Grid.SetColumn(engHeader, header.Col);
                    headerGrid.Children.Add(engHeader);
                }

                return headerGrid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Items Header: {ex.Message}");
                return null;
            }
        }

        // สร้าง Header Cell
        private Border CreateHeaderCell(string text, Thickness borderThickness, double fontSize = 11)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 80, 80, 80)),
                BorderThickness = borderThickness,
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 194, 213, 242))
            };

            var textBlock = new TextBlock
            {
                Text = text,
                TextAlignment = TextAlignment.Center,
                FontWeight = fontSize > 10 ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                FontSize = fontSize,
                Margin = new Thickness(3),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
            };

            border.Child = textBlock;
            return border;
        }

        // สร้างแถวรายการสินค้า
        private Grid? CreateItemRow(ReceiptItemDisplay item)
        {
            try
            {
                var itemGrid = new Grid
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                    Height = 35
                };

                // Column definitions
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

                // Index
                var indexBorder = CreateItemCell(item.Index.ToString(), TextAlignment.Center, new Thickness(0, 0, 1, 1));
                Grid.SetColumn(indexBorder, 0);
                itemGrid.Children.Add(indexBorder);

                // Name
                var nameBorder = CreateItemCell(item.Name, TextAlignment.Left, new Thickness(0, 0, 1, 1), new Thickness(5, 0, 0, 0));
                Grid.SetColumn(nameBorder, 1);
                itemGrid.Children.Add(nameBorder);

                // Quantity
                var quantityBorder = CreateItemCell(item.Quantity.ToString(), TextAlignment.Center, new Thickness(0, 0, 1, 1));
                Grid.SetColumn(quantityBorder, 2);
                itemGrid.Children.Add(quantityBorder);

                // Unit Price
                var unitPriceBorder = CreateItemCell(item.UnitPriceFormatted, TextAlignment.Right, new Thickness(0, 0, 1, 1), new Thickness(0, 0, 5, 0));
                Grid.SetColumn(unitPriceBorder, 3);
                itemGrid.Children.Add(unitPriceBorder);

                // Total Price
                var totalPriceBorder = CreateItemCell(item.TotalPriceFormatted, TextAlignment.Right, new Thickness(0, 0, 0, 1), new Thickness(0, 0, 5, 0));
                Grid.SetColumn(totalPriceBorder, 4);
                itemGrid.Children.Add(totalPriceBorder);

                return itemGrid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Item Row: {ex.Message}");
                return null;
            }
        }

        // สร้าง Item Cell
        private Border CreateItemCell(string text, TextAlignment textAlignment, Thickness borderThickness, Thickness? margin = null)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 80, 80, 80)),
                BorderThickness = borderThickness
            };

            var textBlock = new TextBlock
            {
                Text = text,
                TextAlignment = textAlignment,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin ?? new Thickness(0)
            };

            border.Child = textBlock;
            return border;
        }

        // สร้างส่วนรวมเงิน
        private Grid? CreateTotalSection()
        {
            try
            {
                if (_receipt == null) return null;

                var totalGrid = new Grid();
                totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

                // Left side - Thai text
                var thaiTextBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 80, 80, 80)),
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    Padding = new Thickness(6)
                };

                var thaiTextBlock = new TextBlock
                {
                    Text = ConvertNumberToThaiText(_receipt.TotalAmount),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    TextWrapping = TextWrapping.Wrap
                };

                thaiTextBorder.Child = thaiTextBlock;
                Grid.SetColumn(thaiTextBorder, 0);
                totalGrid.Children.Add(thaiTextBorder);

                // Right side - Total amount
                var totalAmountBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red),
                    BorderThickness = new Thickness(2),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.White)
                };

                var totalAmountTextBlock = new TextBlock
                {
                    Text = _receipt.TotalAmount.ToString("N2"),
                    TextAlignment = TextAlignment.Right,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6)
                };

                totalAmountBorder.Child = totalAmountTextBlock;
                Grid.SetColumn(totalAmountBorder, 1);
                totalGrid.Children.Add(totalAmountBorder);

                return totalGrid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Total Section: {ex.Message}");
                return null;
            }
        }

        // สร้างส่วนการชำระเงิน (ตรงกับ XAML)
        private StackPanel? CreatePaymentSection()
        {
            try
            {
                var paymentStackPanel = new StackPanel
                {
                    Margin = new Thickness(0, 15, 0, 0)
                };

                // หัวข้อ "ชำระโดย"
                var paymentLabel = new TextBlock
                {
                    Text = "ชำระโดย",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 6),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                };
                paymentStackPanel.Children.Add(paymentLabel);

                // Grid สำหรับวิธีการชำระเงิน
                var paymentGrid = new Grid();
                paymentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                paymentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                paymentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                paymentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var paymentMethods = new[] { "เงินสด", "กิจเงิน", "เครดิตการ์ด", "QR" };

                for (int i = 0; i < paymentMethods.Length; i++)
                {
                    var paymentPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 0, 15, 0)
                    };

                    var checkbox = new Border
                    {
                        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        BorderThickness = new Thickness(1),
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(0, 0, 6, 0)
                    };

                    var label = new TextBlock
                    {
                        Text = paymentMethods[i],
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                    };

                    paymentPanel.Children.Add(checkbox);
                    paymentPanel.Children.Add(label);
                    Grid.SetColumn(paymentPanel, i);
                    paymentGrid.Children.Add(paymentPanel);
                }

                paymentStackPanel.Children.Add(paymentGrid);
                return paymentStackPanel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Payment Section: {ex.Message}");
                return null;
            }
        }

        // สร้างส่วนข้อมูลห้องและรางวัล (ตรงกับ XAML)
        private Grid? CreateRoomAndRewardsSection()
        {
            try
            {
                var roomRewardsGrid = new Grid
                {
                    Margin = new Thickness(0, 15, 0, 0)
                };

                roomRewardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                roomRewardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

                // ส่วนซ้าย - ข้อมูลห้องและรางวัล
                var leftStackPanel = new StackPanel();

                // หมายเลขห้องอาหาร
                var restaurantPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                restaurantPanel.Children.Add(new TextBlock
                {
                    Text = "หมายเลขห้องอาหาร",
                    FontSize = 12,
                    Width = 130,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                });
                restaurantPanel.Children.Add(new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Width = 170,
                    Height = 16
                });
                leftStackPanel.Children.Add(restaurantPanel);

                // หมายเลขห้องพัก
                var roomPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                roomPanel.Children.Add(new TextBlock
                {
                    Text = "หมายเลขห้องพัก",
                    FontSize = 12,
                    Width = 130,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                });
                roomPanel.Children.Add(new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Width = 170,
                    Height = 16
                });
                leftStackPanel.Children.Add(roomPanel);

                // ส่วนรางวัล
                var rewardsLabel = new TextBlock
                {
                    Text = "รางวัล",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 6),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                };
                leftStackPanel.Children.Add(rewardsLabel);

                // รายการรางวัล
                var rewardsStackPanel = new StackPanel();
                var rewards = new[] { "cocktail 1 แก้ว", "Wine 1 ขวด", "พิซซ่า 1 ถาด", "เฝอ 1 ที่", "เป็ด 1 ตัว" };

                foreach (var reward in rewards)
                {
                    var rewardPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 0, 10, 5)
                    };

                    var checkbox = new Border
                    {
                        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        BorderThickness = new Thickness(1),
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(0, 0, 6, 0)
                    };

                    var label = new TextBlock
                    {
                        Text = reward,
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                    };

                    rewardPanel.Children.Add(checkbox);
                    rewardPanel.Children.Add(label);
                    rewardsStackPanel.Children.Add(rewardPanel);
                }

                leftStackPanel.Children.Add(rewardsStackPanel);
                Grid.SetColumn(leftStackPanel, 0);
                roomRewardsGrid.Children.Add(leftStackPanel);

                // ส่วนขวา - ลายเซ็นผู้รับเงิน
                var signaturePanel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Bottom
                };

                signaturePanel.Children.Add(new TextBlock
                {
                    Text = "ผู้รับเงิน",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 20),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                });

                signaturePanel.Children.Add(new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Width = 120,
                    Height = 16,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                Grid.SetColumn(signaturePanel, 1);
                roomRewardsGrid.Children.Add(signaturePanel);

                return roomRewardsGrid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Room and Rewards Section: {ex.Message}");
                return null;
            }
        }

        // Helper method แปลงตัวเลขเป็นข้อความไทย - ใช้ ThaiNumberToTextConverter
        private string ConvertNumberToThaiText(decimal amount)
        {
            try
            {
                // ใช้ ThaiNumberToTextConverter เพื่อแปลงเลขเป็นข้อความไทย
                var converter = new ThaiNumberToTextConverter();
                var result = converter.Convert(amount, typeof(string), string.Empty, "th-TH");

                // แก้ไข warning CS8625 โดยใช้ string.Empty แทน null
                return result?.ToString() ?? $"รวม ({amount:N2} บาท)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการแปลงตัวเลขเป็นภาษาไทย: {ex.Message}");
                return $"รวม ({amount:N2} บาท)"; // fallback เมื่อเกิดข้อผิดพลาด
            }
        }
    }
}