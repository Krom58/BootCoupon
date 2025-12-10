using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace CouponManagement
{
    public sealed partial class ReceiptPage : Page
    {
        private ObservableCollection<ReceiptRow> _rows = new ObservableCollection<ReceiptRow>();

        public ReceiptPage()
        {
            this.InitializeComponent();

            // Use FindName to locate controls to avoid generated field issues in some build setups
            var listView = this.FindName("ReceiptsListView") as ListView;
            if (listView != null)
            {
                listView.ItemsSource = _rows;
                listView.SelectionChanged += ReceiptsListView_SelectionChanged;
            }

            // wire up filter controls
            var refreshBtn = this.FindName("RefreshButton") as Button;
            if (refreshBtn != null) refreshBtn.Click += RefreshBtn_Click;

            var searchBox = this.FindName("SearchTextBox") as TextBox;
            if (searchBox != null) searchBox.TextChanged += SearchBox_TextChanged;

            var statusCombo = this.FindName("StatusFilterComboBox") as ComboBox;
            if (statusCombo != null) statusCombo.SelectionChanged += StatusCombo_SelectionChanged;

            // wire coupon type filter
            var couponTypeCombo = this.FindName("CouponTypeFilterComboBox") as ComboBox;
            if (couponTypeCombo != null) couponTypeCombo.SelectionChanged += CouponTypeCombo_SelectionChanged;

            var editBtn = this.FindName("EditButton") as Button;
            if (editBtn != null) editBtn.Click += EditButton_Click;

            // wire up new EditItems button
            var editItemsBtn = this.FindName("EditItemsButton") as Button;
            if (editItemsBtn != null) editItemsBtn.Click += EditItemsButton_Click;

            var cancelBtn = this.FindName("CancelButton") as Button;
            if (cancelBtn != null) cancelBtn.Click += CancelButton_Click;

            var backBtn = this.FindName("BackButton") as Button;
            if (backBtn != null) backBtn.Click += BackButton_Click;

            Loaded += ReceiptPage_Loaded;
        }

        private async void ReceiptPage_Loaded(object sender, RoutedEventArgs e)
        {
            // load coupon types first so filter is available
            await LoadCouponTypesAsync();
            await LoadReceiptsAsync();
        }

        private void ReceiptsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var editBtn = this.FindName("EditButton") as Button;
            var editItemsBtn = this.FindName("EditItemsButton") as Button;
            var cancelBtn = this.FindName("CancelButton") as Button;
            var lv = this.FindName("ReceiptsListView") as ListView;
            if (editBtn != null && lv != null)
            {
                // enable edit only if an item selected and that item is not cancelled
                if (lv.SelectedItem is ReceiptRow sel && string.Equals(sel.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    editBtn.IsEnabled = false;
                }
                else
                {
                    editBtn.IsEnabled = lv.SelectedItem != null;
                }
            }

            if (editItemsBtn != null && lv != null)
            {
                // enable edit items only if an item selected and not cancelled
                if (lv.SelectedItem is ReceiptRow selItems && string.Equals(selItems.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    editItemsBtn.IsEnabled = false;
                }
                else
                {
                    editItemsBtn.IsEnabled = lv.SelectedItem != null;
                }
            }

            if (cancelBtn != null && lv != null)
            {
                // disable cancel if already cancelled
                if (lv.SelectedItem is ReceiptRow sel2 && string.Equals(sel2.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    cancelBtn.IsEnabled = false;
                }
                else
                {
                    cancelBtn.IsEnabled = lv.SelectedItem != null;
                }
            }
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadReceiptsAsync();
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await LoadReceiptsAsync();
        }

        private async void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadReceiptsAsync();
        }

        private async void CouponTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadReceiptsAsync();
        }

        // Navigate back to MainPage when footer Back button is clicked
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(MainPage));
            }
        }

        private async void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            var lv = this.FindName("ReceiptsListView") as ListView;
            if (lv?.SelectedItem is not ReceiptRow row)
            {
                var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = "โปรดเลือกรายการก่อน", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                await err.ShowAsync();
                return;
            }

            try
            {
                using var ctx = new CouponContext();
                var receipt = await ctx.Receipts.FirstOrDefaultAsync(r => r.ReceiptID == row.Id);
                if (receipt == null)
                {
                    var err = new ContentDialog { Title = "ไม่พบข้อมูล", Content = "ไม่พบข้อมูลใบเสร็จ", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await err.ShowAsync();
                    return;
                }

                // if already cancelled, inform user and return
                if (string.Equals(receipt.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    var info = new ContentDialog { Title = "ใบนั้นถูกยกเลิกแล้ว", Content = "ใบเสร็จนี้ถูกยกเลิกไปแล้ว ไม่สามารถทำรายการได้", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await info.ShowAsync();
                    return;
                }

                // build confirmation content
                var panel = new StackPanel { Spacing = 10 };
                panel.Children.Add(new TextBlock { Text = "คุณแน่ใจหรือไม่ว่าต้องการยกเลิกใบเสร็จ?", TextWrapping = TextWrapping.Wrap });

                // details
                panel.Children.Add(new TextBlock { Text = $"รหัสใบเสร็จ: {receipt.ReceiptCode}", Margin = new Thickness(0, 6, 0, 0) });
                panel.Children.Add(new TextBlock { Text = $"ชื่อลูกค้า: {receipt.CustomerName}", Margin = new Thickness(0, 2, 0, 0) });
                panel.Children.Add(new TextBlock { Text = $"ยอดเงิน: {receipt.TotalAmount:N2} บาท", Margin = new Thickness(0, 2, 0, 8) });

                // warning section
                var warn = new StackPanel { Spacing = 6 };
                warn.Children.Add(new TextBlock { Text = "เมื่อยกเลิกแล้ว:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 4) });
                warn.Children.Add(new TextBlock { Text = "• ใบเสร็จจะถูกทำเครื่องหมายว่า \"ยกเลิก\"", TextWrapping = TextWrapping.Wrap });
                warn.Children.Add(new TextBlock { Text = "• คูปองที่ถูกผูกกับใบเสร็จจะถูกคืนสถานะกลับเป็นยังไม่ได้ใช้", TextWrapping = TextWrapping.Wrap });
                warn.Children.Add(new TextBlock { Text = "• คูปองที่มีรหัสจะสามารถนำไปใช้ใหม่ได้", TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(warn);

                var dialog = new ContentDialog
                {
                    Title = "ยืนยันการยกเลิกใบเสร็จ",
                    Content = panel,
                    PrimaryButtonText = "ยืนยันการยกเลิก",
                    CloseButtonText = "ไม่ยกเลิก",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // perform cancellation - mark status
                    receipt.Status = "Cancelled";

                    // ==== restore generated coupons linked to this receipt ====
                    try
                    {
                        // find receipt item ids for this receipt
                        var receiptItemIds = await ctx.ReceiptItems
                            .Where(ri => ri.ReceiptId == receipt.ReceiptID)
                            .Select(ri => ri.ReceiptItemId)
                            .ToListAsync();

                        if (receiptItemIds != null && receiptItemIds.Count > 0)
                        {
                            var linkedGenerated = await ctx.GeneratedCoupons
                                .Where(g => g.ReceiptItemId != null && receiptItemIds.Contains(g.ReceiptItemId.Value))
                                .ToListAsync();

                            if (linkedGenerated.Any())
                            {
                                foreach (var g in linkedGenerated)
                                {
                                    g.IsUsed = false;
                                    g.UsedDate = null;
                                    g.UsedBy = null;
                                    g.ReceiptItemId = null;
                                    ctx.GeneratedCoupons.Update(g);
                                }
                            }
                        }

                        // Optionally: keep ReceiptItems for audit, or remove them. Here we keep items but you may remove them:
                        // var itemsToRemove = ctx.ReceiptItems.Where(ri => ri.ReceiptId == receipt.ReceiptID);
                        // ctx.ReceiptItems.RemoveRange(itemsToRemove);

                        await ctx.SaveChangesAsync();
                    }
                    catch (Exception exRestore)
                    {
                        // proceed anyway but inform user
                        var err2 = new ContentDialog { Title = "เตือน", Content = $"ยกเลิกใบเสร็จสำเร็จ แต่คืนคูปองล้มเหลว: {exRestore.Message}", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                        await err2.ShowAsync();
                    }

                    // save receipt status
                    await ctx.SaveChangesAsync();

                    // refresh list
                    await LoadReceiptsAsync();
                }
            }
            catch (Exception ex)
            {
                var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = ex.Message, CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                await err.ShowAsync();
            }
        }

        private async void EditButton_Click(object? sender, RoutedEventArgs e)
        {
            var lv = this.FindName("ReceiptsListView") as ListView;
            if (lv?.SelectedItem is not ReceiptRow row)
            {
                var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = "โปรดเลือกรายการก่อน", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                await err.ShowAsync();
                return;
            }

            try
            {
                using var ctx = new CouponContext();
                var receipt = await ctx.Receipts.FirstOrDefaultAsync(r => r.ReceiptID == row.Id);
                if (receipt == null)
                {
                    var err = new ContentDialog { Title = "ไม่พบข้อมูล", Content = "ไม่พบข้อมูลใบเสร็จ", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await err.ShowAsync();
                    return;
                }

                // if already cancelled, inform user and return
                if (string.Equals(receipt.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    var info = new ContentDialog { Title = "ใบนั้นถูกยกเลิกแล้ว", Content = "ใบเสร็จนี้ถูกยกเลิกไปแล้ว ไม่สามารถทำรายการได้", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await info.ShowAsync();
                    return;
                }

                // load salespersons and payment methods
                var salesList = await ctx.SalesPerson.ToListAsync();
                var paymentList = await ctx.PaymentMethods.ToListAsync();

                // build dialog content
                var panel = new StackPanel { Spacing = 12 };

                var title = new TextBlock { Text = $"แก้ไขข้อมูลใบเสร็จ {receipt.ReceiptCode}", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
                panel.Children.Add(title);

                panel.Children.Add(new TextBlock { Text = "ชื่อลูกค้า:" });
                var nameBox = new TextBox { Text = receipt.CustomerName ?? string.Empty, PlaceholderText = "ชื่อลูกค้า" };
                panel.Children.Add(nameBox);

                panel.Children.Add(new TextBlock { Text = "เบอร์โทรศัพท์:" });
                var phoneBox = new TextBox { Text = receipt.CustomerPhoneNumber ?? string.Empty, PlaceholderText = "เบอร์โทรศัพท์" };
                panel.Children.Add(phoneBox);

                panel.Children.Add(new TextBlock { Text = "เซลล์:" });
                var salesCombo = new ComboBox { ItemsSource = salesList, DisplayMemberPath = "Name", SelectedValuePath = "ID", SelectedValue = receipt.SalesPersonId };
                panel.Children.Add(salesCombo);

                panel.Children.Add(new TextBlock { Text = "ประเภทการจ่าย:" });
                var payCombo = new ComboBox { ItemsSource = paymentList, DisplayMemberPath = "Name", SelectedValuePath = "Id", SelectedValue = receipt.PaymentMethodId };
                panel.Children.Add(payCombo);

                panel.Children.Add(new TextBlock { Text = "ส่วนลด (บาท):" });
                var discountBox = new Microsoft.UI.Xaml.Controls.NumberBox { Value = (double)(receipt.Discount), Minimum =0, Maximum =1000000 };
                panel.Children.Add(discountBox);

                decimal totalBeforeDiscount = receipt.TotalAmount + receipt.Discount;
                var beforeText = new TextBlock { Text = $"ยอดรวมก่อนส่วนลด: {totalBeforeDiscount:N2} บาท", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) };
                panel.Children.Add(beforeText);

                var netText = new TextBlock { Text = $"ยอดสุทธิ: {(totalBeforeDiscount - receipt.Discount):N2} บาท", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
                panel.Children.Add(netText);

                discountBox.ValueChanged += (s, args) =>
                {
                    var val = double.IsNaN(discountBox.Value) ?0 : discountBox.Value;
                    var net = totalBeforeDiscount - (decimal)val;
                    netText.Text = $"ยอดสุทธิ: {net:N2} บาท";
                };

                var dialog = new ContentDialog
                {
                    Title = "แก้ไขข้อมูลใบเสร็จ",
                    Content = panel,
                    PrimaryButtonText = "บันทึก",
                    CloseButtonText = "ยกเลิก",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // validate
                    if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(phoneBox.Text))
                    {
                        var err = new ContentDialog { Title = "ผิดพลาด", Content = "กรุณากรอกชื่อและเบอร์โทรศัพท์", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                        await err.ShowAsync();
                        return;
                    }

                    receipt.CustomerName = nameBox.Text.Trim();
                    receipt.CustomerPhoneNumber = phoneBox.Text.Trim();
                    receipt.SalesPersonId = salesCombo.SelectedValue as int? ?? receipt.SalesPersonId;
                    receipt.PaymentMethodId = payCombo.SelectedValue as int? ?? receipt.PaymentMethodId;
                    var newDiscount = (decimal)(double.IsNaN(discountBox.Value) ?0 : discountBox.Value);
                    receipt.Discount = newDiscount;
                    receipt.TotalAmount = totalBeforeDiscount - newDiscount;

                    await ctx.SaveChangesAsync();

                    await LoadReceiptsAsync();
                }
            }
            catch (Exception ex)
            {
                var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = ex.Message, CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                await err.ShowAsync();
            }
        }

        // Add EditItems handler implementation
        private async void EditItemsButton_Click(object? sender, RoutedEventArgs e)
        {
            var lv = this.FindName("ReceiptsListView") as ListView;
            if (lv?.SelectedItem is not ReceiptRow row)
            {
                var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = "โปรดเลือกรายการก่อน", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                await err.ShowAsync();
                return;
            }

            try
            {
                using var ctx = new CouponContext();
                var receipt = await ctx.Receipts.FirstOrDefaultAsync(r => r.ReceiptID == row.Id);
                if (receipt == null)
                {
                    var err = new ContentDialog { Title = "ไม่พบข้อมูล", Content = "ไม่พบข้อมูลใบเสร็จ", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await err.ShowAsync();
                    return;
                }

                if (string.Equals(receipt.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    var info = new ContentDialog { Title = "ใบนั้นถูกยกเลิกแล้ว", Content = "ใบเสร็จนี้ถูกยกเลิกไปแล้ว ไม่สามารถแก้ไขรายการได้", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
                    await info.ShowAsync();
                    return;
                }

                // ReceiptItems reference coupon definitions in this schema; join to CouponDefinitions
                var itemRows = await (from ri in ctx.ReceiptItems
                                      join c in ctx.CouponDefinitions on ri.CouponId equals c.Id into gj
                                      from c in gj.DefaultIfEmpty()
                                      join t in ctx.CouponTypes on (c != null ? c.CouponTypeId :0) equals t.Id into tj
                                      from t in tj.DefaultIfEmpty()
                                      where ri.ReceiptId == receipt.ReceiptID
                                      select new ReceiptItemRow
                                      {
                                          ReceiptItemId = ri.ReceiptItemId,
                                          CouponName = c != null ? c.Name : ("Coupon #" + ri.CouponId),
                                          Quantity = ri.Quantity,
                                          UnitPrice = ri.UnitPrice,
                                          TotalPrice = ri.TotalPrice,
                                          IsComplimentary = false,
                                          CouponCode = c != null ? c.Code : string.Empty,
                                          CouponTypeName = t != null ? t.Name : string.Empty
                                      }).ToListAsync();

                // determine complimentary status per receipt item by checking GeneratedCoupons linked to each ReceiptItem
                var receiptItemIds = itemRows.Select(i => i.ReceiptItemId).ToList();
                if (receiptItemIds.Count >0)
                {
                    var comps = await ctx.GeneratedCoupons
                    .Where(gc => gc.ReceiptItemId != null && receiptItemIds.Contains(gc.ReceiptItemId.Value) && gc.IsComplimentary)
                    .Select(gc => gc.ReceiptItemId!.Value)
                    .Distinct()
                    .ToListAsync();

                    var compSet = new System.Collections.Generic.HashSet<int>(comps);
                    foreach (var it in itemRows)
                    {
                        if (compSet.Contains(it.ReceiptItemId)) it.IsComplimentary = true;
                    }

                    // Load generated codes per receipt item
                    var codesMap = await ctx.GeneratedCoupons
                    .Where(gc => gc.ReceiptItemId != null && receiptItemIds.Contains(gc.ReceiptItemId.Value))
                    .GroupBy(gc => gc.ReceiptItemId!.Value)
                    .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.GeneratedCode).ToList());

                    foreach (var it in itemRows)
                    {
                        if (codesMap.TryGetValue(it.ReceiptItemId, out var codes))
                        {
                            it.GeneratedCodes = codes;
                        }
                    }
                }

                // build dialog content with nicer layout and styling
                var panel = new StackPanel { Spacing =8, Padding = new Thickness(6) };
                // constrain dialog width to make it slightly smaller
                panel.MaxWidth =560;
                panel.MinWidth =420;

                // Header bar: title + action buttons (Adjust COM, Close top-right)
                var headerBar = new Grid { Margin = new Thickness(0,0,0,6) };
                headerBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var titleTb = new TextBlock { Text = "แก้ไขรายการใบเสร็จ", FontSize =18, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(titleTb,0);
                headerBar.Children.Add(titleTb);

                // Buttons on the right
                var headerButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Spacing =6 };
var adjustComBtn = new Button
{
 Content = "ปรับเป็น COM",
 Padding = new Thickness(8,4,8,4),
 Margin = new Thickness(0,0,0,0),
 Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
 Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
 Height =28,
 IsEnabled = false // disabled until a row is selected
};
                // placeholder click - implemented below

var closeTopBtn = new Button
{
 Content = "✖",
 Padding = new Thickness(6,4,6,4),
 Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
 Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
 Height =28
};
Grid.SetColumn(headerButtons,1);
headerButtons.Children.Add(adjustComBtn);
headerButtons.Children.Add(closeTopBtn);
headerBar.Children.Add(headerButtons);

panel.Children.Add(headerBar);

// Section header
panel.Children.Add(new TextBlock { Text = "รายการคูปอง", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0,6,0,6), FontSize =14 });

// Items header row (add COM column) with vertical separators and centered cells
var headerGrid = new Grid { Margin = new Thickness(0,0,12,6) }; // reserve space for scrollbar
// make all columns equal width
headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // coupon type

SolidColorBrush sepBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray);
Thickness rightSep = new Thickness(0,0,1,0);

// Use identical cell padding for header and rows so columns align visually
var cellPadding = new Thickness(4,6,4,6);

Border MakeHeaderCell(string text, int col, bool hasRight = true)
{
 var tb = new TextBlock { Text = text, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize =12 };
 var b = new Border { Child = tb, Padding = cellPadding, BorderBrush = sepBrush, BorderThickness = hasRight ? rightSep : new Thickness(0) };
 Grid.SetColumn(b, col);
 return b;
}

headerGrid.Children.Add(MakeHeaderCell("ชื่อคูปอง",0));
headerGrid.Children.Add(MakeHeaderCell("COM",1));
headerGrid.Children.Add(MakeHeaderCell("จำนวน",2));
headerGrid.Children.Add(MakeHeaderCell("รหัสคูปอง",3));
headerGrid.Children.Add(MakeHeaderCell("ประเภทคูปอง",4, hasRight: false));
panel.Children.Add(headerGrid);

// Items list as selectable ListView to allow row selection
var itemsListView = new ListView
{
 MaxHeight =220,
 SelectionMode = ListViewSelectionMode.Single,
 Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
 Padding = new Thickness(0,0,12,0) // reserve same right padding so columns align with header
};

// remove item padding/margin so row content aligns exactly under header cells
itemsListView.ItemContainerStyle = new Style(typeof(ListViewItem));
itemsListView.ItemContainerStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
itemsListView.ItemContainerStyle.Setters.Add(new Setter(ListViewItem.MarginProperty, new Thickness(0)));
itemsListView.ItemContainerStyle.Setters.Add(new Setter(ListViewItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
itemsListView.ItemContainerStyle.Setters.Add(new Setter(ListViewItem.MinHeightProperty,28.0));

// build visual rows and add as ListViewItem so they can be selected
var comMap = new System.Collections.Generic.Dictionary<int, TextBlock>();
foreach (var ir in itemRows)
{
 var g = new Grid { Padding = new Thickness(0) };
 // make row columns equal to header
 g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
 g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
 g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
 g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
 g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

 Border MakeCell(UIElement child, int col, bool hasRight = true)
 {
 var b = new Border { Child = child, Padding = cellPadding, BorderBrush = sepBrush, BorderThickness = hasRight ? rightSep : new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
 Grid.SetColumn(b, col);
 return b;
 }

 // name (show tooltip with full text on hover)
 var nameTb = new TextBlock { Text = ir.CouponName, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, FontSize =12 };
 ToolTipService.SetToolTip(nameTb, ir.CouponName);
 g.Children.Add(MakeCell(nameTb,0));

 // COM marker
 var comTb = new TextBlock { Text = ir.IsComplimentary ? "COM" : string.Empty, HorizontalAlignment = HorizontalAlignment.Center, Foreground = ir.IsComplimentary ? new SolidColorBrush(Microsoft.UI.Colors.Gold) : new SolidColorBrush(Microsoft.UI.Colors.Gray), FontWeight = ir.IsComplimentary ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, FontSize =12 };
 g.Children.Add(MakeCell(comTb,1));

 // qty
 var qtyTb = new TextBlock { Text = ir.Quantity.ToString(), HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), VerticalAlignment = VerticalAlignment.Center, FontSize =12 };
 g.Children.Add(MakeCell(qtyTb,2));

 // coupon code
 var displayCodes = (ir.GeneratedCodes != null && ir.GeneratedCodes.Count >0) ? string.Join(", ", ir.GeneratedCodes) : ir.CouponCode;
 var codeTb = new TextBlock { Text = displayCodes, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), VerticalAlignment = VerticalAlignment.Center, FontSize =12 };
 ToolTipService.SetToolTip(codeTb, displayCodes);
 g.Children.Add(MakeCell(codeTb,3));

 // coupon type
 var typeTb = new TextBlock { Text = ir.CouponTypeName, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), VerticalAlignment = VerticalAlignment.Center, FontSize =12 };
 ToolTipService.SetToolTip(typeTb, ir.CouponTypeName);
 g.Children.Add(MakeCell(typeTb,4, hasRight: false));

 // wrap row with border for nicer selection visual and bottom separator
 var rowBorder = new Border { Child = g, Padding = new Thickness(0), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) , BorderBrush = sepBrush, BorderThickness = new Thickness(0,0,0,1) };
 // store id on border and map com TextBlock for updates
 rowBorder.Tag = ir.ReceiptItemId;
 comMap[ir.ReceiptItemId] = comTb;

 var lvi = new ListViewItem { Content = rowBorder, Tag = ir.ReceiptItemId };
 itemsListView.Items.Add(lvi);
}

// implement adjust COM button: toggle IsComplimentary and persist to DB for selected items
adjustComBtn.Click += async (s, ev) =>
{
 try
 {
 // handle single selection only
 if (itemsListView.SelectedItem is not ListViewItem sel || sel.Tag is not int rid)
 {
 var info = new ContentDialog { Title = "ข้อมูล", Content = "โปรดเลือกรายการคูปองก่อน", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
 await info.ShowAsync();
 return;
 }

 // find the corresponding row model from the in-memory list
 var rowModelSingle = itemRows.FirstOrDefault(x => x.ReceiptItemId == rid);
 if (rowModelSingle == null)
 {
 var info = new ContentDialog { Title = "ข้อมูล", Content = "ไม่พบรายการที่เลือก", CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
 await info.ShowAsync();
 return;
 }

 // toggle flag
 rowModelSingle.IsComplimentary = !rowModelSingle.IsComplimentary;

 // update DB: set IsComplimentary on generated coupons linked to this receipt item
 using var updateCtx = new CouponContext();
 var linkedSingle = await updateCtx.GeneratedCoupons.Where(gc => gc.ReceiptItemId == rid).ToListAsync();
 if (linkedSingle.Count >0)
 {
 foreach (var g in linkedSingle)
 {
 g.IsComplimentary = rowModelSingle.IsComplimentary;
 updateCtx.GeneratedCoupons.Update(g);
 }
 await updateCtx.SaveChangesAsync();
 }

 // update UI marker
 if (comMap.TryGetValue(rid, out var tbSingle))
 {
 tbSingle.Text = rowModelSingle.IsComplimentary ? "COM" : string.Empty;
 tbSingle.Foreground = rowModelSingle.IsComplimentary ? new SolidColorBrush(Microsoft.UI.Colors.Gold) : new SolidColorBrush(Microsoft.UI.Colors.Gray);
 tbSingle.FontWeight = rowModelSingle.IsComplimentary ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
 }
 }
 catch (Exception ex)
 {
 var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = ex.Message, CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
 await err.ShowAsync();
 }
};

// highlight selected row by changing the inner Border background
itemsListView.SelectionChanged += (s, ev) =>
{
 // enable/disable COM button based on selection
 adjustComBtn.IsEnabled = itemsListView.SelectedItem != null;

 foreach (var obj in itemsListView.Items)
 {
 if (obj is ListViewItem item)
 {
 if (item.Content is Border b)
 {
 b.Background = item.IsSelected ? new SolidColorBrush(Microsoft.UI.Colors.DimGray) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
 }
 }
 }
};

panel.Children.Add(itemsListView);

// wrap panel in an outer border for a dialog card look
var outer = new Border { Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,44,44,44)), CornerRadius = new CornerRadius(10), Padding = new Thickness(12) };
outer.Child = panel;

var dialog = new ContentDialog
{
 Title = string.Empty,
 Content = outer,
 CloseButtonText = string.Empty,
 XamlRoot = this.XamlRoot
};

// wire top close button
closeTopBtn.Click += (s, ev) => dialog.Hide();

await dialog.ShowAsync();
 }
 catch (Exception ex)
 {
 var err = new ContentDialog { Title = "ข้อผิดพลาด", Content = ex.Message, CloseButtonText = "ตกลง", XamlRoot = this.XamlRoot };
 await err.ShowAsync();
 }
 }

 private async Task LoadReceiptsAsync()
 {
 TextBlock? statusTextBlock = this.FindName("StatusText") as TextBlock;
 try
 {
 if (statusTextBlock != null) statusTextBlock.Text = "กำลังโหลด...";
 using var ctx = new CouponContext();

 // apply filters
 string? search = null;
 var searchBox = this.FindName("SearchTextBox") as TextBox;
 if (searchBox != null && !string.IsNullOrWhiteSpace(searchBox.Text)) search = searchBox.Text.Trim();

 string? statusFilter = null;
 var statusCombo = this.FindName("StatusFilterComboBox") as ComboBox;
 if (statusCombo != null && statusCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag != null)
 {
 var tag = cbi.Tag.ToString();
 if (!string.IsNullOrEmpty(tag)) statusFilter = tag;
 }

 // coupon type filter
 string? couponTypeFilter = null;
 var couponTypeCombo = this.FindName("CouponTypeFilterComboBox") as ComboBox;
 if (couponTypeCombo != null && couponTypeCombo.SelectedItem is ComboBoxItem ctbi && ctbi.Tag != null)
 {
 var tag = ctbi.Tag.ToString();
 if (!string.IsNullOrEmpty(tag)) couponTypeFilter = tag;
 }

 var query = ctx.Receipts.AsQueryable();

 if (!string.IsNullOrEmpty(statusFilter))
 {
 query = query.Where(r => r.Status == statusFilter);
 }

 if (!string.IsNullOrEmpty(search))
 {
 var s = search.ToLower();

 // also search receipts by coupon code (either CouponDefinitions.Code or GeneratedCoupons.GeneratedCode)
 var receiptIdsByCode = await (from ri in ctx.ReceiptItems
 join cd in ctx.CouponDefinitions on ri.CouponId equals cd.Id into cdg
 from cd in cdg.DefaultIfEmpty()
 join gc in ctx.GeneratedCoupons on ri.ReceiptItemId equals gc.ReceiptItemId into gcg
 from gc in gcg.DefaultIfEmpty()
 where (cd != null && (cd.Code ?? "").ToLower().Contains(s)) || (gc != null && (gc.GeneratedCode ?? "").ToLower().Contains(s))
 select ri.ReceiptId).Distinct().ToListAsync();

 query = query.Where(r => (r.CustomerName ?? "").ToLower().Contains(s) || (r.ReceiptCode ?? "").ToLower().Contains(s) || (r.CustomerPhoneNumber ?? "").ToLower().Contains(s) || receiptIdsByCode.Contains(r.ReceiptID));
 }

 // apply coupon type filter by finding receipts that contain receipt items whose coupon definition has the selected type
 if (!string.IsNullOrEmpty(couponTypeFilter))
 {
 if (int.TryParse(couponTypeFilter, out var typeId))
 {
 var matchingReceiptIds = await (from ri in ctx.ReceiptItems
 join cd in ctx.CouponDefinitions on ri.CouponId equals cd.Id
 where cd.CouponTypeId == typeId
 select ri.ReceiptId).Distinct().ToListAsync();

 if (!matchingReceiptIds.Any())
 {
 // no receipts match -> clear rows and return
 _rows.Clear();
 if (statusTextBlock != null) statusTextBlock.Text = "โหลดข้อมูลเรียบร้อย :0 รายการ";
 return;
 }

 query = query.Where(r => matchingReceiptIds.Contains(r.ReceiptID));
 }
 }

 var receipts = await query.OrderByDescending(r => r.ReceiptID).ToListAsync();

 _rows.Clear();
 foreach (var r in receipts)
 {
 // Load related names safely
 string salesName = string.Empty;
 if (r.SalesPersonId.HasValue)
 {
 var sp = await ctx.SalesPerson.FindAsync(r.SalesPersonId.Value);
 salesName = sp?.Name ?? string.Empty;
 }

 string paymentName = string.Empty;
 if (r.PaymentMethodId.HasValue)
 {
 var pm = await ctx.PaymentMethods.FindAsync(r.PaymentMethodId.Value);
 paymentName = pm?.Name ?? string.Empty;
 }

 _rows.Add(new ReceiptRow
 {
 Id = r.ReceiptID,
 ReceiptCode = r.ReceiptCode ?? string.Empty,
 CustomerName = r.CustomerName ?? string.Empty,
 CustomerPhone = r.CustomerPhoneNumber ?? string.Empty,
 ReceiptDate = r.ReceiptDate,
 ReceiptDateString = r.ReceiptDate.ToString("dd/MM/yyyy HH:mm"),
 SalesPersonName = salesName,
 PaymentMethodName = paymentName,
 Status = r.Status ?? string.Empty
 });
 }

 if (statusTextBlock != null) statusTextBlock.Text = $"โหลดข้อมูลเรียบร้อย : {_rows.Count} รายการ";
 }
 catch (Exception)
 {
 if (statusTextBlock != null) statusTextBlock.Text = "เกิดข้อผิดพลาดในการโหลด";
 }
 }

 private async Task LoadCouponTypesAsync()
 {
 try
 {
 var combo = this.FindName("CouponTypeFilterComboBox") as ComboBox;
 if (combo == null) return;

 using var ctx = new CouponContext();
 var types = await ctx.CouponTypes.OrderBy(ct => ct.Name).ToListAsync();

 // Clear existing items except the default first item
 combo.Items.Clear();

 // Add default All item
 var allItem = new ComboBoxItem { Tag = string.Empty, Content = "-- ทั้งหมด --" };
 combo.Items.Add(allItem);

 foreach (var t in types)
 {
 var item = new ComboBoxItem { Tag = t.Id.ToString(), Content = t.Name };
 combo.Items.Add(item);
 }

 // select default
 combo.SelectedIndex =0;
 }
 catch
 {
 // ignore errors silently
 }
 }
 }
}
