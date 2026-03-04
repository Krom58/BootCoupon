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
using CouponManagement.Shared;
using CouponManagement.Shared.Models;

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

        // ✅ Single source of truth for items per page
        private static int currentItemsPerPage = 45;

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

                // ✅ ทำความสะอาดข้อมูลเก่าก่อนเริ่มใหม่
                await CleanupAllResourcesAsync();

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
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                await ShowErrorDialogSafe(xamlRoot, $"เกิดข้อผิดพลาดในการพิมพ์: {ex.Message}");
                
                // ✅ Cleanup เมื่อเกิด error
                await CleanupAllResourcesAsync();
                return false;
            }
            finally
            {
                // ไม่ reset isPrintingInProgress ที่นี่ เพราะจะทำใน PrintTask_CompletedHandler
            }
        }

        private static Task GeneratePrintPagesAsync()
        {
            try
            {
                if (currentViewModel == null) return Task.CompletedTask;

                // Clear existing pages
                printPages.Clear();

                // ✅ บังคับให้เป็น 45 แถวต่อหน้าตลอด
                var totalItems = currentViewModel.TotalItems;
                currentItemsPerPage = 45;
                var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / currentItemsPerPage));

                Debug.WriteLine($"Total items: {totalItems}, Items per page: {currentItemsPerPage}, Total pages: {totalPages}");

                // ✅ แจ้งเตือนถ้ามีหน้าเยอะมาก
                if (totalPages > 50)
                {
                    Debug.WriteLine($"⚠️ Warning: มีหน้าเยอะมาก ({totalPages} หน้า) - อาจใช้เวลานาน");
                }

                // Generate all pages
                for (int pageNumber = 1; pageNumber <= totalPages; pageNumber++)
                {
                    try
                    {
                        var page = CreatePrintPage(pageNumber);
                        
                        // ✅ Validate page before adding
                        if (page != null)
                        {
                            printPages.Add(page);
                        }
                        else
                        {
                            Debug.WriteLine($"⚠️ หน้า {pageNumber} เป็น null - skip");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"✗ ข้อผิดพลาดในการสร้างหน้า {pageNumber}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"Generated {printPages.Count}/{totalPages} print pages");
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating print pages: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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

            // ✅ จัดการทุก status ที่เป็นไปได้
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
                    finally
                    {
                        // ✅ Cleanup หลังแสดง dialog
                        await CleanupAllResourcesAsync();
                        isPrintingInProgress = false;
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
                    finally
                    {
                        // ✅ Cleanup หลัง error
                        await CleanupAllResourcesAsync();
                        isPrintingInProgress = false;
                    }
                });
            }
            else if (args.Completion == PrintTaskCompletion.Canceled)
            {
                // ✅ เพิ่มการจัดการเมื่อผู้ใช้ยกเลิก
                Debug.WriteLine("ผู้ใช้ยกเลิกการพิมพ์");

                _ = App.MainWindowInstance?.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        Debug.WriteLine("ทำความสะอาดหลังยกเลิกการพิมพ์");
                    }
                    finally
                    {
                        // ✅ Cleanup แม้จะยกเลิก
                        await CleanupAllResourcesAsync();
                        isPrintingInProgress = false;
                    }
                });
            }
            else
            {
                // ✅ จัดการ status อื่นๆ ที่ไม่คาดคิด
                Debug.WriteLine($"Print task completed with unexpected status: {args.Completion}");
                
                _ = App.MainWindowInstance?.DispatcherQueue.TryEnqueue(async () =>
                {
                    await CleanupAllResourcesAsync();
                    isPrintingInProgress = false;
                });
            }
        }

        private static void PrintDocument_Paginate(object sender, PaginateEventArgs e)
        {
            try
            {
                Debug.WriteLine("PrintDocument_Paginate สำหรับรายงาน");

                if (printDocument != null && printPages.Count > 0)
                {
                    printDocument.SetPreviewPageCount(printPages.Count, PreviewPageCountType.Final);
                    Debug.WriteLine($"กำหนดจำนวนหน้าเป็น {printPages.Count} หน้า");
                }
                else
                {
                    Debug.WriteLine("⚠️ Warning: printDocument is null or printPages is empty");
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

                if (printDocument != null && e.PageNumber > 0 && e.PageNumber <= printPages.Count)
                {
                    var page = printPages[e.PageNumber - 1];
                    printDocument.SetPreviewPage(e.PageNumber, page);
                }
                else
                {
                    Debug.WriteLine($"⚠️ Cannot get preview page {e.PageNumber}. Total pages: {printPages.Count}");
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

                if (printDocument != null && printPages.Count > 0)
                {
                    var addedCount = 0;
                    var failedPages = new List<int>();

                    for (int i = 0; i < printPages.Count; i++)
                    {
                        try
                        {
                            var page = printPages[i];

                            // ✅ Validate page before adding
                            if (page != null && page is Grid grid && grid.Children.Count > 0)
                            {
                                printDocument.AddPage(page);
                                addedCount++;
                                Debug.WriteLine($"✓ เพิ่มหน้า {i + 1} สำเร็จ");
                            }
                            else
                            {
                                failedPages.Add(i + 1);
                                Debug.WriteLine($"✗ หน้า {i + 1} ไม่ valid - skip");
                            }
                        }
                        catch (Exception ex)
                        {
                            failedPages.Add(i + 1);
                            Debug.WriteLine($"✗ ข้อผิดพลาดในการเพิ่มหน้า {i + 1}: {ex.Message}");
                        }
                    }

                    printDocument.AddPagesComplete();

                    Debug.WriteLine($"เพิ่มหน้าสำหรับพิมพ์รายงานเรียบร้อย: {addedCount}/{printPages.Count} หน้า");

                    if (failedPages.Any())
                    {
                        Debug.WriteLine($"⚠️ หน้าที่ล้มเหลว: {string.Join(", ", failedPages)}");
                    }
                }
                else
                {
                    Debug.WriteLine("⚠️ Warning: Cannot add pages - printDocument or printPages is null/empty");
                    printDocument?.AddPagesComplete();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดใน AddPages: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                printDocument?.AddPagesComplete();
            }
        }

        // แก้ไข CreatePrintPage method
        private static FrameworkElement CreatePrintPage(int pageNumber)
        {
            if (currentViewModel == null)
                return new Grid();

            var page = new Grid
            {
                Width = 794, // A4 width
                Height = 1123, // A4 height
                Background = new SolidColorBrush(Microsoft.UI.Colors.White)
            };

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Header
            var headerText = new TextBlock
            {
                Text = "รายงานการขาย",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
            };
            stackPanel.Children.Add(headerText);

            // Report info
            var infoPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            var infoText = $"วันที่พิมพ์: {DateTime.Now:dd/MM/yyyy HH:mm} | " +
                          $"ช่วงวันที่: {currentViewModel.StartDate?.ToString("dd/MM/yyyy")} - {currentViewModel.EndDate?.ToString("dd/MM/yyyy")}";

            var filterParts = new List<string>();
            if (currentViewModel.SelectedSalesPerson != null && currentViewModel.SelectedSalesPerson.ID != 0)
                filterParts.Add($"เซล: {currentViewModel.SelectedSalesPerson.Name}");
            if (currentViewModel.SelectedBranch != null && currentViewModel.SelectedBranch.Id != 0)
                filterParts.Add($"สาขา: {currentViewModel.SelectedBranch.Name}");
            if (currentViewModel.SelectedCoupon != null && currentViewModel.SelectedCoupon.Id != 0)
                filterParts.Add($"คูปอง: {currentViewModel.SelectedCoupon.Name}");
            if (currentViewModel.SelectedPaymentMethod != null && currentViewModel.SelectedPaymentMethod.Id != 0)
                filterParts.Add($"การชำระ: {currentViewModel.SelectedPaymentMethod.Name}");

            if (filterParts.Any())
                infoText += " | " + string.Join(" | ", filterParts);

            // เพิ่มชื่อรายงาน
            var reportModeName = currentViewModel.ReportMode switch
            {
                SalesReportViewModel.ReportModes.ByReceipt => "จัดตามใบเสร็จ",
                SalesReportViewModel.ReportModes.LimitedCoupons => "คูปองจำกัดจำนวนพร้อมชื่อลูกค้า",
                SalesReportViewModel.ReportModes.UnlimitedGrouped => "คูปองไม่จำกัด (รวมตามลูกค้า)",
                SalesReportViewModel.ReportModes.SummaryByCoupon => "สรุปตามคูปอง",
                SalesReportViewModel.ReportModes.RemainingCoupons => "จำนวนคูปองที่เหลือ",
                SalesReportViewModel.ReportModes.CancelledReceipts => "ใบเสร็จที่ถูกยกเลิก",
                SalesReportViewModel.ReportModes.CancelledCoupons => "คูปองที่ถูกยกเลิก",
                _ => ""
            };

            // ✅ ใช้ printPages.Count แทนการคำนวณใหม่
            var totalPages = printPages.Count;
            infoText += $" | รูปแบบ: {reportModeName} | หน้า {pageNumber}/{totalPages}";

            // ⭐ เพิ่มคำอธิบายพิเศษสำหรับ RemainingCoupons
            if (currentViewModel.ReportMode == SalesReportViewModel.ReportModes.RemainingCoupons)
            {
                infoText += $"\nหมายเหตุ: 'ขายแล้ว' = จำนวนคูปองที่ขายในช่วงวันที่ที่เลือก";
            }

            infoPanel.Children.Add(new TextBlock
            {
                Text = infoText,
                FontSize = 10,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                TextWrapping = TextWrapping.Wrap
            });

            stackPanel.Children.Add(infoPanel);

            // สร้างตารางตามโหมดรายงาน
            Grid table;
            try
            {
                if (currentViewModel.ReportMode == SalesReportViewModel.ReportModes.SummaryByCoupon)
                {
                    table = CreateSummaryByCouponTable(pageNumber);
                }
                else if (currentViewModel.ReportMode == SalesReportViewModel.ReportModes.RemainingCoupons)
                {
                    table = CreateRemainingCouponsTable(pageNumber);
                }
                else if (currentViewModel.ReportMode == SalesReportViewModel.ReportModes.CancelledReceipts)
                {
                    table = CreateCancelledReceiptsTable(pageNumber);
                }
                else if (currentViewModel.ReportMode == SalesReportViewModel.ReportModes.CancelledCoupons)
                {
                    table = CreateCancelledCouponsTable(pageNumber);
                }
                else if (currentViewModel.ReportMode == SalesReportViewModel.ReportModes.LimitedCoupons)
                {
                    table = CreateLimitedCouponsTable(pageNumber);
                }
                else if (currentViewModel.ReportMode == SalesReportViewModel.ReportModes.UnlimitedGrouped)
                {
                    table = CreateUnlimitedGroupedTable(pageNumber);
                }
                else
                {
                    table = CreateByReceiptTable(pageNumber);
                }

                stackPanel.Children.Add(table);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"✗ ข้อผิดพลาดในการสร้างตารางหน้า {pageNumber}: {ex.Message}");
                
                // ✅ สร้าง error message แทนตาราง
                var errorText = new TextBlock
                {
                    Text = $"ไม่สามารถแสดงข้อมูลหน้า {pageNumber} ได้",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 50, 0, 0)
                };
                stackPanel.Children.Add(errorText);
            }

            // Summary (only on last page)
            if (pageNumber == totalPages)
            {
                var summaryPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

                var summaryText = $"จำนวนรายการทั้งหมด: {currentViewModel.TotalItems:N0} รายการ | " +
                                  $"ยอดรวมทั้งหมด: {currentViewModel.AllResults.Sum(x => x.TotalPrice):N2} บาท";

                summaryPanel.Children.Add(new TextBlock
                {
                    Text = summaryText,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    TextWrapping = TextWrapping.Wrap
                });

                stackPanel.Children.Add(summaryPanel);
            }
    
            page.Children.Add(stackPanel);
            return page;
        }

        // Method สำหรับรายงานแบบ ByReceipt (โค้ดเดิม)
        private static Grid CreateByReceiptTable(int pageNumber)
        {
            if (currentViewModel == null) return new Grid();

            var table = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = double.NaN
            };

            // 12 columns to match XAML: Date, ReceiptCode, Customer, Phone, SalesPerson, PaymentMethod,
            // PaidCount, FreeCount, TotalCount, FreeCouponPrice, PaidCouponPrice, GrandTotal
            var columnWidths = new[] { 1.0, 1.2, 2.0, 1.2, 1.2, 1.2, 0.9, 0.9, 0.9, 1.0, 1.0, 1.2 };
            foreach (var w in columnWidths)
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });

            var headers = new[]
            {
                "วันที่", "เลขที่ใบเสร็จ", "ลูกค้า", "เบอร์โทร", "เซล", "การชำระเงิน",
                "จำนวนคูปองที่จ่าย", "จำนวนคูปองฟรี", "จำนวนทั้งหมด", "ราคาคูปองฟรี", "ราคาที่จ่าย", "มูลค่ารวม"
            };
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0.6),
                    Child = new TextBlock
                    {
                        Text = headers[i],
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 9,
                        Margin = new Thickness(2, 2, 2, 2),
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                Grid.SetColumn(headerCell, i);
                Grid.SetRow(headerCell, 0);
                table.Children.Add(headerCell);
            }

            var startIndex = (pageNumber - 1) * currentItemsPerPage;
            var endIndex = Math.Min(startIndex + currentItemsPerPage, currentViewModel.TotalItems);
            var all = currentViewModel.AllResults;

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = all[i];
                var rowIndex = i - startIndex + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

                var rowData = new[]
                {
                    item.ReceiptDate.ToString("dd/MM/yy"),
                    item.ReceiptCode ?? "",
                    item.CustomerName ?? "",
                    item.CustomerPhone ?? "",
                    item.SalesPersonName ?? "",
                    item.PaymentMethodName ?? "",
                    item.PaidCouponCount.ToString(),
                    item.FreeCouponCount.ToString(),
                    item.TotalCouponCount.ToString(),
                    item.FreeCouponPrice.ToString("N2"),
                    item.PaidCouponPrice.ToString("N2"),
                    item.GrandTotalPrice.ToString("N2")
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
                            FontSize = 8,
                            Margin = new Thickness(4, 2, 4, 2),
                            TextAlignment = (j >= 6) ? TextAlignment.Right : TextAlignment.Left,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            TextTrimming = TextTrimming.None
                        }
                    };
                    Grid.SetColumn(cell, j);
                    Grid.SetRow(cell, rowIndex);
                    table.Children.Add(cell);
                }
            }

            table.Width = 794 - 40;
            return table;
        }

        // Method สำหรับรายงาน SummaryByCoupon
        private static Grid CreateSummaryByCouponTable(int pageNumber)
        {
            if (currentViewModel == null) return new Grid();

            Debug.WriteLine($"CreateSummaryByCouponTable page {pageNumber}");

            var table = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = double.NaN
            };

            // Match XAML: Coupon, Branch, SaleEvent, IsLimited, SoldCount, FreeCount, TotalCount, PaidAmount, FreeAmount, GrandTotal
            var columnWidths = new[] { 2.5, 1.0, 1.4, 1.0, 1.0, 1.0, 1.0, 1.2, 1.2, 1.5 };
            foreach (var width in columnWidths)
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });

            var headers = new[]
            {
                "คูปอง", "สาขา", "งานที่ออกขาย", "จำกัด/ไม่จำกัด",
                "จำนวนคูปองที่ขาย", "จำนวนคูปองที่ฟรี", "จำนวนคูปองทั้งหมด",
                "ราคาที่ขายได้", "ราคาของคูปองฟรี", "มูลค่ารวม"
            };
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0.6),
                    Child = new TextBlock
                    {
                        Text = headers[i],
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 9,
                        Margin = new Thickness(2, 2, 2, 2),
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                Grid.SetColumn(headerCell, i);
                Grid.SetRow(headerCell, 0);
                table.Children.Add(headerCell);
            }

            // Use currentItemsPerPage for pagination
            var startIndex = (pageNumber - 1) * currentItemsPerPage;
            var endIndex = Math.Min(startIndex + currentItemsPerPage, currentViewModel.TotalItems);
            var all = currentViewModel.AllResults;

            Debug.WriteLine($"SummaryByCoupon start={startIndex} end={endIndex} totalItems={currentViewModel.TotalItems}");

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = all[i];
                var rowIndex = i - startIndex + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

                var rowData = new[]
                {
                    item.CouponName ?? "",
                    item.BranchTypeName ?? "",
                    item.SaleEventName ?? "",
                    item.IsLimitedDisplay,
                    item.Quantity.ToString(),                 // sold count
                    item.FreeCouponCount.ToString(),          // free count
                    item.TotalCouponCount.ToString(),         // total count
                    item.PaidCouponPrice.ToString("N2"),      // paid amount
                    item.FreeCouponPrice.ToString("N2"),      // free amount
                    item.TotalPrice.ToString("N2")            // grand total (paid + free)
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
                            FontSize = 8,
                            Margin = new Thickness(4, 2, 4, 2),
                            TextAlignment = (j >= 4) ? TextAlignment.Right : TextAlignment.Left, // numeric columns right-aligned
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            TextTrimming = TextTrimming.None
                        }
                    };
                    Grid.SetColumn(cell, j);
                    Grid.SetRow(cell, rowIndex);
                    table.Children.Add(cell);
                }
            }

            table.Width = 794 - 40;
            return table;
        }

        // Method สำหรับรายงาน RemainingCoupons
        private static Grid CreateRemainingCouponsTable(int pageNumber)
        {
            if (currentViewModel == null) return new Grid();
            Debug.WriteLine($"CreateRemainingCouponsTable page {pageNumber}");

            var table = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, Width = double.NaN };
            var columnWidths = new[] { 1.0, 2.5, 1.5, 1.0, 1.0, 1.0, 1.0 };
            foreach (var width in columnWidths)
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });

            var headers = new[] { "รหัส", "ชื่อคูปอง", "สาขา", "จำนวนรวม", "ขายแล้ว", "คงเหลือ", "ราคา/ใบ" };
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0.6),
                    Child = new TextBlock
                    {
                        Text = headers[i],
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 9,
                        Margin = new Thickness(2),
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                    }
                };
                Grid.SetColumn(headerCell, i); Grid.SetRow(headerCell, 0); table.Children.Add(headerCell);
            }

            var startIndex = (pageNumber - 1) * currentItemsPerPage;
            var endIndex = Math.Min(startIndex + currentItemsPerPage, currentViewModel.TotalItems);
            var all = currentViewModel.AllResults;
            Debug.WriteLine($"RemainingCoupons start={startIndex} end={endIndex} totalItems={currentViewModel.TotalItems}");

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = all[i];
                var rowIndex = i - startIndex + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

                var rowData = new[]
                {
                    item.CouponCode ?? "",
                    item.CouponName ?? "",
                    item.BranchTypeName ?? "",
                    item.TotalQuantity.ToString(),
                    item.SoldQuantity.ToString(),
                    item.RemainingQuantity.ToString(),
                    item.UnitPrice.ToString("N2")
                };

                for (int j = 0; j < rowData.Length; j++)
                {
                    var foregroundColor = Microsoft.UI.Colors.Black;
                    if (j == 5 && item.TotalQuantity > 0)
                    {
                        var percentage = (double)item.RemainingQuantity / item.TotalQuantity * 100;
                        foregroundColor = percentage <= 10 ? Microsoft.UI.Colors.Red : percentage <= 30 ? Microsoft.UI.Colors.Orange : Microsoft.UI.Colors.Green;
                    }

                    var cell = new Border
                    {
                        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        BorderThickness = new Thickness(0.5),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                        Child = new TextBlock
                        {
                            Text = rowData[j],
                            FontSize = 8,
                            Margin = new Thickness(4, 2, 4, 2),
                            TextAlignment = (j >= 3) ? TextAlignment.Right : TextAlignment.Left,
                            Foreground = new SolidColorBrush(foregroundColor),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        }
                    };
                    Grid.SetColumn(cell, j); Grid.SetRow(cell, rowIndex); table.Children.Add(cell);
                }
            }

            // ⭐ เพิ่ม footnote ที่ท้ายตาราง (เฉพาะหน้าแรก)
            if (pageNumber == 1)
            {
                var footerRowIndex = endIndex - startIndex + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var footerCell = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Child = new TextBlock
                    {
                        Text = $"* ขายแล้ว = จำนวนที่ขายในช่วงวันที่ {currentViewModel?.StartDate?.ToString("dd/MM/yyyy")} - {currentViewModel?.EndDate?.ToString("dd/MM/yyyy")}",
                        FontSize = 7,
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        Margin = new Thickness(4, 4, 4, 2),
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                Grid.SetColumn(footerCell, 0);
                Grid.SetColumnSpan(footerCell, 7);
                Grid.SetRow(footerCell, footerRowIndex);
                table.Children.Add(footerCell);
            }

            table.Width = 794 - 40;
            return table;
        }

        // Method สำหรับรายงาน CancelledReceipts
        private static Grid CreateCancelledReceiptsTable(int pageNumber)
        {
            if (currentViewModel == null) return new Grid();

            var table = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, Width = double.NaN };

            // Use 12 columns to match ByReceipt/CancelledReceipts XAML
            var columnWidths = new[] { 1.0, 1.2, 1.0, 2.0, 1.2, 1.2, 1.2, 0.9, 0.9, 1.0, 1.0, 1.2 };
            foreach (var w in columnWidths)
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });

            var headers = new[]
            {
                "วันที่", "เลขที่ใบเสร็จ", "สถานะ", "ลูกค้า", "เบอร์โทร", "เซล",
                "การชำระเงิน", "จำนวนคูปองที่จ่าย", "จำนวนคูปองฟรี", "ราคาคูปองฟรี", "ราคาที่จ่าย", "มูลค่ารวม"
            };
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0.6),
                    Child = new TextBlock
                    {
                        Text = headers[i],
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 9,
                        Margin = new Thickness(2),
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black) // <- ensure black text
                    }
                };
                Grid.SetColumn(headerCell, i); Grid.SetRow(headerCell, 0); table.Children.Add(headerCell);
            }

            var startIndex = (pageNumber - 1) * currentItemsPerPage;
            var endIndex = Math.Min(startIndex + currentItemsPerPage, currentViewModel.TotalItems);
            var all = currentViewModel.AllResults;

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = all[i];
                var rowIndex = i - startIndex + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

                var rowData = new[]
                {
                    item.ReceiptDate.ToString("dd/MM/yy"),
                    item.ReceiptCode ?? "",
                    item.ReceiptStatusDisplay,
                    item.CustomerName ?? "",
                    item.CustomerPhone ?? "",
                    item.SalesPersonName ?? "",
                    item.PaymentMethodName ?? "",
                    item.PaidCouponCount.ToString(),
                    item.FreeCouponCount.ToString(),
                    item.FreeCouponPrice.ToString("N2"),
                    item.PaidCouponPrice.ToString("N2"),
                    item.GrandTotalPrice.ToString("N2")
                };

                for (int j = 0; j < rowData.Length; j++)
                {
                    var foregroundColor = (j == 2) ? (item.ReceiptStatus == "Cancelled" ? Microsoft.UI.Colors.Red : Microsoft.UI.Colors.Green) : Microsoft.UI.Colors.Black;
                    var cell = new Border
                    {
                        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        BorderThickness = new Thickness(0.5),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                        Child = new TextBlock
                        {
                            Text = rowData[j],
                            FontSize = 8,
                            Margin = new Thickness(4, 2, 4, 2),
                            TextAlignment = (j >= 7) ? TextAlignment.Right : TextAlignment.Left,
                            Foreground = new SolidColorBrush(foregroundColor),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        }
                    };
                    Grid.SetColumn(cell, j); Grid.SetRow(cell, rowIndex); table.Children.Add(cell);
                }
            }

            table.Width = 794 - 40;
            return table;
        }

        // Method สำหรับรายงาน CancelledCoupons
        private static Grid CreateCancelledCouponsTable(int pageNumber)
        {
            if (currentViewModel == null) return new Grid();
            Debug.WriteLine($"CreateCancelledCouponsTable page {pageNumber}");

            var table = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, Width = double.NaN };
            var columnWidths = new[] { 0.8, 1.0, 0.7, 1.8, 1.2, 1.0, 1.0, 1.0, 0.8 };
            foreach (var width in columnWidths)
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });

            var headers = new[] { "วันที่", "ใบเสร็จ", "สถานะ", "คูปอง", "ลูกค้า", "เบอร์โทร", "เซล", "หมดอายุ", "ราคา" };
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0.6),
                    Child = new TextBlock
                    {
                        Text = headers[i],
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 9,
                        Margin = new Thickness(2, 2, 2, 2),
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                Grid.SetColumn(headerCell, i);
                Grid.SetRow(headerCell, 0);
                table.Children.Add(headerCell);
            }

            // ✅ ใช้ currentItemsPerPage แทนค่าคงที่
            var startIndex = (pageNumber - 1) * currentItemsPerPage;
            var endIndex = Math.Min(startIndex + currentItemsPerPage, currentViewModel.TotalItems);
            var all = currentViewModel.AllResults;

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = all[i];
                var rowIndex = i - startIndex + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

                var rowData = new[]
                {
                    item.ReceiptDate.ToString("dd/MM/yy"),
                    item.ReceiptCode,
                    item.ReceiptStatusDisplay,
                    OptimizedTruncateString(item.CouponName ?? "", 30),
                    OptimizedTruncateString(item.CustomerName ?? "", 20),
                    item.CustomerPhone ?? "",
                    OptimizedTruncateString(item.SalesPersonName ?? "", 15),
                    item.ExpiresAtDisplay,
                    item.TotalPrice.ToString("N2")
                };

                for (int j = 0; j < rowData.Length; j++)
                {
                    var foregroundColor = Microsoft.UI.Colors.Black;
                    if (j == 2)
                        foregroundColor = item.ReceiptStatus == "Cancelled" ? Microsoft.UI.Colors.Red : Microsoft.UI.Colors.Green;

                    var cell = new Border
                    {
                        BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        BorderThickness = new Thickness(0.5),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                        Child = new TextBlock
                        {
                            Text = rowData[j],
                            FontSize = 8,
                            Margin = new Thickness(4, 2, 4, 2),
                            TextAlignment = (j >= 8) ? TextAlignment.Right : TextAlignment.Left,
                            Foreground = new SolidColorBrush(foregroundColor),
                            FontWeight = (j == 2) ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            TextTrimming = TextTrimming.None
                        }
                    };

                    Grid.SetColumn(cell, j);
                    Grid.SetRow(cell, rowIndex);
                    table.Children.Add(cell);
                }
            }

            var availableWidth = 794 - 40;
            table.Width = availableWidth;

            return table;
        }

        // Method สำหรับรายงาน LimitedCoupons
        private static Grid CreateLimitedCouponsTable(int pageNumber)
        {
            if (currentViewModel == null) return new Grid();

            var table = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, Width = double.NaN };

            // 10 columns: Date, Receipt, Code, Coupon, Customer, Phone, SalesPerson, Expires, Price, SaleEvent
            var columnWidths = new[] { 0.8, 1.0, 1.0, 1.6, 1.2, 1.0, 1.0, 1.0, 0.9, 1.2 };
            foreach (var width in columnWidths)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });

            var headers = new[] { "วันที่", "เลขที่ใบเสร็จ", "รหัสคูปอง", "คูปอง", "ลูกค้า", "เบอร์โทร", "เซล", "วันหมดอายุ", "ราคา", "งานที่ออกขาย" };
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0.6),
                    Child = new TextBlock
                    {
                        Text = headers[i],
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 9,
                        Margin = new Thickness(2, 2, 2, 2),
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                Grid.SetColumn(headerCell, i);
                Grid.SetRow(headerCell, 0);
                table.Children.Add(headerCell);
            }

            var startIndex = (pageNumber - 1) * currentItemsPerPage;
            var endIndex = Math.Min(startIndex + currentItemsPerPage, currentViewModel.TotalItems);
            var all = currentViewModel.AllResults;

            if (startIndex >= all.Count) return table;

            for (int i = startIndex; i < endIndex; i++)
            {
                if (i >= all.Count) break;
                var item = all[i];
                var rowIndex = i - startIndex + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

                var rowData = new[]
                {
                    item.ReceiptDate.ToString("dd/MM/yy"),
                    item.ReceiptCode ?? "",
                    item.GeneratedCode ?? "",
                    OptimizedTruncateString(item.CouponName ?? "", 25),
                    OptimizedTruncateString(item.CustomerName ?? "", 20),
                    item.CustomerPhone ?? "",
                    OptimizedTruncateString(item.SalesPersonName ?? "", 15),
                    item.ExpiresAtDisplay,
                    item.TotalPrice.ToString("N2"),
                    item.SaleEventName ?? ""
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
                            FontSize = 8,
                            Margin = new Thickness(4, 2, 4, 2),
                            TextAlignment = (j >= 8) ? TextAlignment.Right : TextAlignment.Left,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            TextTrimming = TextTrimming.None
                        }
                    };
                    Grid.SetColumn(cell, j);
                    Grid.SetRow(cell, rowIndex);
                    table.Children.Add(cell);
                }
            }

            table.Width = 794 - 40;
            return table;
        }

        // Method สำหรับรายงาน UnlimitedGrouped  
        private static Grid CreateUnlimitedGroupedTable(int pageNumber)
        {
            if (currentViewModel == null) return new Grid();

            Debug.WriteLine($"CreateUnlimitedGroupedTable: page={pageNumber}"); // ✅ เพิ่ม log

            var table = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = double.NaN
            };

            var columnWidths = new[] { 0.8, 1.0, 1.5, 1.2, 1.0, 1.0, 1.0, 0.6, 0.8 };
            foreach (var width in columnWidths)
            {
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
            }

            var headers = new[] { "วันที่", "ใบเสร็จ", "คูปอง", "ลูกค้า", "เบอร์โทร", "เซล", "หมดอายุ", "จำนวน", "รวม" };
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = new Border
                {
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 220, 220)),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0.6),
                    Child = new TextBlock
                    {
                        Text = headers[i],
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        FontSize = 9,
                        Margin = new Thickness(2, 2, 2, 2),
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        TextWrapping = TextWrapping.Wrap
                    }
                };
                Grid.SetColumn(headerCell, i);
                Grid.SetRow(headerCell, 0);
                table.Children.Add(headerCell);
            }

            // ✅ ใช้ currentItemsPerPage แทนค่าคงที่
            var startIndex = (pageNumber - 1) * currentItemsPerPage;
            var endIndex = Math.Min(startIndex + currentItemsPerPage, currentViewModel.TotalItems);
            var all = currentViewModel.AllResults;

            Debug.WriteLine($"UnlimitedGrouped: start={startIndex} end={endIndex} total={currentViewModel.TotalItems} allResults.Count={all.Count}"); // ✅ เพิ่ม log

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = all[i];
                var rowIndex = i - startIndex + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

                var rowData = new[] {
                    item.ReceiptDate.ToString("dd/MM/yy"),
                    item.ReceiptCode,
                    OptimizedTruncateString(item.CouponName ?? "", 25),
                    OptimizedTruncateString(item.CustomerName ?? "", 20),
                    item.CustomerPhone ?? "",
                    OptimizedTruncateString(item.SalesPersonName ?? "", 15),
                    item.ExpiresAtDisplay,
                    item.Quantity.ToString(),  // ✅ ถูกต้อง: ใช้ Quantity สำหรับคูปองไม่จำกัด
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
                            FontSize = 8,
                            Margin = new Thickness(4, 2, 4, 2),
                            TextAlignment = (j >= 7) ? TextAlignment.Right : TextAlignment.Left,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            TextTrimming = TextTrimming.None
                        }
                    };
                    Grid.SetColumn(cell, j);
                    Grid.SetRow(cell, rowIndex);
                    table.Children.Add(cell);
                }
            }

            var availableWidth = 794 - 40;
            table.Width = availableWidth;

            Debug.WriteLine($"UnlimitedGrouped: created table with {table.RowDefinitions.Count - 1} data rows"); // ✅ เพิ่ม log

            return table;
        }

        private static string OptimizedTruncateString(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            if (text.Length <= maxLength)
                return text;

            // ตัดให้เหลือ maxLength - 3 แล้วเติม "..."
            return text.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        // ✅ เปลี่ยนเป็น async method และใช้ DispatcherQueue
        private static async Task CleanupAllResourcesAsync()
        {
            if (App.MainWindowInstance?.DispatcherQueue == null)
            {
                // Fallback to synchronous cleanup if no dispatcher available
                CleanupAllResourcesSync();
                return;
            }

            var tcs = new TaskCompletionSource<bool>();

            App.MainWindowInstance.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    CleanupAllResourcesSync();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ข้อผิดพลาดใน cleanup: {ex.Message}");
                    tcs.SetResult(false);
                }
            });

            await tcs.Task;
        }

        // ✅ แยก cleanup logic ออกมาเป็น sync method
        private static void CleanupAllResourcesSync()
        {
            try
            {
                Debug.WriteLine("เริ่มทำความสะอาด resources ทั้งหมด");

                // Unsubscribe events first (ป้องกัน COMException)
                if (printDocument != null)
                {
                    try
                    {
                        printDocument.Paginate -= PrintDocument_Paginate;
                        printDocument.GetPreviewPage -= PrintDocument_GetPreviewPage;
                        printDocument.AddPages -= PrintDocument_AddPages;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ข้อผิดพลาดใน unsubscribe printDocument events: {ex.Message}");
                    }
                    printDocument = null;
                }

                if (printManager != null)
                {
                    try
                    {
                        printManager.PrintTaskRequested -= PrintManager_PrintTaskRequestedHandler;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ข้อผิดพลาดใน unsubscribe printManager events: {ex.Message}");
                    }
                    printManager = null;
                }

                printDocumentSource = null;

                // Clear print pages
                if (printPages != null && printPages.Count > 0)
                {
                    printPages.Clear();
                    Debug.WriteLine("ล้าง printPages เรียบร้อย");
                }

                currentViewModel = null;

                Debug.WriteLine("ทำความสะอาด resources ทั้งหมดเสร็จสมบูรณ์");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการทำความสะอาดทั้งหมด: {ex.Message}");
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