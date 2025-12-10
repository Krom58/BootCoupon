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
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object? sender, RoutedEventArgs e)
        {
            // ตรวจสอบว่า login แล้วหรือยัง
            if (App.IsAuthenticated)
            {
                EnableMainFunctions();
            }
            else
            {
                // ถ้ายังไม่ได้ login ให้แสดง login dialog
                await PromptLoginAsync();
            }
        }

        private async Task PromptLoginAsync()
        {
            // Loop until successful login or user cancels
            while (true)
            {
                var usernameBox = new TextBox
                {
                    PlaceholderText = "ชื่อผู้ใช้",
                    Width = 320
                };
                var passwordBox = new PasswordBox
                {
                    PlaceholderText = "รหัสผ่าน",
                    Width = 320
                };

                var panel = new StackPanel { Spacing = 8 };
                panel.Children.Add(new TextBlock { Text = "กรุณาเข้าสู่ระบบ", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
                panel.Children.Add(new TextBlock { Text = "ชื่อผู้ใช้:" });
                panel.Children.Add(usernameBox);
                panel.Children.Add(new TextBlock { Text = "รหัสผ่าน:" });
                panel.Children.Add(passwordBox);

                var loginDialog = new ContentDialog
                {
                    Title = "เข้าสู่ระบบ",
                    Content = panel,
                    PrimaryButtonText = "เข้าสู่ระบบ",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result = await loginDialog.ShowAsync();

                if (result != ContentDialogResult.Primary)
                {
                    // user cancelled - leave app in locked state
                    await ShowErrorDialog("ยังไม่ได้เข้าสู่ระบบ ระบบจะถูกล็อคจนกว่าจะเข้าสู่ระบบ");

                    // Log cancellation
                    try
                    {
                        using (var logContext = new CouponContext())
                        {
                            string attemptedUser = usernameBox.Text?.Trim() ?? "(no user)";
                            string appName = GetAppName();
                            await LogLoginAttemptAsync(logContext, attemptedUser, "Login Cancelled", "LoginDialog - Cancel", Environment.MachineName, appName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to log login cancellation: {ex}");
                    }

                    break;
                }

                string username = usernameBox.Text?.Trim() ?? string.Empty;
                string password = passwordBox.Password ?? string.Empty;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    await ShowErrorDialog("กรุณากรอกชื่อผู้ใช้และรหัสผ่าน");
                    // Log missing credentials
                    try
                    {
                        using (var logContext = new CouponContext())
                        {
                            string appName = GetAppName();
                            await LogLoginAttemptAsync(logContext, username, "Login Failed - missing credentials", "LoginDialog - MissingCredentials", Environment.MachineName, appName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to log missing credentials: {ex}");
                    }
                    continue;
                }

                try
                {
                    using (var context = new CouponContext())
                    {
                        // Ensure database exists and is accessible
                        await context.Database.EnsureCreatedAsync();

                        // Find user by username (case-insensitive) and check password
                        var user = await context.ApplicationUsers
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

                        if (user == null)
                        {
                            // Log attempt
                            string appName = GetAppName();
                            await LogLoginAttemptAsync(context, username, "Login Failed - user not found", "PromptLogin - UserLookup", Environment.MachineName, appName);

                            await ShowErrorDialog("ไม่พบผู้ใช้ดังกล่าว");
                            continue;
                        }

                        if (!user.IsActive)
                        {
                            // Log attempt
                            string appName = GetAppName();
                            await LogLoginAttemptAsync(context, username, "Login Failed - account inactive", "PromptLogin - CheckIsActive", Environment.MachineName, appName);

                            await ShowErrorDialog("บัญชีนี้ถูกปิดใช้งาน กรุณาติดต่อผู้ดูแลระบบ");
                            continue;
                        }

                        // For now passwords are stored in plain text per project note
                        if (user.Password != password)
                        {
                            // Log attempt
                            string appName = GetAppName();
                            await LogLoginAttemptAsync(context, username, "Login Failed - wrong password", "PromptLogin - PasswordCheck", Environment.MachineName, appName);

                            await ShowErrorDialog("รหัสผ่านไม่ถูกต้อง");
                            continue;
                        }

                        // Success - เก็บ session ไว้ที่ App level
                        App.CurrentUser = user;

                        // Log success
                        string appSuccess = GetAppName();
                        await LogLoginAttemptAsync(context, user.Username, "Login Successful", "PromptLogin - Success", Environment.MachineName, appSuccess);

                        EnableMainFunctions();
                        await ShowSuccessDialog($"ยินดีต้อนรับ {user.DisplayName ?? user.Username}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Login error: {ex}");
                    await ShowErrorDialog($"เกิดข้อผิดพลาดขณะตรวจสอบข้อมูลผู้ใช้: {ex.Message}");
                    // Optionally break to avoid infinite loop on DB failure
                    break;
                }
            }
        }

        private void EnableMainFunctions()
        {
            CreateReceiptButton.IsEnabled = true;
            ReprintReceiptButton.IsEnabled = true;
            SalesReportButton.IsEnabled = true;
            SettingsButton.IsEnabled = true;
        }

        private void CreateReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(Receipt));
        }
        
        private void ReprintReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(ReprintReceiptPage));
        }

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
                    await context.Database.EnsureCreatedAsync();
                    
                    var numberManager = await context.ReceiptNumberManagers.FirstOrDefaultAsync();

                    List<string> canceledNumbers = new List<string>();
                    try
                    {
                        canceledNumbers = await GetCanceledReceiptCodesAsync(context);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to load canceled numbers via fallback reader: {ex.Message}");
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
                            CurrentNumber = 1,
                            YearCode = DateTime.Now.Year % 100, // เพิ่ม YearCode
                            UpdatedBy = Environment.MachineName
                        };
                        context.ReceiptNumberManagers.Add(numberManager);
                        await context.SaveChangesAsync();
                    }

                    var settingsPanel = new StackPanel { Spacing = 10 };

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
                        PlaceholderText = "เช่น INV, RC",
                        Width = 200,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        MaxLength = 10
                    };
                    settingsPanel.Children.Add(prefixTextBox);

                    // หมายเลขถัดไป
                    settingsPanel.Children.Add(new TextBlock 
                    { 
                        Text = "หมายเลขถัดไป (3 หลัก):",
                        Margin = new Thickness(0, 10, 0, 0)
                    });
                    var numberTextBox = new TextBox 
                    { 
                        Text = numberManager.CurrentNumber.ToString(),
                        PlaceholderText = "เช่น 1, 2, 999",
                        Width = 200,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    settingsPanel.Children.Add(numberTextBox);

                    // *** ตัวอย่างรหัสใหม่: Prefix + YY + NNN ***
                    var currentYearCode = DateTime.Now.Year % 100;
                    var previewTextBlock = new TextBlock 
                    { 
                        Text = $"ตัวอย่าง: {numberManager.Prefix}{currentYearCode:D2}{numberManager.CurrentNumber:D3}",
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        Margin = new Thickness(0, 5, 0, 10),
                        Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green),
                        FontSize = 14,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    };
                    settingsPanel.Children.Add(previewTextBlock);

                    // อัปเดตตัวอย่างเมื่อเปลี่ยนค่า
                    prefixTextBox.TextChanged += (s, args) => {
                        var prefix = prefixTextBox.Text;
                        var number = int.TryParse(numberTextBox.Text, out int num) ? num : numberManager.CurrentNumber;
                        previewTextBlock.Text = $"ตัวอย่าง: {prefix}{currentYearCode:D2}{number:D3}";
                    };

                    numberTextBox.TextChanged += (s, args) => {
                        var prefix = prefixTextBox.Text;
                        var number = int.TryParse(numberTextBox.Text, out int num) ? num : numberManager.CurrentNumber;
                        previewTextBlock.Text = $"ตัวอย่าง: {prefix}{currentYearCode:D2}{number:D3}";
                    };

                    var separator = new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    settingsPanel.Children.Add(separator);

                    // ข้อมูลปัจจุบัน
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

                    // *** เพิ่มปุ่ม "กลับ" ***
                    var dialog = new ContentDialog
                    {
                        Title = "ตั้งค่าระบบ",
                        Content = settingsPanel,
                        PrimaryButtonText = "บันทึก",
                        SecondaryButtonText = "กลับ", // ✅ เพิ่มปุ่มกลับ
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await dialog.ShowAsync();

                    // ✅ ถ้ากด "กลับ" ให้ return ทันที
                    if (result == ContentDialogResult.Secondary)
                    {
                        return;
                    }

                    if (result == ContentDialogResult.Primary)
                    {
                        string newPrefix = prefixTextBox.Text.Trim();
                        string numberText = numberTextBox.Text.Trim();

                        if (string.IsNullOrWhiteSpace(newPrefix))
                        {
                            await ShowErrorDialog("กรุณากรอกคำนำหน้ารหัสใบเสร็จ");
                            return;
                        }

                        if (!int.TryParse(numberText, out int newNumber) || newNumber < 1)
                        {
                            await ShowErrorDialog("กรุณากรอกหมายเลขที่ถูกต้อง (ต้องเป็นตัวเลข 1-999)");
                            return;
                        }

                        if (newNumber > 999)
                        {
                            await ShowErrorDialog("หมายเลขต้องไม่เกิน 999 (เพื่อให้อยู่ใน 3 หลัก)");
                            return;
                        }

                        try
                        {
                            numberManager.Prefix = newPrefix;
                            numberManager.CurrentNumber = newNumber;
                            numberManager.YearCode = currentYearCode;
                            numberManager.LastUpdated = DateTime.Now;
                            numberManager.UpdatedBy = Environment.MachineName;

                            context.ReceiptNumberManagers.Update(numberManager);
                            await context.SaveChangesAsync();

                            var exampleCode = $"{newPrefix}{currentYearCode:D2}{newNumber:D3}";
                            await ShowSuccessDialog($"บันทึกการตั้งค่าเรียบร้อยแล้ว\n\nรหัสใบเสร็จถัดไป: {exampleCode}");
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
        
        private async Task<List<string>> GetCanceledReceiptCodesAsync(CouponContext context)
        {
            var codes = new List<string>();
            var conn = context.Database.GetDbConnection();
            try
            {
                if (conn.State != ConnectionState.Open) await conn.OpenAsync();

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
                bool hasCreatedAt = columns.Contains("CreatedAt", StringComparer.OrdinalIgnoreCase) || columns.Contains("CreatedAtUtc", StringComparer.OrdinalIgnoreCase);

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

        private async Task LogLoginAttemptAsync(CouponContext context, String userName, String action, String? location = null, String? ownerMachineId = null, String? appName = null)
        {
            try
            {
                DateTime loggedAt = DateTime.Now;
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO dbo.[Log] (LoggedAt, UserName, Action, Location, OwnerMachineId, App) VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
                    loggedAt, userName ?? string.Empty, action ?? string.Empty, location ?? string.Empty, ownerMachineId ?? string.Empty, appName ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write login log: {ex}");
            }
        }

        private static string GetAppName()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetEntryAssembly() ?? typeof(App).Assembly;
                return asm.GetName().Name ?? "BootCoupon";
            }
            catch
            {
                return "BootCoupon";
            }
        }
    }
}
