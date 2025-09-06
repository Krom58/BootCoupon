using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Printing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
using WinRT.Interop;

namespace BootCoupon
{
    public static class SalesReportPrintService
    {
        private static PrintManager? printManager;
        private static PrintDocument? printDocument;
        private static IPrintDocumentSource? printDocumentSource;
        private static List<FrameworkElement> printPages = new List<FrameworkElement>();
        private static SalesReportViewModel? currentViewModel;

        // Dialog management
        private static readonly SemaphoreSlim dialogSemaphore = new(1, 1);
        private static bool isPrintingInProgress = false;

        /// <summary>
        /// พิมพ์รายงานการขายโดยไม่ต้องเปิดหน้า Preview
        /// </summary>
        /// <param name="viewModel">ViewModel ที่มีข้อมูลรายงาน</param>
        /// <param name="xamlRoot">XamlRoot สำหรับแสดง dialog</param>
        /// <returns>true ถ้าพิมพ์สำเร็จ, false ถ้าไม่สำเร็จ</returns>
        public static async Task<bool> PrintSalesReportAsync(SalesReportViewModel viewModel, XamlRoot xamlRoot)
        {
            // ป้องกันการพิมพ์พร้อมกัน
            if (isPrintingInProgress)
            {
                await ShowErrorDialogSafe(xamlRoot, "กำลังมีการพิมพ์อยู่แล้ว กรุณารอสักครู่");
                return false;
            }

            try
            {
                isPrintingInProgress = true;
                Debug.WriteLine("เริ่มพิมพ์รายงานการขาย");

                // ตรวจสอบข้อมูล
                if (!viewModel.HasData)
                {
                    await ShowErrorDialogSafe(xamlRoot, "ไม่มีข้อมูลให้พิมพ์ กรุณาค้นหาข้อมูลก่อน");
                    return false;
                }

                // เก็บ viewModel สำหรับใช้ในการสร้างหน้าพิมพ์
                currentViewModel = viewModel;

                // สร้างหน้าพิมพ์
                await GeneratePrintPagesAsync();

                // เริ่มกระบวนการพิมพ์
                return await InitiatePrintProcessAsync(xamlRoot);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการพิมพ์: {ex.Message}");
                await ShowErrorDialogSafe(xamlRoot, $"เกิดข้อผิดพลาดในการพิมพ์: {ex.Message}");
                return false;
            }
            finally
            {
                isPrintingInProgress = false;
            }
        }

        private static Task GeneratePrintPagesAsync()
        {
            try
            {
                if (currentViewModel == null) return Task.CompletedTask;

                // Clear existing pages
                printPages.Clear();

                // !! เพิ่มจำนวนแถวต่อหน้าให้มากขึ้น !!
                var itemsPerPage = 45; // เพิ่มจาก 30 เป็น 45 แถว
                var totalItems = currentViewModel.ReportData.Count;
                var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / itemsPerPage));

                Debug.WriteLine($"Total items: {totalItems}, Items per page: {itemsPerPage}, Total pages: {totalPages}");

                // Generate all pages
                for (int pageNumber = 1; pageNumber <= totalPages; pageNumber++)
                {
                    var page = CreatePrintPage(pageNumber);
                    printPages.Add(page);
                }

