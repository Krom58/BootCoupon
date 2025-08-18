using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace BootCoupon
{
    public sealed partial class AddCoupon : Page
    {
        // Initialize context inline to satisfy nullability and avoid CS8618
        private readonly CouponContext _context = new CouponContext();
        private readonly ObservableCollection<Coupon> _coupons = new();

        public AddCoupon()
        {
            this.InitializeComponent();

            CouponListView.ItemsSource = _coupons;
            this.Loaded += AddCoupon_Loaded;
        }

        private async void AddCoupon_Loaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe so it only runs once
            this.Loaded -= AddCoupon_Loaded;

            try
            {
                // Small delay to allow layout to settle before measuring
                await Task.Delay(50);

                await _context.Database.EnsureCreatedAsync();
                await LoadCouponTypes();
                await LoadCoupons();

                // Try to resize window to contents (best-effort)
                try
                {
                    double w = RootGrid.ActualWidth;
                    double h = RootGrid.ActualHeight;
                }
                catch { /* ignore resize errors */ }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                await ShowErrorDialog($"Error during load: {ex.Message}");
            }
        }

        private async Task LoadCouponTypes()
        {
            await _context.Database.EnsureCreatedAsync();
            var types = await _context.CouponTypes.ToListAsync();
            CouponTypeComboBox.ItemsSource = types;
            CouponTypeComboBox.DisplayMemberPath = "Name";
        }

        private async Task LoadCoupons()
        {
            try
            {
                await _context.Database.EnsureCreatedAsync();

                var coupons = await _context.Coupons
                    .Include(c => c.CouponType)
                    .ToListAsync();

                // Update observable collection on UI thread (we are on UI thread here)
                _coupons.Clear();
                foreach (var coupon in coupons)
                {
                    _coupons.Add(coupon);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Error loading coupons: {ex.Message}");
            }
        }

        private async void AddTypeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var typeNameBox = new TextBox
                {
                    PlaceholderText = "กรอกชื่อประเภทคูปอง",
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var dialog = new ContentDialog
                {
                    Title = "เพิ่มประเภทคูปอง",
                    Content = typeNameBox,
                    PrimaryButtonText = "ตกลง",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var typeName = typeNameBox.Text.Trim();

                    if (string.IsNullOrWhiteSpace(typeName))
                    {
                        await ShowErrorDialog("กรุณากรอกชื่อประเภทคูปอง");
                        return;
                    }

                    var newType = new CouponType { Name = typeName };
                    _context.CouponTypes.Add(newType);
                    await _context.SaveChangesAsync();

                    await LoadCouponTypes();
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"ไม่สามารถเพิ่มประเภทคูปองได้: {ex.Message}");
            }
        }

        private async void EditTypeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _context.Database.EnsureCreatedAsync();
                var types = await _context.CouponTypes.ToListAsync();

                if (types.Count == 0)
                {
                    await ShowErrorDialog("ยังไม่มีประเภทคูปองให้แก้ไข");
                    return;
                }

                var listView = new ListView
                {
                    Height = 300
                };

                foreach (var t in types)
                {
                    var item = new ListViewItem
                    {
                        Content = t.Name,
                        Tag = t
                    };
                    listView.Items.Add(item);
                }

                var selectDialog = new ContentDialog
                {
                    Title = "เลือกประเภทคูปองที่จะแก้ไข",
                    Content = listView,
                    PrimaryButtonText = "เลือก",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var selectResult = await selectDialog.ShowAsync();
                if (selectResult != ContentDialogResult.Primary)
                {
                    return; // user cancelled
                }

                var selectedListViewItem = listView.SelectedItem as ListViewItem;
                if (selectedListViewItem == null)
                {
                    await ShowErrorDialog("กรุณาเลือกประเภทก่อน");
                    return;
                }

                var selectedType = selectedListViewItem.Tag as CouponType;
                if (selectedType == null)
                {
                    await ShowErrorDialog("ไม่พบประเภทที่เลือก");
                    return;
                }

                var nameBox = new TextBox
                {
                    Text = selectedType.Name ?? string.Empty,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var editDialog = new ContentDialog
                {
                    Title = "แก้ไขชื่อประเภทคูปอง",
                    Content = nameBox,
                    PrimaryButtonText = "ตกลง",
                    CloseButtonText = "ยกเลิก",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };

                var editResult = await editDialog.ShowAsync();
                if (editResult != ContentDialogResult.Primary)
                {
                    return;
                }

                var newName = nameBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(newName))
                {
                    await ShowErrorDialog("กรุณากรอกชื่อประเภทคูปอง");
                    return;
                }

                selectedType.Name = newName;
                _context.CouponTypes.Update(selectedType);
                await _context.SaveChangesAsync();

                await LoadCouponTypes();
                await LoadCoupons();

                await ShowErrorDialog("แก้ไขชื่อประเภทคูปองเรียบร้อยแล้ว");
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"ไม่สามารถแก้ไขประเภทคูปองได้: {ex.Message}");
            }
        }

        private async Task ShowErrorDialog(string message)
        {
            var errorDialog = new ContentDialog
            {
                Title = "แจ้งเตือน",
                Content = message,
                CloseButtonText = "ตกลง",
                XamlRoot = this.XamlRoot
            };

            await errorDialog.ShowAsync();
        }

        private async void SaveCouponButton_Click(object sender, RoutedEventArgs e)
        {
            var name = CouponNameTextBox.Text.Trim();
            var priceText = CouponPriceTextBox.Text.Trim();
            var code = CouponCodeTextBox.Text.Trim();
            var selectedType = CouponTypeComboBox.SelectedItem as CouponType;

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(priceText) ||
                string.IsNullOrWhiteSpace(code) ||
                selectedType == null)
            {
                await ShowErrorDialog("กรุณากรอกข้อมูลให้ครบทุกช่องและเลือกประเภทคูปอง");
                return;
            }

            if (!decimal.TryParse(priceText, out var price) || price < 0)
            {
                await ShowErrorDialog("กรุณากรอกราคาเป็นตัวเลขที่ถูกต้อง");
                return;
            }

            var coupon = new Coupon
            {
                Name = name,
                Price = price,
                Code = code,
                CouponTypeId = selectedType.Id,
                CouponType = selectedType
            };

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            await ShowErrorDialog("บันทึกข้อมูลสำเร็จ");
            await LoadCoupons();

            CouponNameTextBox.Text = string.Empty;
            CouponPriceTextBox.Text = string.Empty;
            CouponCodeTextBox.Text = string.Empty;
            CouponTypeComboBox.SelectedItem = null;
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.MainFrameControl.Navigate(typeof(MainPage));
        }
    }
}
