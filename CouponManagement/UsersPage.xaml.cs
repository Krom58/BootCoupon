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
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CouponManagement
{
    /// <summary>
    /// Page for managing users. Loads ApplicationUsers from database and displays them.
    /// </summary>
    public sealed partial class UsersPage : Page
    {
        private readonly ObservableCollection<UserDisplay> _users = new();

        public UsersPage()
        {
            InitializeComponent();
            this.Loaded += UsersPage_Loaded;
        }

        private async void UsersPage_Loaded(object sender, RoutedEventArgs e)
        {
            // locate ListView by name at runtime to avoid needing generated field in analysis step
            var lv = this.FindName("UsersListView") as ListView;
            if (lv != null)
            {
                lv.ItemsSource = _users;
            }
            await LoadUsersAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadUsersAsync();
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            // Load coupon types to use as user type options
            List<string> typeOptions;
            try
            {
                using var ctx = new CouponContext();
                typeOptions = await ctx.CouponTypes
                    .AsNoTracking()
                    .OrderBy(ct => ct.Name)
                    .Select(ct => ct.Name)
                    .ToListAsync();
            }
            catch
            {
                typeOptions = new List<string>();
            }

            // Build options with case-insensitive uniqueness: coupon types first, admin last; do not include 'User'
            var options = new List<string>();
            void AddIfNotExists(string v)
            {
                if (string.IsNullOrWhiteSpace(v)) return;
                if (!options.Any(s => string.Equals(s, v, StringComparison.OrdinalIgnoreCase)))
                    options.Add(v);
            }
            foreach (var t in typeOptions)
            {
                AddIfNotExists(t);
            }
            // ensure admin is present, but add at the end
            AddIfNotExists("admin");

            var panel = new StackPanel { Spacing =8 };
            panel.Children.Add(new TextBlock { Text = "สร้างผู้ใช้ใหม่", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var usernameBox = new TextBox { PlaceholderText = "Username" };
            var passwordBox = new PasswordBox { PlaceholderText = "Password" };
            var displayBox = new TextBox { PlaceholderText = "Display name (optional)" };
            var userTypeCombo = new ComboBox { Width =220 };
            foreach (var opt in options) userTypeCombo.Items.Add(opt);
            if (userTypeCombo.Items.Count >0) userTypeCombo.SelectedIndex =0;
            var activeCheck = new CheckBox { Content = "Is active", IsChecked = true };

            panel.Children.Add(usernameBox);
            panel.Children.Add(passwordBox);
            panel.Children.Add(displayBox);
            panel.Children.Add(new TextBlock { Text = "ประเภทผู้ใช้", Margin = new Thickness(0,6,0,0) });
            panel.Children.Add(userTypeCombo);
            panel.Children.Add(activeCheck);

            var dlg = new ContentDialog
            {
                Title = "สร้างผู้ใช้",
                Content = panel,
                PrimaryButtonText = "บันทึก",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dlg.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var username = usernameBox.Text?.Trim();
            var password = passwordBox.Password?.Trim();
            var display = displayBox.Text?.Trim();
            var userType = (userTypeCombo.SelectedItem as string) ?? "User";
            var isActive = activeCheck.IsChecked == true;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = "Username และ Password จำเป็นต้องระบุ", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                await err.ShowAsync();
                return;
            }

            try
            {
                using var ctx = new CouponContext();
                var exists = await ctx.ApplicationUsers.AnyAsync(u => u.Username == username);
                if (exists)
                {
                    var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = "Username นี้มีอยู่แล้ว", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await err.ShowAsync();
                    return;
                }

                var user = new ApplicationUser
                {
                    Username = username,
                    Password = password,
                    DisplayName = string.IsNullOrWhiteSpace(display) ? null : display,
                    IsActive = isActive,
                    CreatedAt = DateTime.Now
                };

                // set UserType if property exists
                var prop = typeof(ApplicationUser).GetProperty("UserType");
                if (prop != null && prop.PropertyType == typeof(string))
                {
                    prop.SetValue(user, userType);
                }

                ctx.ApplicationUsers.Add(user);
                await ctx.SaveChangesAsync();

                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                var err = new ContentDialog { Title = "ไม่สามารถสร้างผู้ใช้", Content = ex.Message, CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                await err.ShowAsync();
            }
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            // Determine which user was clicked via DataContext of the button
            if (sender is not Button btn) return;
            if (btn.DataContext is not UserDisplay ud) return;

            try
            {
                using var ctx = new CouponContext();
                var user = await ctx.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == ud.Id);
                if (user == null)
                {
                    var missing = new ContentDialog { Title = "ไม่พบผู้ใช้", Content = "ไม่สามารถหา record ของผู้ใช้ได้", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await missing.ShowAsync();
                    return;
                }

                // Load user type options similar to Create
                List<string> typeOptions;
                try
                {
                    typeOptions = await ctx.CouponTypes.AsNoTracking().OrderBy(ct => ct.Name).Select(ct => ct.Name).ToListAsync();
                }
                catch
                {
                    typeOptions = new List<string>();
                }

                var options = new List<string>();
                void AddIfNotExists(string v)
                {
                    if (string.IsNullOrWhiteSpace(v)) return;
                    if (!options.Any(s => string.Equals(s, v, StringComparison.OrdinalIgnoreCase)))
                        options.Add(v);
                }
                foreach (var t in typeOptions) AddIfNotExists(t);
                AddIfNotExists("admin");

                // Build edit UI pre-filled
                var panel = new StackPanel { Spacing =8 };
                panel.Children.Add(new TextBlock { Text = "แก้ไขผู้ใช้", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                var usernameBox = new TextBox { Text = user.Username ?? string.Empty };
                var passwordBox = new PasswordBox { Password = user.Password ?? string.Empty };
                var displayBox = new TextBox { Text = user.DisplayName ?? string.Empty };
                var userTypeCombo = new ComboBox { Width =220 };
                foreach (var opt in options) userTypeCombo.Items.Add(opt);
                // select current user type if present
                var currentType = typeof(ApplicationUser).GetProperty("UserType")?.GetValue(user) as string ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(currentType))
                {
                    for (int i =0; i < userTypeCombo.Items.Count; i++)
                    {
                        if (string.Equals(userTypeCombo.Items[i] as string, currentType, StringComparison.OrdinalIgnoreCase))
                        {
                            userTypeCombo.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (userTypeCombo.SelectedIndex <0 && userTypeCombo.Items.Count >0) userTypeCombo.SelectedIndex =0;

                var activeCheck = new CheckBox { Content = "Is active", IsChecked = user.IsActive };

                panel.Children.Add(new TextBlock { Text = "Username" });
                panel.Children.Add(usernameBox);
                panel.Children.Add(new TextBlock { Text = "Password" });
                panel.Children.Add(passwordBox);
                panel.Children.Add(new TextBlock { Text = "Display name (optional)" });
                panel.Children.Add(displayBox);
                panel.Children.Add(new TextBlock { Text = "ประเภทผู้ใช้", Margin = new Thickness(0,6,0,0) });
                panel.Children.Add(userTypeCombo);
                panel.Children.Add(activeCheck);

                var dlg = new ContentDialog
                {
                    Title = "แก้ไขผู้ใช้",
                    Content = panel,
                    PrimaryButtonText = "บันทึก",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result = await dlg.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                var newUsername = usernameBox.Text?.Trim();
                var newPassword = passwordBox.Password?.Trim();
                var newDisplay = displayBox.Text?.Trim();
                var newUserType = (userTypeCombo.SelectedItem as string) ?? "User";
                var newIsActive = activeCheck.IsChecked == true;

                if (string.IsNullOrWhiteSpace(newUsername) || string.IsNullOrWhiteSpace(newPassword))
                {
                    var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = "Username และ Password จำเป็นต้องระบุ", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await err.ShowAsync();
                    return;
                }

                // check uniqueness excluding current record
                var exists = await ctx.ApplicationUsers.AnyAsync(u => u.Username == newUsername && u.Id != user.Id);
                if (exists)
                {
                    var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = "Username นี้มีอยู่แล้ว", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await err.ShowAsync();
                    return;
                }

                // apply changes
                user.Username = newUsername;
                user.Password = newPassword;
                user.DisplayName = string.IsNullOrWhiteSpace(newDisplay) ? null : newDisplay;
                user.IsActive = newIsActive;
                var prop = typeof(ApplicationUser).GetProperty("UserType");
                if (prop != null && prop.PropertyType == typeof(string))
                {
                    prop.SetValue(user, newUserType);
                }

                await ctx.SaveChangesAsync();
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                var err = new ContentDialog { Title = "ไม่สามารถแก้ไขผู้ใช้", Content = ex.Message, CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                await err.ShowAsync();
            }
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                _users.Clear();
                using var ctx = new CouponContext();
                // ensure DB can be accessed; don't force create here
                var list = await ctx.ApplicationUsers.AsNoTracking()
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                foreach (var u in list)
                {
                    _users.Add(new UserDisplay(u));
                }
            }
            catch (Exception ex)
            {
                // Simple user feedback: show a dialog
                var dlg = new ContentDialog
                {
                    Title = "ไม่สามารถโหลดผู้ใช้",
                    Content = ex.Message,
                    CloseButtonText = "ตกลง",
                    XamlRoot = this.XamlRoot
                };
                _ = dlg.ShowAsync();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate back to the main page
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(MainPage));
            }
        }

        private class UserDisplay
        {
            public int Id { get; }
            public string Username { get; }
            public string DisplayName { get; }
            public string UserType { get; }
            public bool IsActive { get; }
            public string IsActiveText => IsActive ? "ใช้งาน" : "ปิดใช้งาน";
            public DateTime CreatedAt { get; }
            public string CreatedAtText => CreatedAt == default ? string.Empty : CreatedAt.ToString("dd/MM/yyyy HH:mm");

            public UserDisplay(ApplicationUser u)
            {
                Id = u.Id;
                Username = u.Username ?? string.Empty;
                DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? "-" : u.DisplayName!;
                // read UserType column if exists via property on model; fallback to 'User'
                try
                {
                    var prop = typeof(ApplicationUser).GetProperty("UserType");
                    if (prop != null)
                    {
                        var val = prop.GetValue(u) as string;
                        UserType = string.IsNullOrWhiteSpace(val) ? "User" : val!;
                    }
                    else
                    {
                        UserType = "User";
                    }
                }
                catch
                {
                    UserType = "User";
                }

                IsActive = u.IsActive;
                CreatedAt = u.CreatedAt;
            }
        }
    }
}