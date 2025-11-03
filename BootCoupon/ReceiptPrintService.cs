using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
using WinRT.Interop;
using Microsoft.UI.Xaml.Printing;
using BootCoupon.Services;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;

namespace BootCoupon
{
    public static class ReceiptPrintService
    {
        private static PrintManager? printManager;
        private static PrintDocument? printDocument;
        private static IPrintDocumentSource? printDocumentSource;
        private static Canvas? printCanvas;
        private static ReceiptModel? currentReceipt;
        private static List<ReceiptItemDisplay> currentItems = new();
        private static string salesPersonName = string.Empty;
        private static string salesPersonPhone = string.Empty;
        private static string selectedPaymentMethod = string.Empty; // เพิ่มในส่วนตัวแปร

        // Dialog management
        private static readonly SemaphoreSlim dialogSemaphore = new(1,1);
        private static bool isPrintingInProgress = false;

        /// <summary>
        /// พิมพ์ใบเสร็จโดยไม่ต้องเปิดหน้า Preview
        /// </summary>
        /// <param name="receiptId">รหัสใบเสร็จ</param>
        /// <param name="xamlRoot">XamlRoot สำหรับแสดง dialog</param>
        /// <returns>true ถ้าพิมพ์สำเร็จ, false ถ้าไม่สำเร็จ</returns>
        public static async Task<bool> PrintReceiptAsync(int receiptId, XamlRoot xamlRoot)
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
                Debug.WriteLine($"เริ่มพิมพ์ใบเสร็จ ID: {receiptId}");

                // โหลดข้อมูลใบเสร็จ
                if (!await LoadReceiptDataAsync(receiptId))
                {
                    await ShowErrorDialogSafe(xamlRoot, "ไม่สามารถโหลดข้อมูลใบเสร็จได้");
                    return false;
                }

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

