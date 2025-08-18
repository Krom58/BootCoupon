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
using System.Threading.Tasks; // Make sure this import is present
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;

namespace BootCoupon
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
        }
        
        private void AddCouponButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(AddCoupon));
        }

        private void CreateReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(Receipt));
        }
        
        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Create the settings dialog content
            var settingsPanel = new StackPanel { Spacing = 15, Margin = new Thickness(0, 10, 0, 10) };
            
            settingsPanel.Children.Add(new TextBlock { 
                Text = "ตั้งค่ารหัสใบเสร็จ", 
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 18
            });
            
            // Load current settings
            var settings = await AppSettings.GetSettingsAsync();
            
            // Prefix setting
            settingsPanel.Children.Add(new TextBlock { Text = "คำนำหน้ารหัสใบเสร็จ:" });
            var prefixTextBox = new TextBox { 
                PlaceholderText = "เช่น INV", 
                Text = settings.ReceiptCodePrefix,
                Width = 250, 
                HorizontalAlignment = HorizontalAlignment.Left 
            };
            settingsPanel.Children.Add(prefixTextBox);
            
            // Receipt number setting
            settingsPanel.Children.Add(new TextBlock { Text = "เลขที่เริ่มต้นใบเสร็จ:" });
            var numberTextBox = new TextBox { 
                PlaceholderText = "เช่น 1000", 
                Text = settings.CurrentReceiptNumber.ToString(),
                Width = 250, 
                HorizontalAlignment = HorizontalAlignment.Left 
            };
            settingsPanel.Children.Add(numberTextBox);
            
            // Example of receipt code
            var previewTextBlock = new TextBlock { 
                Text = $"ตัวอย่างรหัสใบเสร็จ: {settings.ReceiptCodePrefix}{settings.CurrentReceiptNumber}",
                Margin = new Thickness(0, 10, 0, 0),
                FontStyle = Windows.UI.Text.FontStyle.Italic
            };
            settingsPanel.Children.Add(previewTextBlock);
            
            // Update preview when inputs change
            prefixTextBox.TextChanged += (s, args) => {
                previewTextBlock.Text = $"ตัวอย่างรหัสใบเสร็จ: {prefixTextBox.Text}{(int.TryParse(numberTextBox.Text, out int num) ? num : settings.CurrentReceiptNumber)}";
            };
            
            numberTextBox.TextChanged += (s, args) => {
                previewTextBlock.Text = $"ตัวอย่างรหัสใบเสร็จ: {prefixTextBox.Text}{(int.TryParse(numberTextBox.Text, out int num) ? num : settings.CurrentReceiptNumber)}";
            };
            
            // แสดงรายการหมายเลขที่รอใช้ใหม่
            if (settings.CanceledReceiptNumbers.Count > 0)
            {
                settingsPanel.Children.Add(new TextBlock { 
                    Text = "หมายเลขใบเสร็จที่รอใช้ใหม่:", 
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 10, 0, 5)
                });
                
                var recycleNumbersText = new TextBlock { 
                    Text = string.Join(", ", settings.CanceledReceiptNumbers),
                    TextWrapping = TextWrapping.Wrap
                };
                settingsPanel.Children.Add(recycleNumbersText);
            }
            
            // Create the dialog
            var dialog = new ContentDialog
            {
                Title = "ตั้งค่าระบบ",
                Content = settingsPanel,
                PrimaryButtonText = "บันทึก",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };
            
            // Show the dialog and handle the result
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string receiptPrefix = prefixTextBox.Text.Trim();
                
                // Validate prefix input
                //if (string.IsNullOrEmpty(receiptPrefix))
                //{
                //    await ShowMessageDialog("กรุณากรอกคำนำหน้ารหัสใบเสร็จ", "ข้อผิดพลาด");
                //    return;
                //}
                
                // Validate number input
                if (!int.TryParse(numberTextBox.Text.Trim(), out int receiptNumber) || receiptNumber < 1)
                {
                    await ShowMessageDialog("กรุณากรอกเลขที่เริ่มต้นที่ถูกต้อง (ต้องเป็นตัวเลขที่มากกว่า 0)", "ข้อผิดพลาด");
                    return;
                }
                
                // Save settings
                settings.ReceiptCodePrefix = receiptPrefix;
                settings.CurrentReceiptNumber = receiptNumber;
                await AppSettings.SaveSettingsAsync(settings);
                
                await ShowMessageDialog("บันทึกการตั้งค่าเรียบร้อยแล้ว", "สำเร็จ");
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
