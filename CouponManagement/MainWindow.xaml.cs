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
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CouponManagement
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static MainWindow Instance { get; private set; } = null!;
        public Frame MainFrameControl => MainFrame;
        // Define the necessary Win32 API functions and structures
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // Constants for GetSystemMetrics
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        // Constants for SetWindowPos
        private const uint SWP_SHOWWINDOW = 0x0040;

        public MainWindow()
        {
            try
            {
                Debug.WriteLine("=== MainWindow Constructor Start ===");
                this.InitializeComponent();

                Debug.WriteLine("=== MainWindow InitializeComponent Complete ===");

                // Set window properties
                this.Title = "Coupon Management System";

                Debug.WriteLine("=== MainWindow Constructor Complete ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in MainWindow constructor: {ex.Message}");
                Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }

            Instance = this;
            if (MainFrame != null)
            {
                // ตรวจสอบว่า Type ไม่เป็น null
                Type pageType = typeof(MainPage);
                if (pageType != null)
                {
                    MainFrame.Navigate(pageType);
                }
            }

            // Set the window size and position after initialization
            SetWindowSizeAndCenter(1920, 1080);
        }

        private void SetWindowSizeAndCenter(int width, int height)
        {
            // Get window handle
            IntPtr hWnd = WindowNative.GetWindowHandle(this);

            // Get screen dimensions
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            // Calculate position to center the window
            int x = (screenWidth - width) / 2;
            int y = (screenHeight - height) / 2;

            // Ensure x and y are not negative
            x = Math.Max(0, x);
            y = Math.Max(0, y);

            // Set window size and position
            SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, SWP_SHOWWINDOW);
        }
    }
}