        /// <summary>
        /// จัดการกับการยกเลิกใบเสร็จ (เหมือนกับปุ่มปิดใน ReceiptPrintPreview)
        /// </summary>
        /// <param name="receiptId">รหัสใบเสร็จ</param>
        /// <param name="xamlRoot">XamlRoot สำหรับแสดง dialog</param>
        public static async Task HandleReceiptCancellationAsync(int receiptId, XamlRoot xamlRoot)
        {
            try
            {
                // โหลดข้อมูลใบเสร็จ
                if (!await LoadReceiptDataAsync(receiptId))
                {
                    return;
                }

                if (currentReceipt != null)
                {
                    await CleanupReceiptDataAsync(xamlRoot);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการยกเลิกใบเสร็จ: {ex.Message}");
                await ShowErrorDialogSafe(xamlRoot, $"เกิดข้อผิดพลาดในการยกเลิกใบเสร็จ: {ex.Message}");
            }
        }

        private static async Task<bool> LoadReceiptDataAsync(int receiptId)
        {
            try
            {
                using (var context = new CouponContext())
                {
                    // ดึงข้อมูลใบเสร็จจากฐานข้อมูล
                    currentReceipt = await context.Receipts.FirstOrDefaultAsync(r => r.ReceiptID == receiptId);

                    if (currentReceipt == null)
                    {
                        Debug.WriteLine($"ไม่พบใบเสร็จ ID: {receiptId}");
                        return false;
                    }

                    // ดึงข้อมูลรายการสินค้า
                    var items = await context.ReceiptItems
                        .Where(ri => ri.ReceiptId == receiptId)
                        .ToListAsync();

                    currentItems.Clear();

                    // สร้างข้อมูลสำหรับแสดงในตาราง - ใช้ CouponDefinition แทน Coupon
                    int displayIndex =0;

                    // Group receipt items by CouponId and UnitPrice to aggregate into single display rows
                    var groups = items.GroupBy(it => new { it.CouponId, it.UnitPrice });

                    foreach (var group in groups)
                    {
                        var groupItems = group.ToList();
                        var couponDefinition = await context.CouponDefinitions.FindAsync(group.Key.CouponId);
                        int totalQuantity = groupItems.Sum(g => g.Quantity);

                        if (couponDefinition != null && couponDefinition.IsLimited)
                        {
                            try
                            {
                                var receiptItemIds = groupItems.Select(g => g.ReceiptItemId).ToList();

                                var codes = await context.GeneratedCoupons
                                    .Where(g => g.ReceiptItemId != null && receiptItemIds.Contains(g.ReceiptItemId.Value))
                                    .OrderBy(g => g.Id)
                                    .Select(g => g.GeneratedCode)
                                    .ToListAsync();

                                if (codes != null && codes.Count >0)
                                {
                                    // New behaviour: concatenate codes into single display entry
                                    displayIndex++;
                                    var codesJoined = string.Join(",", codes);
                                    var nameWithCodes = string.IsNullOrWhiteSpace(codesJoined) ? couponDefinition.Name : $"{couponDefinition.Name} ({codesJoined})";

                                    currentItems.Add(new ReceiptItemDisplay
                                    {
                                        Index = displayIndex,
                                        Name = nameWithCodes,
                                        Quantity = totalQuantity,
                                        UnitPrice = group.Key.UnitPrice,
                                        TotalPrice = group.Key.UnitPrice * totalQuantity
                                    });

                                    // If quantity is larger than number of codes, add an extra hint row
                                    int remaining = totalQuantity - codes.Count;
                                    if (remaining >0)
                                    {
                                        displayIndex++;
                                        currentItems.Add(new ReceiptItemDisplay
                                        {
                                            Index = displayIndex,
                                            Name = $"{couponDefinition.Name} (ยังไม่ระบุรหัสจำนวน {remaining} ใบ)",
                                            Quantity = remaining,
                                            UnitPrice = group.Key.UnitPrice,
                                            TotalPrice = group.Key.UnitPrice * remaining
                                        });
                                    }

                                    continue; // processed this receipt item group
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Failed to load generated codes for ReceiptItem group {group.Key.CouponId}: {ex.Message}");
                                // fall through to add as single row
                            }
                        }

                        // Fallback: non-limited or no codes found -> single row per group
                        displayIndex++;
                        currentItems.Add(new ReceiptItemDisplay
                        {
                            Index = displayIndex,
                            Name = couponDefinition != null ? couponDefinition.Name : $"Coupon #{group.Key.CouponId}",
                            Quantity = totalQuantity,
                            UnitPrice = group.Key.UnitPrice,
                            TotalPrice = group.Key.UnitPrice * totalQuantity
                        });
                    }

                    // โหลดข้อมูล Sales Person
                    await LoadSalesPersonDataAsync(context);

                    // โหลดข้อมูล Payment Method
                    await LoadPaymentMethodDataAsync(context);

                    Debug.WriteLine($"โหลดข้อมูลใบเสร็จ {currentReceipt.ReceiptCode} สำเร็จ");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการโหลดข้อมูล: {ex.Message}");
                return false;
            }
        }

        private static async Task LoadSalesPersonDataAsync(CouponContext context)
        {
            try
            {
                if (currentReceipt?.SalesPersonId.HasValue == true)
                {
                    var salesPerson = await context.SalesPerson
                        .FirstOrDefaultAsync(sp => sp.ID == currentReceipt.SalesPersonId.Value);

                    if (salesPerson != null)
                    {
                        salesPersonName = salesPerson.Name;
                        salesPersonPhone = salesPerson.Telephone;
                        Debug.WriteLine($"โหลดข้อมูล Sales Person: {salesPersonName} ({salesPersonPhone})");
                    }
                }
                else
                {
                    salesPersonName = "ไม่ระบุ";
                    salesPersonPhone = "";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการโหลด Sales Person: {ex.Message}");
                salesPersonName = "ไม่สามารถโหลดข้อมูลได้";
                salesPersonPhone = "";
            }
        }

        private static async Task LoadPaymentMethodDataAsync(CouponContext context) // เพิ่ม method ใหม่
        {
            try
            {
                Debug.WriteLine($"LoadPaymentMethodDataAsync: currentReceipt?.PaymentMethodId = {currentReceipt?.PaymentMethodId}");
                
                if (currentReceipt?.PaymentMethodId.HasValue == true)
                {
                    var paymentMethod = await context.PaymentMethods
                        .FirstOrDefaultAsync(pm => pm.Id == currentReceipt.PaymentMethodId.Value);

                    if (paymentMethod != null)
                    {
                        selectedPaymentMethod = paymentMethod.Name;
                        Debug.WriteLine($"โหลดข้อมูลวิธีการชำระเงิน: '{selectedPaymentMethod}' (ID: {paymentMethod.Id})");
                    }
                    else
                    {
                        selectedPaymentMethod = "ไม่ระบุ";
                        Debug.WriteLine($"ไม่พบข้อมูล PaymentMethod สำหรับ ID: {currentReceipt.PaymentMethodId.Value}");
                    }
                }
                else
                {
                    selectedPaymentMethod = "ไม่ระบุ";
                    Debug.WriteLine("ไม่มี PaymentMethodId ในใบเสร็จ");
                }
                
                Debug.WriteLine($"selectedPaymentMethod ถูกตั้งค่าเป็น: '{selectedPaymentMethod}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการโหลดวิธีการชำระเงิน: {ex.Message}");
                selectedPaymentMethod = "不สามารถโหลดข้อมูลได้";
            }
        }

        private static async Task<bool> InitiatePrintProcessAsync(XamlRoot xamlRoot)
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

                // วิธีใหม่สำหรับ WinUI3 - ใช้ MainWindow instance
                if (App.MainWindowInstance != null)
                {
                    var windowHandle = WindowNative.GetWindowHandle(App.MainWindowInstance);
                    printManager = PrintManagerInterop.GetForWindow(windowHandle);

                    // แก้ไข event handler subscription
                    printManager.PrintTaskRequested -= PrintManager_PrintTaskRequestedHandler;
                    printManager.PrintTaskRequested += PrintManager_PrintTaskRequestedHandler;

                    // แสดง Print UI โดยระบุ window handle - ไปที่ printer dialog โดยตรง
                    await PrintManagerInterop.ShowPrintUIForWindowAsync(windowHandle);

                    Debug.WriteLine("แสดง Print UI เรียบร้อย");
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

                await ShowErrorDialogSafe(xamlRoot,
                    $"ไม่สามารถเข้าถึงเครื่องพิมพ์ได้: {ex.Message}\n\n" +
                    "กรุณาตรวจสอบ:\n" +
                    "- เครื่องพิมพ์ต่ออยู่หรือไม่\n" +
                    "- Driver เครื่องพิมพ์ติดตั้งแล้วหรือไม่\n" +
                    "- Windows Print Service ทำงานหรือไม่");

                return false;
            }
        }

        // แก้ไข event handler ให้ตรงกับ delegate signature
        private static void PrintManager_PrintTaskRequestedHandler(PrintManager sender, PrintTaskRequestedEventArgs args)
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
                    printTask.Completed += PrintTask_CompletedHandler;

                    // ตั้งค่าเพิ่มเติมสำหรับการพิมพ์ A5
                    printTask.Options.MediaSize = PrintMediaSize.IsoA5;
                    printTask.Options.Orientation = PrintOrientation.Portrait;

                    Debug.WriteLine("ตั้งค่า PrintTask สำหรับ A5 เรียบร้อย");
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

            // ตรวจสอบผลลัพธ์ของการพิมพ์
            if (args.Completion == PrintTaskCompletion.Submitted)
            {
                // พิมพ์สำเร็จ
                Debug.WriteLine("การพิมพ์เสร็จสมบูรณ์");

                // แจ้งให้ผู้ใช้ทราบ (บน UI thread) แบบ fire-and-forget
                _ = App.MainWindowInstance?.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (App.MainWindowInstance?.Content is FrameworkElement element)
                        {
                            await ShowSuccessDialogSafe(element.XamlRoot,
                                "ใบเสร็จถูกส่งไปยังเครื่องพิมพ์เรียบร้อยแล้ว");
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
                Debug.WriteLine("การพิมพ์ล้มเหลว");

                _ = App.MainWindowInstance?.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        if (App.MainWindowInstance?.Content is FrameworkElement element)
                        {
                            await ShowErrorDialogSafe(element.XamlRoot,
                                "ไม่สามารถพิมพ์ใบเสร็จได้ กรุณาลองใหม่อีกครั้ง");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ข้อผิดพลาดในการแสดง error dialog: {ex.Message}");
                    }
                });
            }
            else if (args.Completion == PrintTaskCompletion.Canceled)
            {
                Debug.WriteLine("การพิมพ์ถูกยกเลิก");
            }

            // ทำความสะอาด
            CleanupPrintResources();
            isPrintingInProgress = false;
        }

        private static void PrintDocument_Paginate(object sender, PaginateEventArgs e)
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

        private static void PrintDocument_GetPreviewPage(object sender, GetPreviewPageEventArgs e)
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

        private static void PrintDocument_AddPages(object sender, AddPagesEventArgs e)
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

        private static void CreatePrintCanvas()
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

        // สร้างเนื้อหาใบเสร็จ (คัดลอกมาจาก ReceiptPrintPreview)
        private static FrameworkElement? CreateReceiptContent()
        {
            try
            {
                if (currentReceipt == null) return null;

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
                var roomAndSalesSection = CreateRoomAndSalesSection();
                if (roomAndSalesSection != null)
                    mainStackPanel.Children.Add(roomAndSalesSection);

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

        // Helper methods (คัด ลอกมาจาก ReceiptPrintPreview และปรับปรุง)
        private static Grid? CreateHeaderSection()
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
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/AsiaHotelLogo.jpg")),
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
                    Text = "ใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ",
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

        private static Border? CreateCustomerSection()
        {
            try
            {
                if (currentReceipt == null) return null;

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

                // สร้างตารางทั้งหมด 8 ช่อง (2 แถว x 4 คอลัมน์) ก่อน
                for (int row = 0; row < 2; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        // กำหนด BorderThickness สำหรับแต่ละช่อง
                        Thickness borderThickness;

                        if (row == 0) // แถวบน
                        {
                            if (col == 3) // คอลัมน์สุดท้าย
                                borderThickness = new Thickness(0, 0, 0, 1); // เฉพาะเส้นล่าง
                            else
                                borderThickness = new Thickness(0, 0, 1, 1); // เส้นขวาและล่าง
                        }
                        else // แถวล่าง
                        {
                            if (col == 3) // คอลัมน์สุดท้าย
                                borderThickness = new Thickness(0, 0, 0, 0); // ไม่มีเส้น
                            else
                                borderThickness = new Thickness(0, 0, 1, 0); // เฉพาะเส้นขวา
                        }

                        var cellBorder = new Border
                        {
                            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 80, 80, 80)),
                            BorderThickness = borderThickness,
                            Padding = new Thickness(6)
                        };

                        Grid.SetRow(cellBorder, row);
                        Grid.SetColumn(cellBorder, col);
                        customerGrid.Children.Add(cellBorder);
                    }
                }

                // ตอนนี้ใส่ข้อมูลเข้าไปในช่องที่เตรียมไว้
                var thaiCulture = new CultureInfo("th-TH");
                thaiCulture.DateTimeFormat.Calendar = new ThaiBuddhistCalendar();

                // กำหนดข้อมูลและสไตล์สำหรับแต่ละช่อง
                var cellData = new[]
                {
                    // Row 0
                    new { Row = 0, Col = 0, Text = "ชื่อลูกค้า :", IsHeader = true },
                    new { Row = 0, Col = 1, Text = currentReceipt.CustomerName, IsHeader = false },
                    new { Row = 0, Col = 2, Text = "เลขที่ / No.", IsHeader = true },
                    new { Row = 0, Col = 3, Text = currentReceipt.ReceiptCode, IsHeader = false },
                    
                    // Row 1
                    new { Row = 1, Col = 0, Text = "โทรศัพท์ :", IsHeader = true },
                    new { Row = 1, Col = 1, Text = currentReceipt.CustomerPhoneNumber, IsHeader = false },
                    new { Row = 1, Col = 2, Text = "วันที่ / Date", IsHeader = true },
                    new { Row = 1, Col = 3, Text = currentReceipt.ReceiptDate.ToString("dd / MM / yyyy", thaiCulture), IsHeader = false }
                };

                // ใส่ข้อมูลลงในแต่ละช่อง
                foreach (var data in cellData)
                {
                    // หา Border ที่ตำแหน่งนี้
                    var targetBorder = customerGrid.Children
                        .OfType<Border>()
                        .FirstOrDefault(b => Grid.GetRow(b) == data.Row && Grid.GetColumn(b) == data.Col);

                    if (targetBorder != null)
                    {
                        // ตั้งค่าสีพื้นหลังสำหรับ header
                        if (data.IsHeader)
                        {
                            targetBorder.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 230, 242, 255));
                        }

                        // สร้าง TextBlock
                        var textBlock = new TextBlock
                        {
                            Text = data.Text,
                            FontSize = 12,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                        };

                        // ตั้งค่า FontWeight และ Alignment
                        if (data.IsHeader)
                        {
                            textBlock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                        }

                        // ตั้งค่า HorizontalAlignment สำหรับคอลัมน์ขวา
                        if (data.Col == 3)
                        {
                            textBlock.HorizontalAlignment = HorizontalAlignment.Right;
                        }

                        targetBorder.Child = textBlock;
                    }
                }