                Debug.WriteLine($"Generated {printPages.Count} print pages");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating print pages: {ex.Message}");
                throw;
            }
        }

        private static async Task<bool> InitiatePrintProcessAsync(XamlRoot xamlRoot)
        {
            try
            {
                Debug.WriteLine("เริ่มต้นกระบวนการพิมพ์รายงาน");

                // รอสักครู่ก่อนเริ่มพิมพ์
                await Task.Delay(200);

                // !! ย้าย CleanupPrintResources() ไปก่อน GeneratePrintPagesAsync() !!
                // หรือแก้ไข CleanupPrintResources() ให้ไม่ clear printPages

                // สร้าง PrintDocument ใหม่
                printDocument = new PrintDocument();
                printDocumentSource = printDocument.DocumentSource;

                // ผูก events
                printDocument.Paginate += PrintDocument_Paginate;
                printDocument.GetPreviewPage += PrintDocument_GetPreviewPage;
                printDocument.AddPages += PrintDocument_AddPages;

                // วิธีใหม่สำหรับ WinUI 3
                if (App.MainWindowInstance != null)
                {
                    var windowHandle = WindowNative.GetWindowHandle(App.MainWindowInstance);
                    printManager = PrintManagerInterop.GetForWindow(windowHandle);

                    printManager.PrintTaskRequested -= PrintManager_PrintTaskRequestedHandler;
                    printManager.PrintTaskRequested += PrintManager_PrintTaskRequestedHandler;

                    // แสดง Print UI โดยตรง
                    await PrintManagerInterop.ShowPrintUIForWindowAsync(windowHandle);

                    Debug.WriteLine("แสดง Print UI สำหรับรายงานการขายเรียบร้อย");
                    return true;
                }
                else
                {
                    throw new Exception("ไม่สามารถหา Main Window ได้");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการพิมพ์: {ex.Message}");
                await ShowErrorDialogSafe(xamlRoot, $"ไม่สามารถเข้าถึงเครื่องพิมพ์ได้: {ex.Message}");
                return false;
            }
        }

        private static void PrintManager_PrintTaskRequestedHandler(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            try
            {
                Debug.WriteLine("PrintTaskRequested สำหรับรายงานการขาย");

                var printTask = args.Request.CreatePrintTask("รายงานการขาย", sourceRequestedArgs =>
                {
                    Debug.WriteLine("กำลังตั้งค่า PrintDocumentSource สำหรับรายงาน");
                    if (printDocumentSource != null)
                    {
                        sourceRequestedArgs.SetSource(printDocumentSource);
                    }
                });

                if (printTask != null)
                {
                    printTask.Completed += PrintTask_CompletedHandler;

                    // ตั้งค่าสำหรับการพิมพ์ A4
                    printTask.Options.MediaSize = PrintMediaSize.IsoA4;
                    printTask.Options.Orientation = PrintOrientation.Portrait;
                    printTask.Options.PrintQuality = PrintQuality.Normal;

                    Debug.WriteLine("ตั้งค่า PrintTask สำหรับรายงาน A4 เรียบร้อย");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดใน PrintTaskRequested: {ex.Message}");
            }
        }

        private static void PrintTask_CompletedHandler(PrintTask sender, PrintTaskCompletedEventArgs args)
        {
            Debug.WriteLine($"Print task completed with status: {args.Completion}");

            if (args.Completion == PrintTaskCompletion.Submitted)
            {
                Debug.WriteLine("การพิมพ์รายงานเสร็จสมบูรณ์");

                _ = App.MainWindowInstance?.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (App.MainWindowInstance?.Content is FrameworkElement element)
                        {
                            await ShowSuccessDialogSafe(element.XamlRoot,
                                "รายงานการขายถูกส่งไปยังเครื่องพิมพ์เรียบร้อยแล้ว");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ข้อผิดพลาดในการแสดง success dialog: {ex.Message}");
                    }
                });
            }
            else if (args.Completion == PrintTaskCompletion.Failed)
            {
                Debug.WriteLine("การพิมพ์รายงานล้มเหลว");

                _ = App.MainWindowInstance?.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (App.MainWindowInstance?.Content is FrameworkElement element)
                        {
                            await ShowErrorDialogSafe(element.XamlRoot,
                                "ไม่สามารถพิมพ์รายงานได้ กรุณาลองใหม่อีกครั้ง");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ข้อผิดพลาดในการแสดง error dialog: {ex.Message}");
                    }
                });
            }

            // ทำความสะอาด
            CleanupPrintResources();
            FinalCleanup(); // เพิ่มการ cleanup สุดท้าย
            isPrintingInProgress = false;
        }

        private static void PrintDocument_Paginate(object sender, PaginateEventArgs e)
        {
            try
            {
                Debug.WriteLine("PrintDocument_Paginate สำหรับรายงาน");

                if (printDocument != null)
                {
                    printDocument.SetPreviewPageCount(printPages.Count, PreviewPageCountType.Final);
                    Debug.WriteLine($"กำหนดจำนวนหน้าเป็น {printPages.Count} หน้า");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดใน Paginate: {ex.Message}");
            }
        }

        private static void PrintDocument_GetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            try
            {
                Debug.WriteLine($"PrintDocument_GetPreviewPage หน้า {e.PageNumber}");

                if (printDocument != null && e.PageNumber <= printPages.Count)
                {
                    var page = printPages[e.PageNumber - 1];
                    printDocument.SetPreviewPage(e.PageNumber, page);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดใน GetPreviewPage: {ex.Message}");
            }
        }

        private static void PrintDocument_AddPages(object sender, AddPagesEventArgs e)
        {
            try
            {
                Debug.WriteLine("PrintDocument_AddPages สำหรับรายงาน");

                if (printDocument != null)
                {
                    foreach (var page in printPages)
                    {
                        printDocument.AddPage(page);
                    }
                    printDocument.AddPagesComplete();
                    Debug.WriteLine("เพิ่มหน้าสำหรับพิมพ์รายงานเรียบร้อย");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดใน AddPages: {ex.Message}");
            }
        }

        // แก้ไข CreatePrintPage method
        private static FrameworkElement CreatePrintPage(int pageNumber)
        {
            if (currentViewModel == null)
                return new Grid();

            var page = new Grid
            {
                Width = 794,   // A4 width
                Height = 1123, // A4 height
                Background = new SolidColorBrush(Microsoft.UI.Colors.White)
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(25) // ลด margin เพื่อให้มีพื้นที่มากขึ้น
            };
            
            // Header - ทำให้เล็กลง
            var headerText = new TextBlock
            {
                Text = "รายงานการขาย",
                FontSize = 18, // ลดจาก 20
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10), // ลด margin
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
            };
            stackPanel.Children.Add(headerText);

            // Report info - ทำให้กะทัดรัดขึ้น
            var infoPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) }; // ลด margin
            
            // รวม info ในบรรทัดเดียว
            var infoText = $"วันที่พิมพ์: {DateTime.Now:dd/MM/yyyy HH:mm} | " +
                           $"ช่วงวันที่: {currentViewModel.StartDate?.ToString("dd/MM/yyyy")} - {currentViewModel.EndDate?.ToString("dd/MM/yyyy")}";
            
            // เพิ่ม filter info ในบรรทัดเดียว
            var filterParts = new List<string>();
            if (currentViewModel.SelectedSalesPerson != null && currentViewModel.SelectedSalesPerson.ID != 0)
                filterParts.Add($"เซล: {currentViewModel.SelectedSalesPerson.Name}");
            if (currentViewModel.SelectedCouponType != null && currentViewModel.SelectedCouponType.Id != 0)
                filterParts.Add($"ประเภท: {currentViewModel.SelectedCouponType.Name}");
            if (currentViewModel.SelectedCoupon != null && currentViewModel.SelectedCoupon.Id != 0)
                filterParts.Add($"คูปอง: {currentViewModel.SelectedCoupon.Name}");
            if (currentViewModel.SelectedPaymentMethod != null && currentViewModel.SelectedPaymentMethod.Id != 0)
                filterParts.Add($"การชำระ: {currentViewModel.SelectedPaymentMethod.Name}");
            
            if (filterParts.Any())
                infoText += " | " + string.Join(" | ", filterParts);
            
            infoText += $" | หน้า {pageNumber}";

            infoPanel.Children.Add(new TextBlock 
            { 
                Text = infoText,
                FontSize = 10, // ลดขนาด
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                TextWrapping = TextWrapping.Wrap
            });

            stackPanel.Children.Add(infoPanel);

            // Table - ปรับขนาดคอลัมน์ให้เหมาะสมกับ A4 และไม่ล้นออกนอก
            var table = new Grid();
            
            // คำนวณขนาดคอลัมน์ใหม่ - รวม 744 pixels (794 - 50 margin)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) }); // วันที่
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) }); // ใบเสร็จ
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); // ลูกค้า
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // เซล
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // คูปอง - เพิ่มขึ้น
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // ประเภท
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) }); // การชำระ
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // จำนวน
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) }); // ราคา

            // Header row - ลดขนาดเพื่อประหยัดพื้นที่
            var headers = new[] { "วันที่", "ใบเสร็จ", "ลูกค้า", "เซล", "คูปอง", "ประเภท", "การชำระ", "จำนวน", "รวม" };
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0.5), // ลดความหนาเส้น
                    Child = new TextBlock
                    {
                        Text = headers[i],
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 8, // ลดขนาด
                        Margin = new Thickness(2, 2, 2, 2), // ลด margin
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                Grid.SetColumn(headerCell, i);
                Grid.SetRow(headerCell, 0);
                table.Children.Add(headerCell);
            }

            // Data rows - เพิ่มจำนวนแถวต่อหน้า
            var itemsPerPage = 45; // เพิ่มจาก 30 เป็น 45
            var startIndex = (pageNumber - 1) * itemsPerPage;
            var endIndex = Math.Min(startIndex + itemsPerPage, currentViewModel.ReportData.Count);

            Debug.WriteLine($"Page {pageNumber}: startIndex={startIndex}, endIndex={endIndex}, total items={currentViewModel.ReportData.Count}");

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = currentViewModel.ReportData[i];
                var rowIndex = i - startIndex + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) }); // เพิ่มความสูงแถวเล็กน้อย

                // !! แก้ไข: ใช้ชื่อเต็มไม่ตัด !!
                var rowData = new[]
                {
                    item.ReceiptDate.ToString("dd/MM/yy"),
                    item.ReceiptCode,
                    item.CustomerName ?? "", // ใช้ชื่อเต็ม
                    item.SalesPersonName ?? "", // ใช้ชื่อเต็ม
                    item.CouponName ?? "", // ใช้ชื่อเต็ม
                    item.CouponTypeName ?? "", // ใช้ชื่อเต็ม
                    item.PaymentMethodName ?? "", // ใช้ชื่อเต็ม
                    item.Quantity.ToString(),
                    item.TotalPrice.ToString("N2")
                };

                for (int j = 0; j < rowData.Length; j++)
                {
                    var cell = new Border
                    {
                        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        BorderThickness = new Thickness(0.5),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                        Child = new TextBlock
                        {
                            Text = rowData[j],
                            FontSize = 7, // อาจจะลดเป็น 6 ถ้าข้อความยาวมาก
                            Margin = new Thickness(1, 1, 1, 1),
                            TextAlignment = j >= 7 ? TextAlignment.Right : TextAlignment.Left,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap, // !! เปลี่ยนเป็น Wrap เพื่อให้ข้อความแบ่งบรรทัดได้ !!
                            TextTrimming = TextTrimming.None // !! เอา trimming ออก !!
                        }
                    };
                    Grid.SetColumn(cell, j);
                    Grid.SetRow(cell, rowIndex);
                    table.Children.Add(cell);
                }
            }

            stackPanel.Children.Add(table);

            // Summary (only on last page) - ทำให้กะทัดรัดขึ้น
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)currentViewModel.ReportData.Count / itemsPerPage));
            if (pageNumber == totalPages)
            {
                var summaryPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; // ลด margin
                
                var summaryText = $"จำนวนรายการทั้งหมด: {currentViewModel.ReportData.Count:N0} รายการ | " +
                                 $"ยอดรวมทั้งหมด: {currentViewModel.ReportData.Sum(x => x.TotalPrice):N2} บาท";
                
                summaryPanel.Children.Add(new TextBlock 
                { 
                    Text = summaryText,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    FontSize = 10, // ลดขนาด
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                });
                stackPanel.Children.Add(summaryPanel);
            }

            page.Children.Add(stackPanel);
            return page;
        }

        // เพิ่ม helper method ใหม่ที่เหมาะสมกับการพิมพ์
        private static string OptimizedTruncateString(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            
            if (text.Length <= maxLength)
                return text;
            
            // ตัดให้เหลือ maxLength - 3 แล้วเติม "..."
            return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        private static void CleanupPrintResources()
        {
            try
            {
                if (printManager != null)
                {
                    printManager.PrintTaskRequested -= PrintManager_PrintTaskRequestedHandler;
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
                // !! ไม่ clear printPages และ currentViewModel ที่นี่ !!
                // printPages.Clear();
                // currentViewModel = null;

                Debug.WriteLine("ทำความสะอาด Print resources สำหรับรายงานเรียบร้อย");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการทำความสะอาด: {ex.Message}");
            }
        }

        // เพิ่ม method ใหม่สำหรับ cleanup หลังจากพิมพ์เสร็จ
        private static void FinalCleanup()
        {
            try
            {
                printPages.Clear();
                currentViewModel = null;
                Debug.WriteLine("ทำความสะอาดสุดท้ายเรียบร้อย");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการทำความสะอาดสุดท้าย: {ex.Message}");
            }
        }

        private static async Task ShowErrorDialogSafe(XamlRoot xamlRoot, string message)
        {
            await dialogSemaphore.WaitAsync();
            try
            {
                var errorDialog = new ContentDialog
                {
                    Title = "ข้อผิดพลาด",
                    Content = message,
                    CloseButtonText = "ตกลง",
                    XamlRoot = xamlRoot
                };

                await errorDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการแสดง error dialog: {ex.Message}");
            }
            finally
            {
                dialogSemaphore.Release();
            }
        }

        private static async Task ShowSuccessDialogSafe(XamlRoot xamlRoot, string message)
        {
            await dialogSemaphore.WaitAsync();
            try
            {
                var successDialog = new ContentDialog
                {
                    Title = "สำเร็จ",
                    Content = message,
                    CloseButtonText = "ตกลง",
                    XamlRoot = xamlRoot
                };

                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการแสดง success dialog: {ex.Message}");
            }
            finally
            {
                dialogSemaphore.Release();
            }
        }
    }
}
