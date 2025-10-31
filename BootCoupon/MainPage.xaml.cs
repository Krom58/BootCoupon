using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using Microsoft.Data.SqlClient; // added for direct DB access
using System.Data;

namespace BootCoupon
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            // ลบการตั้งค่า EPPlus license ออก
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

        // อัปเดต event handler สำหรับปุ่มรายงานการขาย
        private void SalesReportButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(SalesReportPage));
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

                    // Load canceled numbers robustly (DB schema may differ)
                    List<string> canceledNumbers = new List<string>();
                    try
                    {
                        canceledNumbers = await GetCanceledReceiptCodesAsync(context);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to load canceled numbers via fallback reader: {ex.Message}");
                        // final fallback to EF (may still throw) - swallow and continue with empty list
                        try
                        {
                            canceledNumbers = await context.CanceledReceiptNumbers
                                .OrderBy(c => c.CanceledDate)
                                .Select(c => c.ReceiptCode)
                                .ToListAsync();
                        }
                        catch (Exception efEx)
                        {
                            Debug.WriteLine($"EF load also failed: {efEx.Message}");
                            canceledNumbers = new List<string>();
                        }
                    }

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
        
        // Helper to read canceled receipt codes from DB handling different schema versions
        private async Task<List<string>> GetCanceledReceiptCodesAsync(CouponContext context)
        {
            var codes = new List<string>();
            var conn = context.Database.GetDbConnection();
            try
            {
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

                // determine columns present
                var columns = new List<string>();
                using (var colCmd = conn.CreateCommand())
                {
                    colCmd.CommandText = @"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CanceledReceiptNumbers'";
                    using var reader = await colCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        columns.Add(reader.GetString(0));
                    }
                }

                bool hasReceiptCode = columns.Contains("ReceiptCode", StringComparer.OrdinalIgnoreCase);
                bool hasCanceledDate = columns.Contains("CanceledDate", StringComparer.OrdinalIgnoreCase);
                bool hasNumber = columns.Contains("Number", StringComparer.OrdinalIgnoreCase);
                bool hasCreatedAt = columns.Contains("CreatedAt", StringComparer.OrdinalIgnoreCase) || columns.Contains("CreatedAtUtc", StringComparer.OrdinalIgnoreCase) || columns.Contains("CreatedAtUtc", StringComparer.OrdinalIgnoreCase);

                string selectSql = null!;
                if (hasReceiptCode && hasCanceledDate)
                {
                    selectSql = "SELECT ReceiptCode AS Code FROM dbo.CanceledReceiptNumbers ORDER BY CanceledDate";
                }
                else if (hasNumber && hasCreatedAt)
                {
                    selectSql = "SELECT Number AS Code FROM dbo.CanceledReceiptNumbers ORDER BY CreatedAt";
                }
                else if (hasReceiptCode)
                {
                    selectSql = "SELECT ReceiptCode AS Code FROM dbo.CanceledReceiptNumbers ORDER BY Id";
                }
                else if (hasNumber)
                {
                    selectSql = "SELECT Number AS Code FROM dbo.CanceledReceiptNumbers ORDER BY Id";
                }
                else
                {
                    // no recognizable columns
                    return codes;
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = selectSql;
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (!reader.IsDBNull(0)) codes.Add(reader.GetString(0));
                    }
                }

                return codes;
            }
            finally
            {
                try { if (conn.State == ConnectionState.Open) conn.Close(); } catch { }
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