                customerBorder.Child = customerGrid;
                return customerBorder;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Customer Section: {ex.Message}");
                return null;
            }
        }

        // สร้างส่วนตารางรายการสินค้า
        private static Border? CreateItemsSection()
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
                for (int i = 0; i < currentItems.Count; i++) // แก้ไขจาก _displayItems เป็น currentItems
                {
                    var item = currentItems[i]; // แก้ไขจาก _displayItems เป็น currentItems
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

        // เพิ่ม method CreateItemsHeader
        private static Grid? CreateItemsHeader()
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

        // เพิ่ม method CreateHeaderCell
        private static Border CreateHeaderCell(string text, Thickness borderThickness, double fontSize = 11)
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

        // เพิ่ม method CreateItemRow
        private static Grid? CreateItemRow(ReceiptItemDisplay item)
        {
            try
            {
                var itemGrid = new Grid
                {
                    Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                    // allow height to auto-size for wrapped text
                    // Height =35
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
                // Create name cell with two lines: name (normal) and codes (smaller)
                var nameBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 80, 80, 80)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(5, 2, 0, 2)
                };

                var nameStack = new StackPanel { Orientation = Orientation.Vertical };

                var fullText = item.Name ?? string.Empty;
                string mainName = fullText;
                string codesPart = string.Empty;

                // split on first '('
                int p = fullText.IndexOf('(');
                if (p > 0 && fullText.EndsWith(")"))
                {
                    mainName = fullText.Substring(0, p).Trim();
                    codesPart = fullText.Substring(p).Trim(); // includes parentheses
                }

                var mainTextBlock = new TextBlock
                {
                    Text = mainName,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    TextWrapping = TextWrapping.Wrap
                };

                nameStack.Children.Add(mainTextBlock);

                if (!string.IsNullOrEmpty(codesPart))
                {
                    var codesTextBlock = new TextBlock
                    {
                        Text = codesPart,
                        FontSize = 6,
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                        TextWrapping = TextWrapping.Wrap
                    };
                    nameStack.Children.Add(codesTextBlock);
                }

                nameBorder.Child = nameStack;
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

        // เพิ่ม method CreateItemCell
        private static Border CreateItemCell(string text, TextAlignment textAlignment, Thickness borderThickness, Thickness? margin = null)
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
                Margin = margin ?? new Thickness(0),
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = textBlock;
            return border;
        }

        // เพิ่ม method CreateTotalSection
        private static Grid? CreateTotalSection()
        {
            try
            {
                if (currentReceipt == null) return null;

                // Create a local non-null reference to quiet nullable analysis
                var receipt = currentReceipt!;

                var totalGrid = new Grid();
                //2 columns: left (labels and thai text), right (values)
                totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                totalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) }); // เปลี่ยนจาก 120 เป็น 90 ให้ตรงกับคอลัมน์ "จำนวนเงิน"

                // Three rows for labels/values
                totalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                totalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                totalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Row0 - "รวมทั้งหมด (ก่อนส่วนลด)"
                var beforeBorder = new Border
                {
                    Padding = new Thickness(6),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,80,80,80)),
                    BorderThickness = new Thickness(0,0,0,1)
                };
                var beforeText = new TextBlock
                {
                    Text = "รวมทั้งหมด (ก่อนส่วนลด)",
                    FontSize =12,
                    TextAlignment = TextAlignment.Right,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                };
                beforeBorder.Child = beforeText;
                Grid.SetColumn(beforeBorder,0);
                Grid.SetRow(beforeBorder,0);
                totalGrid.Children.Add(beforeBorder);

                // Row1 - "ส่วนลด"
                var discountBorder = new Border
                {
                    Padding = new Thickness(6),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,80,80,80)),
                    BorderThickness = new Thickness(0,0,0,1)
                };
                var discountText = new TextBlock
                {
                    Text = "ส่วนลด",
                    FontSize =12,
                    TextAlignment = TextAlignment.Right,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                };
                discountBorder.Child = discountText;
                Grid.SetColumn(discountBorder,0);
                Grid.SetRow(discountBorder,1);
                totalGrid.Children.Add(discountBorder);

                // Row2 - bottom: left side contains Thai text (number in words) and the net label on the same row
                var bottomGrid = new Grid();
                bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Thai text on left (may be long)
                var thaiTextBorder = new Border
                {
                    Padding = new Thickness(6),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,80,80,80)),
                    BorderThickness = new Thickness(0,0,0,0)
                };
                var thaiTextBlock = new TextBlock
                {
                    Text = ConvertNumberToThaiText(receipt.TotalAmount), // เปลี่ยนจาก receipt.TotalAmount + receipt.Discount เป็น receipt.TotalAmount
                    FontSize =12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    VerticalAlignment = VerticalAlignment.Center
                };
                thaiTextBorder.Child = thaiTextBlock;
                Grid.SetColumn(thaiTextBorder,0);
                bottomGrid.Children.Add(thaiTextBorder);

                // Net label on right side of bottomGrid
                var netLabelBorder = new Border
                {
                    Padding = new Thickness(6),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0,0,0,0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var netLabelText = new TextBlock
                {
                    Text = "ราคาสุทธิ (รวมภาษีมูลค่าเพิ่ม)",
                    FontSize =12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Right,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    VerticalAlignment = VerticalAlignment.Center
                };
                netLabelBorder.Child = netLabelText;
                Grid.SetColumn(netLabelBorder,1);
                bottomGrid.Children.Add(netLabelBorder);

                Grid.SetColumn(bottomGrid,0);
                Grid.SetRow(bottomGrid,2);
                totalGrid.Children.Add(bottomGrid);

                // Right column - numeric values aligned with the three rows
                decimal netTotal = receipt.TotalAmount; // already net in model
                decimal discount = receipt.Discount;
                decimal beforeTotal = netTotal + discount;

                // ช่อง "รวมทั้งหมด (ก่อนส่วนลด)"
                var rightBorder1 = new Border 
                { 
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black), 
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(0, 0, 5, 0) // เพิ่ม padding ขวา
                };
                var txtBefore = new TextBlock 
                { 
                    Text = beforeTotal.ToString("N2"), 
                    FontSize = 12, 
                    TextAlignment = TextAlignment.Right, 
                    Margin = new Thickness(6), 
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black) // เพิ่มสีดำชัดเจน
                };
                rightBorder1.Child = txtBefore;
                Grid.SetColumn(rightBorder1,1); 
                Grid.SetRow(rightBorder1,0); 
                totalGrid.Children.Add(rightBorder1);

                // ช่อง "ส่วนลด"
                var rightBorder2 = new Border 
                { 
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black), 
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(0, 0, 5, 0) // เพิ่ม padding ขวา
                };
                var txtDiscount = new TextBlock 
                { 
                    Text = discount.ToString("N2"), 
                    FontSize = 12, 
                    TextAlignment = TextAlignment.Right, 
                    Margin = new Thickness(6), 
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black) // เพิ่มสีดำชัดเจน
                };
                rightBorder2.Child = txtDiscount;
                Grid.SetColumn(rightBorder2,1); 
                Grid.SetRow(rightBorder2,1); 
                totalGrid.Children.Add(rightBorder2);

                // ช่อง "ราคาสุทธิ" - กรอบสีแดง
                var rightBorder3 = new Border 
                { 
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Red), 
                    BorderThickness = new Thickness(2), 
                    Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                    Padding = new Thickness(0, 0, 5, 0) // เพิ่ม padding ขวา
                };
                var txtNet = new TextBlock 
                { 
                    Text = netTotal.ToString("N2"), 
                    TextAlignment = TextAlignment.Right, 
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
                    FontSize = 14, 
                    Margin = new Thickness(6), 
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black) // เพิ่มสีดำชัดเจน
                };
                rightBorder3.Child = txtNet;
                Grid.SetColumn(rightBorder3,1); 
                Grid.SetRow(rightBorder3,2); 
                totalGrid.Children.Add(rightBorder3);

                Debug.WriteLine($"สร้าง Total Section: ก่อนส่วนลด={beforeTotal:N2}, ส่วนลด={discount:N2}, สุทธิ={netTotal:N2}");

                return totalGrid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Total Section: {ex.Message}");
                return null;
            }
        }

        private static StackPanel? CreatePaymentSection()
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

                var paymentMethods = new[] { "เงินสด", "เงินโอน/QR", "เครดิตการ์ด"};

                Debug.WriteLine($"กำลังสร้าง Payment Section สำหรับ: '{selectedPaymentMethod}'");

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
                        Margin = new Thickness(0, 0, 6, 0),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.White)
                    };

                    // เปรียบเทียบแบบไม่คำนึงถึงตัวพิมพ์เล็กใหญ่และช่องว่าง
                    string currentMethod = paymentMethods[i].Trim();
                    string selectedMethod = selectedPaymentMethod?.Trim() ?? "";
                    
                    bool isSelected = string.Equals(currentMethod, selectedMethod, StringComparison.OrdinalIgnoreCase);
                    
                    Debug.WriteLine($"เปรียบเทียบ: '{currentMethod}' กับ '{selectedMethod}' = {isSelected}");

                    // เพิ่มเครื่องหมายถูกถ้าตรงกับวิธีการชำระเงินที่เลือก
                    if (isSelected)
                    {
                        checkbox.Child = new TextBlock
                        {
                            Text = "✓",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontSize = 12,
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green)
                        };
                        
                        Debug.WriteLine($"แสดงเครื่องหมายถูกสำหรับ: {currentMethod}");
                    }

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

        private static Grid? CreateRoomAndSalesSection()
        {
            try
            {
                var roomSalesGrid = new Grid
                {
                    Margin = new Thickness(0, 15, 0, 0)
                };

                roomSalesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                roomSalesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

                // ส่วนซ้าย - ข้อมูลห้อง
                var leftStackPanel = new StackPanel();

                // หมายเลขคูปองห้องอาหาร
                var restaurantPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                restaurantPanel.Children.Add(new TextBlock
                {
                    Text = "หมายเลขคูปองห้องอาหาร",
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

                // หมายเลขคูปองห้องพัก
                var roomPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                roomPanel.Children.Add(new TextBlock
                {
                    Text = "หมายเลขคูปองห้องพัก",
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

                Grid.SetColumn(leftStackPanel, 0);
                roomSalesGrid.Children.Add(leftStackPanel);

                // ส่วนขวา - Sales และ ผู้รับเงิน (ตรงกับ XAML)
                var rightStackPanel = new StackPanel();

                // Sales information
                var salesPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                salesPanel.Children.Add(new TextBlock
                {
                    Text = "Sales: ",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                });

                salesPanel.Children.Add(new TextBlock
                {
                    Text = salesPersonName, // แก้ไขจาก SalesPersonName เป็น salesPersonName
                    FontWeight = Microsoft.UI.Text.FontWeights.Normal,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    Margin = new Thickness(5, 0, 0, 0)
                });

                salesPanel.Children.Add(new TextBlock
                {
                    Text = salesPersonPhone, // แก้ไขจาก SalesPersonPhone เป็น salesPersonPhone
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    Margin = new Thickness(10, 0, 0, 0)
                });

                leftStackPanel.Children.Add(salesPanel);

                // ผู้รับเงิน
                var signaturePanel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center
                };

                signaturePanel.Children.Add(new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Black),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Width = 120,
                    Height = 16,
                    HorizontalAlignment = HorizontalAlignment.Right
                });

                signaturePanel.Children.Add(new TextBlock
                {
                    Text = "ผู้รับเงิน",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 0),
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
                });

                rightStackPanel.Children.Add(signaturePanel);

                Grid.SetColumn(rightStackPanel, 1);
                roomSalesGrid.Children.Add(rightStackPanel);

                return roomSalesGrid;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการสร้าง Room and Sales Section: {ex.Message}");
                return null;
            }
        }

        // ลบข้อมูลใบเสร็จเมื่อไม่มีการพิมพ์ (เหมือนกับ ReceiptPrintPreview)
        private static async Task CleanupReceiptDataAsync(XamlRoot xamlRoot)
        {
            try
            {
                if (currentReceipt == null || string.IsNullOrEmpty(currentReceipt.ReceiptCode)) return;

                string receiptCodeToDelete = currentReceipt.ReceiptCode;
                int receiptIdToDelete = currentReceipt.ReceiptID;

                Debug.WriteLine($"เริ่มลบใบเสร็จ {receiptCodeToDelete} (ID: {receiptIdToDelete})");

                using (var context = new CouponContext())
                {
                    // ลบ receipt items ก่อน
                    var receiptItems = await context.ReceiptItems
                        .Where(ri => ri.ReceiptId == receiptIdToDelete)
                        .ToListAsync();

                    if (receiptItems.Any())
                    {
                        // Restore any GeneratedCoupons that were linked to these receipt items
                        try
                        {
                            var receiptItemIds = receiptItems.Select(ri => ri.ReceiptItemId).ToList();

                            var linkedGenerated = await context.GeneratedCoupons
                                .Where(g => g.ReceiptItemId != null && receiptItemIds.Contains(g.ReceiptItemId.Value))
                                .ToListAsync();

                            if (linkedGenerated.Any())
                            {
                                Debug.WriteLine($"พบ GeneratedCoupons ที่เชื่อมกับ ReceiptItems จำนวน {linkedGenerated.Count} – จะคืนสถานะเป็นยังไม่ใช้");

                                foreach (var g in linkedGenerated)
                                {
                                    g.IsUsed = false;
                                    g.UsedDate = null;
                                    g.UsedBy = null;
                                    g.ReceiptItemId = null;
                                    context.GeneratedCoupons.Update(g);
                                }

                                await context.SaveChangesAsync();
                                Debug.WriteLine("คืนสถานะ GeneratedCoupons เรียบร้อย");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to restore GeneratedCoupons for receipt {receiptCodeToDelete}: {ex.Message}");
                            // ไม่หยุดการลบใบเสร็จ แต่บันทึกข้อผิดพลาด
                        }

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

                // ใช้บริการใหม่แทน AppSettings
                await ReceiptNumberService.RecycleReceiptCodeAsync(receiptCodeToDelete, "Print canceled");
                Debug.WriteLine($"เก็บหมายเลข {receiptCodeToDelete} เพื่อนำกลับมาใช้ใหม่");

                currentReceipt = null;
                Debug.WriteLine($"ลบข้อมูลใบเสร็จ {receiptCodeToDelete} เรียบร้อยแล้ว");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up receipt data: {ex.Message}");
                await ShowErrorDialogSafe(xamlRoot, $"ไม่สามารถลบข้อมูลใบเสร็จได้: {ex.Message}");
            }
        }

        private static void CleanupPrintResources()
        {
            try
            {
                if (printManager != null)
                {
                    printManager.PrintTaskRequested -= PrintManager_PrintTaskRequestedHandler; // แก้ไข event handler name
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
                printCanvas = null;

                Debug.WriteLine("ทำความสะอาด Print resources เรียบร้อย");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการทำความสะอาด: {ex.Message}");
            }
        }
        // Safe dialog methods ที่ป้องกัน dialog ซ้อนกัน
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

        // Helper method แปลงตัวเลขเป็นข้อความไทย
        private static string ConvertNumberToThaiText(decimal amount)
        {
            try
            {
                var converter = new ThaiNumberToTextConverter();
                var result = converter.Convert(amount, typeof(string), string.Empty, "th-TH");
                return result?.ToString() ?? $"รวม ({amount:N2} บาท)";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ข้อผิดพลาดในการแปลงตัวเลขเป็นภาษาไทย: {ex.Message}");
                return $"รวม ({amount:N2} บาท)";
            }
        }
    }
}