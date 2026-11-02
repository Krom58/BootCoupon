using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using CouponManagement.Shared.Models;
using CouponManagement.Shared.Services;

namespace CouponManagement
{
    public sealed partial class UnusedCouponDefinitionsPage : Page
    {
        private readonly CouponDefinitionService _definitionService = new();
        private readonly CouponService _couponService = new();

        private readonly ObservableCollection<CleanupCouponDefinitionItem> _eligibleItems = new();
        private readonly ObservableCollection<CouponDefinitionDeletionHistory> _historyItems = new();

        private string? _currentSearchText;
        private int? _selectedBranchId;
        private bool _isHistoryTabSelected = false;
        private bool _isInitialized = false;
        private bool _suppressBranchFilterChanged = false;
        private bool _suppressSelectAllChanged = false;
        private bool _isUpdatingSelection = false;

        public UnusedCouponDefinitionsPage()
        {
            this.InitializeComponent();

            EligibleListView.ItemsSource = _eligibleItems;
            HistoryListView.ItemsSource = _historyItems;

            this.Loaded += UnusedCouponDefinitionsPage_Loaded;
        }

        private async void UnusedCouponDefinitionsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            UpdateTabStyles();
            await LoadBranchesAsync();
            await LoadEligibleDefinitionsAsync();
        }

