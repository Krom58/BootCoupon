using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace CouponManagement
{
    public sealed partial class UsersPage : Page
    {
        private readonly ObservableCollection<UserDisplay> _users = new();
        private readonly ObservableCollection<BranchDisplay> _branches = new();
        private readonly ObservableCollection<SaleEventDisplay> _saleEvents = new();

        private enum ManagementType
        {
            Users,
            Branches,
            Events
        }

        private ManagementType _currentType = ManagementType.Users;

        public UsersPage()
        {
            InitializeComponent();
            this.Loaded += UsersPage_Loaded;
        }

        private async void UsersPage_Loaded(object sender, RoutedEventArgs e)
        {
            UsersListView.ItemsSource = _users;
            BranchesListView.ItemsSource = _branches;
            EventsListView.ItemsSource = _saleEvents;
        }

        // Make handler async and load data when selection changes so user doesn't need to press Refresh.
        private async void ManagementTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox combo) return;

            // SelectedItem may be null during initialization
            if (combo.SelectedItem is not ComboBoxItem selected)
                return;

            var tag = selected.Tag?.ToString() ?? "Users";

            // Guard UI containers / buttons in case XAML names are missing
            if (UsersTableContainer != null)
                UsersTableContainer.Visibility = Visibility.Collapsed;
            if (BranchesTableContainer != null)
                BranchesTableContainer.Visibility = Visibility.Collapsed;
            if (EventsTableContainer != null)
                EventsTableContainer.Visibility = Visibility.Collapsed;
            if (CreateButton != null)
                CreateButton.Content = "สร้าง";

            switch (tag)
            {
                case "Users":
                    _currentType = ManagementType.Users;
                    if (UsersTableContainer != null)
                        UsersTableContainer.Visibility = Visibility.Visible;
                    if (CreateButton != null)
                        CreateButton.Content = "สร้างผู้ใช้";
                    break;
                case "Branches":
                    _currentType = ManagementType.Branches;
                    if (BranchesTableContainer != null)
                        BranchesTableContainer.Visibility = Visibility.Visible;
                    if (CreateButton != null)
                        CreateButton.Content = "สร้างสาขา";
                    break;
                case "Events":
                    _currentType = ManagementType.Events;
                    if (EventsTableContainer != null)
                        EventsTableContainer.Visibility = Visibility.Visible;
                    if (CreateButton != null)
                        CreateButton.Content = "สร้างชื่องาน";
                    break;
                default:
                    _currentType = ManagementType.Users;
                    if (UsersTableContainer != null)
                        UsersTableContainer.Visibility = Visibility.Visible;
                    if (CreateButton != null)
                        CreateButton.Content = "สร้างผู้ใช้";
                    break;
            }

            // Immediately load data for the newly selected type so user doesn't have to press Refresh.
            await LoadDataAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            switch (_currentType)
            {
                case ManagementType.Users:
                    await CreateUserAsync();
                    break;
                case ManagementType.Branches:
                    await CreateBranchAsync();
                    break;
                case ManagementType.Events:
                    await CreateEventAsync();
                    break;
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                switch (_currentType)
                {
                    case ManagementType.Users:
                        await LoadUsersAsync();
                        break;
                    case ManagementType.Branches:
                        await LoadBranchesAsync();
                        break;
                    case ManagementType.Events:
                        await LoadEventsAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ไม่สามารถโหลดข้อมูล", ex.Message);
            }
        }

        // ==================== USERS ====================

        private async Task LoadUsersAsync()
        {
            _users.Clear();
            using var ctx = new CouponContext();
            var list = await ctx.ApplicationUsers.AsNoTracking()
                .OrderBy(u => u.Username)
                .ToListAsync();

            foreach (var u in list)
            {
                _users.Add(new UserDisplay(u));
            }
        }

        private async Task CreateUserAsync()
        {
            List<string> typeOptions;
            try
            {
                using var ctx = new CouponContext();
                typeOptions = await ctx.Branches
                    .AsNoTracking()
                    .OrderBy(ct => ct.Name)
                    .Select(ct => ct.Name)
                    .ToListAsync();
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

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = "สร้างผู้ใช้ใหม่", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var usernameBox = new TextBox { PlaceholderText = "Username" };
            var passwordBox = new PasswordBox { PlaceholderText = "Password" };
            var displayBox = new TextBox { PlaceholderText = "Display name (optional)" };
            var userTypeCombo = new ComboBox { Width = 220 };
            foreach (var opt in options) userTypeCombo.Items.Add(opt);
            if (userTypeCombo.Items.Count > 0) userTypeCombo.SelectedIndex = 0;
            var activeCheck = new CheckBox { Content = "Is active", IsChecked = true };

            panel.Children.Add(usernameBox);
            panel.Children.Add(passwordBox);
            panel.Children.Add(displayBox);
            panel.Children.Add(new TextBlock { Text = "ประเภทผู้ใช้", Margin = new Thickness(0, 6, 0, 0) });
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
                await ShowErrorAsync("ข้อผิดพลาด", "Username และ Password จำเป็นต้องระบุ");
                return;
            }

            try
            {
                using var ctx = new CouponContext();
                var exists = await ctx.ApplicationUsers.AnyAsync(u => u.Username == username);
                if (exists)
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "Username นี้มีอยู่แล้ว");
                    return;
                }

                var user = new ApplicationUser
                {
                    Username = username,
                    Password = password,
                    DisplayName = string.IsNullOrWhiteSpace(display) ? null : display,
                    IsActive = isActive,
                    UserType = userType,
                    CreatedAt = DateTime.Now
                };

                ctx.ApplicationUsers.Add(user);
                await ctx.SaveChangesAsync();
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ไม่สามารถสร้างผู้ใช้", ex.Message);
            }
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not UserDisplay ud) return;

            try
            {
                using var ctx = new CouponContext();
                var user = await ctx.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == ud.Id);
                if (user == null)
                {
                    await ShowErrorAsync("ไม่พบผู้ใช้", "ไม่สามารถหา record ของผู้ใช้ได้");
                    return;
                }

                List<string> typeOptions;
                try
                {
                    typeOptions = await ctx.Branches.AsNoTracking().OrderBy(ct => ct.Name).Select(ct => ct.Name).ToListAsync();
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

                var panel = new StackPanel { Spacing = 8 };
                panel.Children.Add(new TextBlock { Text = "แก้ไขผู้ใช้", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                var usernameBox = new TextBox { Text = user.Username ?? string.Empty };
                var passwordBox = new PasswordBox { Password = user.Password ?? string.Empty };
                var displayBox = new TextBox { Text = user.DisplayName ?? string.Empty };
                var userTypeCombo = new ComboBox { Width = 220 };
                foreach (var opt in options) userTypeCombo.Items.Add(opt);

                var currentType = user.UserType ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(currentType))
                {
                    for (int i = 0; i < userTypeCombo.Items.Count; i++)
                    {
                        if (string.Equals(userTypeCombo.Items[i] as string, currentType, StringComparison.OrdinalIgnoreCase))
                        {
                            userTypeCombo.SelectedIndex = i;
                            break;
                        }
                    }
                }
                if (userTypeCombo.SelectedIndex < 0 && userTypeCombo.Items.Count > 0) userTypeCombo.SelectedIndex = 0;

                var activeCheck = new CheckBox { Content = "Is active", IsChecked = user.IsActive };

                panel.Children.Add(new TextBlock { Text = "Username" });
                panel.Children.Add(usernameBox);
                panel.Children.Add(new TextBlock { Text = "Password" });
                panel.Children.Add(passwordBox);
                panel.Children.Add(new TextBlock { Text = "Display name (optional)" });
                panel.Children.Add(displayBox);
                panel.Children.Add(new TextBlock { Text = "ประเภทผู้ใช้", Margin = new Thickness(0, 6, 0, 0) });
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
                    await ShowErrorAsync("ข้อผิดพลาด", "Username และ Password จำเป็นต้องระบุ");
                    return;
                }

                var exists = await ctx.ApplicationUsers.AnyAsync(u => u.Username == newUsername && u.Id != user.Id);
                if (exists)
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "Username นี้มีอยู่แล้ว");
                    return;
                }

                user.Username = newUsername;
                user.Password = newPassword;
                user.DisplayName = string.IsNullOrWhiteSpace(newDisplay) ? null : newDisplay;
                user.IsActive = newIsActive;
                user.UserType = newUserType;

                await ctx.SaveChangesAsync();
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ไม่สามารถแก้ไขผู้ใช้", ex.Message);
            }
        }

        // ==================== BRANCHES ====================

        private async Task LoadBranchesAsync()
        {
            _branches.Clear();
            using var ctx = new CouponContext();

            // order by Id ascending (รหัสจากน้อยไปมาก)
            var list = await ctx.Branches
                .AsNoTracking()
                .OrderBy(b => b.Id)
                .Select(b => new { b.Id, b.Name, b.CreatedBy, b.CreatedAt })
                .ToListAsync();

            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                _branches.Add(new BranchDisplay(dbId: b.Id, sequence: i + 1, name: b.Name, createdBy: b.CreatedBy, createdAt: b.CreatedAt));
            }
        }

        private async Task CreateBranchAsync()
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = "สร้างสาขาใหม่", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var nameBox = new TextBox { PlaceholderText = "ชื่อสาขา" };

            panel.Children.Add(new TextBlock { Text = "ชื่อสาขา" });
            panel.Children.Add(nameBox);

            var dlg = new ContentDialog
            {
                Title = "สร้างสาขา",
                Content = panel,
                PrimaryButtonText = "บันทึก",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dlg.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var name = nameBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                await ShowErrorAsync("ข้อผิดพลาด", "ชื่อสาขาจำเป็นต้องระบุ");
                return;
            }

            try
            {
                using var ctx = new CouponContext();
                var exists = await ctx.Branches.AnyAsync(b => b.Name == name);
                if (exists)
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "ชื่อสาขานี้มีอยู่แล้ว");
                    return;
                }

                var branch = new Branch
                {
                    Name = name,
                    CreatedBy = Environment.UserName,
                    CreatedAt = DateTime.Now
                };

                ctx.Branches.Add(branch);
                await ctx.SaveChangesAsync();
                await LoadBranchesAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ไม่สามารถสร้างสาขา", ex.Message);
            }
        }

        private async void EditBranchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not BranchDisplay bd) return;

            try
            {
                using var ctx = new CouponContext();
                var branch = await ctx.Branches.FirstOrDefaultAsync(b => b.Id == bd.Id);
                if (branch == null)
                {
                    await ShowErrorAsync("ไม่พบสาขา", "ไม่สามารถหา record ของสาขาได้");
                    return;
                }

                var panel = new StackPanel { Spacing = 8 };
                panel.Children.Add(new TextBlock { Text = "แก้ไขสาขา", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                var nameBox = new TextBox { Text = branch.Name ?? string.Empty };

                panel.Children.Add(new TextBlock { Text = "ชื่อสาขา" });
                panel.Children.Add(nameBox);

                var dlg = new ContentDialog
                {
                    Title = "แก้ไขสาขา",
                    Content = panel,
                    PrimaryButtonText = "บันทึก",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result = await dlg.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                var newName = nameBox.Text?.Trim();

                if (string.IsNullOrWhiteSpace(newName))
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "ชื่อสาขาจำเป็นต้องระบุ");
                    return;
                }

                var exists = await ctx.Branches.AnyAsync(b => b.Name == newName && b.Id != branch.Id);
                if (exists)
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "ชื่อสาขานี้มีอยู่แล้ว");
                    return;
                }

                branch.Name = newName;

                await ctx.SaveChangesAsync();
                await LoadBranchesAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ไม่สามารถแก้ไขสาขา", ex.Message);
            }
        }

        // ==================== SALE EVENTS ====================

        private async Task LoadEventsAsync()
        {
            _saleEvents.Clear();
            using var ctx = new CouponContext();

            // order by Id ascending (รหัสจากน้อยไปมาก)
            var list = await ctx.SaleEvents
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.StartDate,
                    e.EndDate,
                    e.IsActive,
                    e.CreatedBy,
                    e.CreatedAt,
                    e.UpdatedBy,
                    e.UpdatedAt
                })
                .ToListAsync();

            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                _saleEvents.Add(new SaleEventDisplay(
                    dbId: e.Id,
                    sequence: i + 1,
                    name: e.Name,
                    start: e.StartDate,
                    end: e.EndDate,
                    isActive: e.IsActive,
                    createdBy: e.CreatedBy,
                    createdAt: e.CreatedAt,
                    updatedBy: e.UpdatedBy,
                    updatedAt: e.UpdatedAt));
            }
        }

        private async Task CreateEventAsync()
        {
            // Only DatePicker inputs (no TimePicker) — store dates with time 00:00
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = "สร้างชื่องานใหม่", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

            panel.Children.Add(new TextBlock { Text = "ชื่องาน" });
            var nameBox = new TextBox { PlaceholderText = "ชื่องาน" };
            panel.Children.Add(nameBox);

            panel.Children.Add(new TextBlock { Text = "วันที่เริ่ม" });
            var startDate = new DatePicker { Date = DateTimeOffset.Now.Date };
            panel.Children.Add(startDate);

            panel.Children.Add(new TextBlock { Text = "วันที่สิ้นสุด" });
            var endDate = new DatePicker { Date = DateTimeOffset.Now.Date.AddDays(1) };
            panel.Children.Add(endDate);

            var dlg = new ContentDialog
            {
                Title = "สร้างชื่องาน",
                Content = panel,
                PrimaryButtonText = "บันทึก",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dlg.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var name = nameBox.Text?.Trim();
            var start = startDate.Date.DateTime; // use date only
            var end = endDate.Date.DateTime;

            if (string.IsNullOrWhiteSpace(name))
            {
                await ShowErrorAsync("ข้อผิดพลาด", "ชื่องานจำเป็นต้องระบุ");
                return;
            }

            if (start > end)
            {
                await ShowErrorAsync("ข้อผิดพลาด", "วันที่เริ่มต้องไม่มากกว่าวันที่สิ้นสุด");
                return;
            }

            try
            {
                using var ctx = new CouponContext();
                var exists = await ctx.SaleEvents.AnyAsync(e => e.Name == name);
                if (exists)
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "ชื่องานนี้มีอยู่แล้ว");
                    return;
                }

                var saleEvent = new SaleEvent
                {
                    Name = name,
                    CreatedBy = Environment.UserName,
                    CreatedAt = DateTime.Now,
                    StartDate = start,
                    EndDate = end,
                    IsActive = true
                };

                ctx.SaleEvents.Add(saleEvent);
                await ctx.SaveChangesAsync();
                await LoadEventsAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ไม่สามารถสร้างชื่องาน", ex.Message);
            }
        }

        private async void EditEventButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not SaleEventDisplay ed) return;

            try
            {
                using var ctx = new CouponContext();
                var saleEvent = await ctx.SaleEvents.FirstOrDefaultAsync(x => x.Id == ed.Id);
                if (saleEvent == null)
                {
                    await ShowErrorAsync("ไม่พบชื่องาน", "ไม่สามารถหา record ของชื่องานได้");
                    return;
                }

                var panel = new StackPanel { Spacing = 8 };
                panel.Children.Add(new TextBlock { Text = "แก้ไขชื่องาน", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                panel.Children.Add(new TextBlock { Text = "ชื่องาน" });
                var nameBox = new TextBox { Text = saleEvent.Name ?? string.Empty };
                panel.Children.Add(nameBox);

                panel.Children.Add(new TextBlock { Text = "วันที่เริ่ม" });
                var startDate = new DatePicker { Date = new DateTimeOffset(saleEvent.StartDate.Date) };
                panel.Children.Add(startDate);

                panel.Children.Add(new TextBlock { Text = "วันที่สิ้นสุด" });
                var endDate = new DatePicker { Date = new DateTimeOffset(saleEvent.EndDate.Date) };
                panel.Children.Add(endDate);

                var dlg = new ContentDialog
                {
                    Title = "แก้ไขชื่องาน",
                    Content = panel,
                    PrimaryButtonText = "บันทึก",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result = await dlg.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                var newName = nameBox.Text?.Trim();
                var newStart = startDate.Date.DateTime;
                var newEnd = endDate.Date.DateTime;

                if (string.IsNullOrWhiteSpace(newName))
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "ชื่องานจำเป็นต้องระบุ");
                    return;
                }

                if (newStart > newEnd)
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "วันที่เริ่มต้องไม่มากกว่าวันที่สิ้นสุด");
                    return;
                }

                var exists = await ctx.SaleEvents.AnyAsync(x => x.Name == newName && x.Id != saleEvent.Id);
                if (exists)
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "ชื่องานนี้มีอยู่แล้ว");
                    return;
                }

                saleEvent.Name = newName;
                saleEvent.StartDate = newStart;
                saleEvent.EndDate = newEnd;
                saleEvent.UpdatedBy = Environment.UserName;
                saleEvent.UpdatedAt = DateTime.Now;

                await ctx.SaveChangesAsync();
                await LoadEventsAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ไม่สามารถแก้ไขชื่องาน", ex.Message);
            }
        }

        // ==================== HELPERS ====================

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(MainPage));
            }
        }

        private async Task ShowErrorAsync(string title, string message)
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "ตกลง",
                XamlRoot = this.XamlRoot
            };
            await dlg.ShowAsync();
        }

        // ==================== DISPLAY CLASSES (updated) ====================

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
                UserType = string.IsNullOrWhiteSpace(u.UserType) ? "User" : u.UserType!;
                IsActive = u.IsActive;
                CreatedAt = u.CreatedAt;
            }
        }

        private class BranchDisplay
        {
            public int Id { get; }           // DB id
            public int Sequence { get; }    // 1-based sequence for display
            public string Code => Id.ToString(); // expose Code so XAML bindings keep working (now set to DB id)
            public string Name { get; }
            public string CreatedBy { get; }
            public DateTime CreatedAt { get; }
            public string CreatedAtText => CreatedAt == default ? string.Empty : CreatedAt.ToString("dd/MM/yyyy HH:mm");

            public BranchDisplay(int dbId, int sequence, string name, string createdBy, DateTime createdAt)
            {
                Id = dbId;
                Sequence = sequence;
                Name = name ?? string.Empty;
                CreatedBy = createdBy ?? string.Empty;
                CreatedAt = createdAt;
            }
        }

        private class SaleEventDisplay
        {
            public int Id { get; }           // DB id
            public int Sequence { get; }     // 1-based sequence for display
            public string Code => Id.ToString();
            public string Name { get; }
            public DateTime StartDate { get; }
            public DateTime EndDate { get; }
            public bool IsActive { get; }
            public string CreatedBy { get; }
            public DateTime CreatedAt { get; }
            public string? UpdatedBy { get; }
            public DateTime? UpdatedAt { get; }

            public string StartDateText => StartDate == default ? string.Empty : StartDate.ToString("dd/MM/yyyy");
            public string EndDateText => EndDate == default ? string.Empty : EndDate.ToString("dd/MM/yyyy");
            public string CreatedAtText => CreatedAt == default ? string.Empty : CreatedAt.ToString("dd/MM/yyyy HH:mm");
            public string UpdatedAtText => UpdatedAt.HasValue ? UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty;
            public string DateRangeText => $"{StartDateText} - {EndDateText}";
            public string StatusText => IsActive ? "ใช้งาน" : "ปิดใช้งาน";

            public SaleEventDisplay(int dbId, int sequence, string name, DateTime start, DateTime end, bool isActive, string createdBy, DateTime createdAt, string? updatedBy, DateTime? updatedAt)
            {
                Id = dbId;
                Sequence = sequence;
                Name = name ?? string.Empty;
                StartDate = start.Date;
                EndDate = end.Date;
                IsActive = isActive;
                CreatedBy = createdBy ?? string.Empty;
                CreatedAt = createdAt;
                UpdatedBy = updatedBy;
                UpdatedAt = updatedAt;
            }
        }
    }
}