        #region Navigation & Tabs

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(MainPage));
            }
        }

        private void EligibleTabButton_Click(object sender, RoutedEventArgs e)
        {
            _isHistoryTabSelected = false;
            EligibleDefinitionsView.Visibility = Visibility.Visible;
            HistoryView.Visibility = Visibility.Collapsed;
            UpdateTabStyles();
        }

        private async void HistoryTabButton_Click(object sender, RoutedEventArgs e)
        {
            _isHistoryTabSelected = true;
            EligibleDefinitionsView.Visibility = Visibility.Collapsed;
            HistoryView.Visibility = Visibility.Visible;
            UpdateTabStyles();

            await LoadHistoryAsync();
        }

        private void UpdateTabStyles()
        {
            var accentBrush = (SolidColorBrush)Application.Current.Resources["SystemControlBackgroundAccentBrush"];
            var subtleBrush = new SolidColorBrush(Colors.Transparent);
            var whiteBrush = new SolidColorBrush(Colors.White);
            var primaryTextBrush = (SolidColorBrush)Application.Current.Resources["TextFillColorPrimaryBrush"];

            if (!_isHistoryTabSelected)
            {
                EligibleTabButton.Background = accentBrush;
                EligibleTabButton.Foreground = whiteBrush;

                HistoryTabButton.Background = subtleBrush;
                HistoryTabButton.Foreground = primaryTextBrush;
            }
            else
            {
                EligibleTabButton.Background = subtleBrush;
                EligibleTabButton.Foreground = primaryTextBrush;

                HistoryTabButton.Background = accentBrush;
                HistoryTabButton.Foreground = whiteBrush;
            }
        }

        private async void RefreshCurrentTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isHistoryTabSelected)
            {
                await LoadEligibleDefinitionsAsync();
            }
            else
            {
                await LoadHistoryAsync();
            }
        }

        #endregion

        #region Branch & Search Filtering

        private async Task LoadBranchesAsync()
        {
            try
            {
                _suppressBranchFilterChanged = true;

                var branches = await _couponService.GetAllBranchesAsync();
                BranchFilterComboBox.Items.Clear();

                var allItem = new ComboBoxItem { Content = "ทุกสาขา", Tag = 0 };
                BranchFilterComboBox.Items.Add(allItem);

                foreach (var branch in branches.OrderBy(b => b.Name))
                {
                    BranchFilterComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = branch.Name,
                        Tag = branch.Id
                    });
                }

                BranchFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowInfoBar(InfoBarSeverity.Error, "โหลดข้อมูลสาขาล้มเหลว", ex.Message);
            }
            finally
            {
                _suppressBranchFilterChanged = false;
            }
        }

        private async void BranchFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _suppressBranchFilterChanged) return;

            if (BranchFilterComboBox?.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                if (int.TryParse(item.Tag.ToString(), out int branchId))
                {
                    _selectedBranchId = branchId > 0 ? branchId : null;
                    await LoadEligibleDefinitionsAsync();
                }
            }
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitialized) return;

            _currentSearchText = SearchTextBox?.Text?.Trim();
            await LoadEligibleDefinitionsAsync();
        }

        private async void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox != null) SearchTextBox.Text = string.Empty;
            _currentSearchText = null;

            _suppressBranchFilterChanged = true;
            if (BranchFilterComboBox != null) BranchFilterComboBox.SelectedIndex = 0;
            _selectedBranchId = null;
            _suppressBranchFilterChanged = false;

            await LoadEligibleDefinitionsAsync();
        }

        #endregion

        #region Multi-Selection Handling

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllChanged || _isUpdatingSelection) return;

            _isUpdatingSelection = true;
            try
            {
                foreach (var item in _eligibleItems)
                {
                    item.IsSelected = true;
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }
            UpdateSelectionUi();
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectAllChanged || _isUpdatingSelection) return;

            _isUpdatingSelection = true;
            try
            {
                foreach (var item in _eligibleItems)
                {
                    item.IsSelected = false;
                }
            }
            finally
            {
                _isUpdatingSelection = false;
            }
            UpdateSelectionUi();
        }

        private void ItemCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSelection) return;

            if (sender is CheckBox cb && cb.DataContext is CleanupCouponDefinitionItem item)
            {
                item.IsSelected = cb.IsChecked == true;
            }

            UpdateSelectionUi();
        }

        private void UpdateSelectionUi()
        {
            int selectedCount = _eligibleItems.Count(i => i.IsSelected);

            if (DeleteSelectedButton != null)
            {
                DeleteSelectedButton.IsEnabled = selectedCount > 0;
            }

            if (DeleteSelectedButtonText != null)
            {
                DeleteSelectedButtonText.Text = $"ลบรายการที่เลือก ({selectedCount:N0})";
            }

            if (SelectAllCheckBox != null)
            {
                _suppressSelectAllChanged = true;
                if (_eligibleItems.Count == 0)
                {
                    SelectAllCheckBox.IsChecked = false;
                }
                else if (selectedCount == _eligibleItems.Count)
                {
                    SelectAllCheckBox.IsChecked = true;
                }
                else if (selectedCount == 0)
                {
                    SelectAllCheckBox.IsChecked = false;
                }
                else
                {
                    SelectAllCheckBox.IsChecked = null; // Indeterminate
                }
                _suppressSelectAllChanged = false;
            }
        }

        #endregion

        #region Data Loading

        private async Task LoadEligibleDefinitionsAsync()
        {
            try
            {
                if (LoadingRing != null) LoadingRing.IsActive = true;
                if (EmptyStatePanel != null) EmptyStatePanel.Visibility = Visibility.Collapsed;

                var list = await _definitionService.GetCleanupEligibleDefinitionsAsync(_currentSearchText, _selectedBranchId);

                _eligibleItems.Clear();
                foreach (var item in list)
                {
                    _eligibleItems.Add(item);
                }

                int totalCount = list.Count;
                int notCreatedCount = list.Count(i => !i.HasGeneratedCoupons);
                int createdCount = list.Count(i => i.HasGeneratedCoupons);

                if (SummaryCountTextBlock != null)
                {
                    SummaryCountTextBlock.Text = $"พบทั้งหมด {totalCount:N0} รายการ (ยังไม่เคยสร้างคูปอง: {notCreatedCount:N0} รายการ, สร้างแล้วแต่ไม่เคยใช้: {createdCount:N0} รายการ)";
                }

                if (EmptyStatePanel != null)
                {
                    EmptyStatePanel.Visibility = totalCount == 0 ? Visibility.Visible : Visibility.Collapsed;
                }

                UpdateSelectionUi();
            }
            catch (Exception ex)
            {
                ShowInfoBar(InfoBarSeverity.Error, "ไม่สามารถดึงข้อมูลคำนิยามคูปองได้", ex.Message);
            }
            finally
            {
                if (LoadingRing != null) LoadingRing.IsActive = false;
            }
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                if (HistoryLoadingRing != null) HistoryLoadingRing.IsActive = true;
                if (HistoryEmptyStatePanel != null) HistoryEmptyStatePanel.Visibility = Visibility.Collapsed;

                var histories = await _definitionService.GetDeletionHistoriesAsync(1, 200);

                _historyItems.Clear();
                foreach (var h in histories)
                {
                    _historyItems.Add(h);
                }

                if (HistoryCountTextBlock != null)
                {
                    HistoryCountTextBlock.Text = $"ประวัติการลบทั้งหมด ({histories.Count:N0} รายการ)";
                }

                if (HistoryEmptyStatePanel != null)
                {
                    HistoryEmptyStatePanel.Visibility = histories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                ShowInfoBar(InfoBarSeverity.Error, "ไม่สามารถดึงประวัติการลบได้", ex.Message);
            }
            finally
            {
                if (HistoryLoadingRing != null) HistoryLoadingRing.IsActive = false;
            }
        }

        #endregion

        #region Delete Action (Single & Batch)

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not CleanupCouponDefinitionItem item)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "⚠️ ยืนยันการลบคำนิยามคูปอง",
                PrimaryButtonText = "ยืนยันการลบข้อมูล",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var stack = new StackPanel { Spacing = 12 };

            stack.Children.Add(new TextBlock
            {
                Text = $"คุณต้องการลบคำนิยามคูปองรหัส '{item.Code}' ใช่หรือไม่?",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 15
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"• ชื่อคำนิยาม: {item.Name}\n" +
                       $"• สาขา: {item.BranchName}\n" +
                       $"• งานขาย: {item.SaleEventName} (งานสิ้นสุดแล้ว)\n" +
                       $"• วันหมดอายุ: {item.ValidTo:dd/MM/yyyy} (หมดอายุแล้ว)\n" +
                       $"• ราคา: {item.Price:N2} บาท",
                FontSize = 13,
                Foreground = new SolidColorBrush(Colors.SlateGray)
            });

            var alertBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 4, 0, 0)
            };

            if (item.HasGeneratedCoupons)
            {
                alertBorder.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 254, 242, 242));
                alertBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 248, 113, 113));
                alertBorder.BorderThickness = new Thickness(1);

                var alertText = new TextBlock
                {
                    Text = $"🚨 คำนิยามนี้มีการสร้างคูปองไว้ {item.TotalGeneratedCount:N0} ใบ (ตรวจสอบแล้วว่าไม่มีใบใดผูกกับใบเสร็จ)\n" +
                           $"เมื่อยืนยัน ระบบจะทำการลบทั้ง 'คำนิยามคูปอง' และ 'คูปองที่สร้างไว้ทั้ง {item.TotalGeneratedCount:N0} ใบ' ออกจากฐานข้อมูลถาวร และบันทึกประวัติการลบไว้",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 153, 27, 27)),
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                };
                alertBorder.Child = alertText;
            }
            else
            {
                alertBorder.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 243, 244, 246));
                alertBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 209, 213, 219));
                alertBorder.BorderThickness = new Thickness(1);

                var alertText = new TextBlock
                {
                    Text = "ℹ️ คำนิยามนี้ยังไม่เคยถูกสร้างคูปอง (0 ใบ)\nระบบจะทำการลบเฉพาะคำนิยามคูปองออกจากฐานข้อมูล และบันทึกประวัติการลบไว้",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 55, 65, 81)),
                    TextWrapping = TextWrapping.Wrap
                };
                alertBorder.Child = alertText;
            }

            stack.Children.Add(alertBorder);
            dialog.Content = stack;

            var dialogResult = await dialog.ShowAsync();
            if (dialogResult != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                if (LoadingRing != null) LoadingRing.IsActive = true;
                var (success, message) = await _definitionService.DeleteCleanupDefinitionAsync(item.Id, Environment.UserName);

                if (success)
                {
                    ShowInfoBar(InfoBarSeverity.Success, "ลบข้อมูลสำเร็จ", message);
                    await LoadEligibleDefinitionsAsync();
                    if (_isHistoryTabSelected)
                    {
                        await LoadHistoryAsync();
                    }
                }
                else
                {
                    ShowInfoBar(InfoBarSeverity.Error, "ไม่สามารถลบข้อมูลได้", message);
                }
            }
            catch (Exception ex)
            {
                ShowInfoBar(InfoBarSeverity.Error, "เกิดข้อผิดพลาดในการลบข้อมูล", ex.Message);
            }
            finally
            {
                if (LoadingRing != null) LoadingRing.IsActive = false;
            }
        }

        private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _eligibleItems.Where(i => i.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                return;
            }

            int totalDefinitions = selectedItems.Count;
            int notCreatedCount = selectedItems.Count(i => !i.HasGeneratedCoupons);
            int createdCount = selectedItems.Count(i => i.HasGeneratedCoupons);
            int totalCoupons = selectedItems.Sum(i => i.TotalGeneratedCount);

            var dialog = new ContentDialog
            {
                Title = "⚠️ ยืนยันการลบคำนิยามคูปองแบบกลุ่ม",
                PrimaryButtonText = $"ยืนยันการลบ {totalDefinitions:N0} รายการ",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var stack = new StackPanel { Spacing = 12 };

            stack.Children.Add(new TextBlock
            {
                Text = $"คุณต้องการลบคำนิยามคูปองที่เลือกทั้งหมด {totalDefinitions:N0} รายการ ใช่หรือไม่?",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 15
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"• คำนิยามที่ยังไม่เคยสร้างคูปอง: {notCreatedCount:N0} รายการ\n" +
                       $"• คำนิยามที่สร้างคูปองแล้ว (ไม่เคยผูกใบเสร็จ): {createdCount:N0} รายการ\n" +
                       $"• จำนวนคูปองทั้งหมดที่จะถูกลบ: {totalCoupons:N0} ใบ",
                FontSize = 13,
                Foreground = new SolidColorBrush(Colors.SlateGray)
            });

            var alertBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 254, 242, 242)),
                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 248, 113, 113)),
                BorderThickness = new Thickness(1)
            };

            var alertText = new TextBlock
            {
                Text = $"🚨 คำเตือน: ระบบจะทำการลบคำนิยามทั้ง {totalDefinitions:N0} รายการ และคูปองทั้งหมด {totalCoupons:N0} ใบออกจากฐานข้อมูลอย่างถาวร!\n" +
                       "ข้อมูลที่ถูกลบจะไม่สามารถกู้คืนได้ และระบบจะทำการบันทึกประวัติการลบทั้งหมดไว้",
                FontSize = 13,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 153, 27, 27)),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            alertBorder.Child = alertText;
            stack.Children.Add(alertBorder);

            dialog.Content = stack;

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                if (LoadingRing != null) LoadingRing.IsActive = true;

                var (successCount, failCount, totalCouponsDeleted, errorMessages) = 
                    await _definitionService.DeleteCleanupDefinitionsBatchAsync(
                        selectedItems.Select(i => i.Id), 
                        Environment.UserName);

                if (successCount > 0)
                {
                    string msg = $"ลบสำเร็จ {successCount:N0} รายการ (ลบคูปองทั้งหมด {totalCouponsDeleted:N0} ใบ)";
                    if (failCount > 0)
                    {
                        msg += $", ไม่สำเร็จ {failCount:N0} รายการ";
                    }
                    ShowInfoBar(InfoBarSeverity.Success, "ลบข้อมูลสำเร็จ", msg);

                    await LoadEligibleDefinitionsAsync();
                    if (_isHistoryTabSelected)
                    {
                        await LoadHistoryAsync();
                    }
                }
                else
                {
                    string errorDetail = string.Join("\n", errorMessages);
                    ShowInfoBar(InfoBarSeverity.Error, "ไม่สามารถลบข้อมูลได้", errorDetail);
                }
            }
            catch (Exception ex)
            {
                ShowInfoBar(InfoBarSeverity.Error, "เกิดข้อผิดพลาดในการลบข้อมูล", ex.Message);
            }
            finally
            {
                if (LoadingRing != null) LoadingRing.IsActive = false;
            }
        }

        #endregion

        #region Status Messages

        private void ShowInfoBar(InfoBarSeverity severity, string title, string message)
        {
            if (StatusInfoBar != null)
            {
                StatusInfoBar.Severity = severity;
                StatusInfoBar.Title = title;
                StatusInfoBar.Message = message;
                StatusInfoBar.IsOpen = true;
            }
        }

        #endregion
    }
}
