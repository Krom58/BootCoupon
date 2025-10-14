using CouponManagement.Shared.Models;
using CouponManagement.Shared.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using System.Threading;
using System.Diagnostics;
using CouponManagement.Dialogs; // for CouponDetailDialog
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace CouponManagement
{
    public sealed partial class GeneratedCouponsPage : Page, INotifyPropertyChanged
    {
        // P/Invoke to get the active window handle; avoids cross-project App reference
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private readonly GeneratedCouponService _generatedCouponService;
        private readonly CouponDefinitionService _definitionService;
        private readonly ObservableCollection<GeneratedCouponDisplayModel> _coupons;
        private readonly ObservableCollection<CouponDefinition> _definitions;

        // Pagination and filtering
        private int _currentPage = 1;
        private const int _pageSize = 50;
        private int _totalPages = 1;
        private int _totalCount = 0;

        // Search debouncing
        private Timer? _searchTimer;
        private readonly object _searchLock = new object();

        // Filter parameters
        private int? _selectedDefinitionId;
        private string? _searchCode;
        private bool? _isUsedFilter;
        private DateTime? _createdFrom;
        private DateTime? _createdTo;
        private string? _createdBy;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public bool ShowStatistics => _selectedDefinitionId.HasValue;

        // Helper to update selected definition id and notify UI
        private void UpdateSelectedDefinitionId(int? id)
        {
            if (_selectedDefinitionId != id)
            {
                _selectedDefinitionId = id;
                OnPropertyChanged(nameof(ShowStatistics));
            }
        }

        public GeneratedCouponsPage()
        {
            this.InitializeComponent();
            _generatedCouponService = new GeneratedCouponService();
            _definitionService = new CouponDefinitionService();
            _coupons = new ObservableCollection<GeneratedCouponDisplayModel>();
            _definitions = new ObservableCollection<CouponDefinition>();

            CouponsListView.ItemsSource = _coupons;
            DefinitionComboBox.ItemsSource = _definitions;

            this.Loaded += GeneratedCouponsPage_Loaded;
            this.Unloaded += GeneratedCouponsPage_Unloaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // If navigated with a specific definition ID, set it as filter
            if (e.Parameter is int definitionId)
            {
                UpdateSelectedDefinitionId(definitionId);
            }
        }

        private async void GeneratedCouponsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadDefinitionsAsync();
                await LoadDataAsync();
                UpdateLastUpdatedText();
            }
            catch (Exception ex)
            {
                // Log and show a friendly error dialog instead of crashing
                Debug.WriteLine($"Error in GeneratedCouponsPage_Loaded: {ex}");
                try { await ShowErrorAsync("ข้อผิดพลาดในการโหลดหน้า", ex.Message); } catch { }
            }
        }

        private void GeneratedCouponsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _searchTimer?.Dispose();
            // Services no longer implement IDisposable; do not attempt to dispose.
        }

        private async Task LoadDefinitionsAsync()
        {
            try
            {
                var definitions = await _definitionService.GetAllAsync();
                _definitions.Clear();
                
                // Add "All" option
                _definitions.Add(new CouponDefinition { Id = 0, Code = "ทั้งหมด", Name = "แสดงทุกคำนิยาม" });
                
                foreach (var def in definitions.OrderBy(d => d.Code))
                {
                    _definitions.Add(def);
                }

                // Set initial selection
                if (_selectedDefinitionId.HasValue)
                {
                    var selectedDef = _definitions.FirstOrDefault(d => d.Id == _selectedDefinitionId.Value);
                    if (selectedDef != null)
                    {
                        DefinitionComboBox.SelectedItem = selectedDef;
                        UpdateSelectedDefinitionId(selectedDef.Id);
                    }
                }
                else
                {
                    DefinitionComboBox.SelectedIndex = 0; // Select "All"
                    UpdateSelectedDefinitionId(null);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ข้อผิดพลาดในการโหลดข้อมูลคำนิยาม", ex.Message);
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                ShowLoading(true);
                UpdateStatus("กำลังโหลดข้อมูลคูปอง...");

                var result = await _generatedCouponService.GetGeneratedCouponsAsync(
                    _currentPage, _pageSize, _selectedDefinitionId, _searchCode, 
                    _isUsedFilter, _createdFrom, _createdTo, _createdBy);

                _coupons.Clear();
                foreach (var coupon in result.Items.OrderBy(c => c.GeneratedCode))
                {
                    _coupons.Add(coupon);
                }

                _totalCount = result.TotalCount;
                _totalPages = result.TotalPages;

                UpdatePagination();
                UpdateResultCount();
                await UpdateStatisticsAsync();
                UpdateEmptyState();
                UpdateStatus($"โหลดข้อมูลเรียบร้อย - พบ {_totalCount:N0} รายการ");
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ข้อผิดพลาดในการโหลดข้อมูล", ex.Message);
                UpdateStatus("เกิดข้อผิดพลาดในการโหลดข้อมูล");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private async Task UpdateStatisticsAsync()
        {
            if (!_selectedDefinitionId.HasValue || _selectedDefinitionId.Value == 0)
            {
                StatisticsTextBlock.Text = "";
                return;
            }

            try
            {
                var stats = await _generatedCouponService.GetStatisticsByDefinitionAsync(_selectedDefinitionId.Value);
                StatisticsTextBlock.Text = $"สร้างแล้ว: {stats.TotalGenerated:N0} | ใช้แล้ว: {stats.TotalUsed:N0} | คงเหลือ: {stats.TotalAvailable:N0}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating statistics: {ex.Message}");
                StatisticsTextBlock.Text = "ไม่สามารถโหลดสถิติได้";
            }
        }

        private void UpdatePagination()
        {
            PreviousPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < _totalPages;
            PageInfoTextBlock.Text = $"หน้า {_currentPage} จาก {_totalPages}";
        }

        private void UpdateResultCount()
        {
            var startItem = (_currentPage - 1) * _pageSize + 1;
            var endItem = Math.Min(_currentPage * _pageSize, _totalCount);
            
            if (_totalCount == 0)
            {
                ResultCountTextBlock.Text = "ไม่พบรายการ";
            }
            else
            {
                ResultCountTextBlock.Text = $"แสดง {startItem:N0}-{endItem:N0} จาก {_totalCount:N0} รายการ";
            }
        }

        private void UpdateEmptyState()
        {
            EmptyStatePanel.Visibility = _coupons.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CouponsListView.Visibility = _coupons.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowLoading(bool show)
        {
            LoadingPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            CouponsListView.Visibility = show ? Visibility.Collapsed : (_coupons.Count > 0 ? Visibility.Visible : Visibility.Collapsed);
            EmptyStatePanel.Visibility = show ? Visibility.Collapsed : (_coupons.Count == 0 ? Visibility.Visible : Visibility.Collapsed);
        }

        private void UpdateStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        private void UpdateLastUpdatedText()
        {
            LastUpdatedTextBlock.Text = $"อัปเดตล่าสุด: {DateTime.Now:HH:mm:ss}";
        }

        private void PerformDelayedSearch()
        {
            lock (_searchLock)
            {
                _searchTimer?.Dispose();
                _searchTimer = new Timer(_ =>
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        _currentPage = 1; // Reset to first page
                        await LoadDataAsync();
                    });
                }, null, 500, Timeout.Infinite);
            }
        }

        // Event Handlers
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(CouponDefinitionPage));
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
            UpdateLastUpdatedText();
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new FileSavePicker();
                var hWnd = GetForegroundWindow();
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("CSV Files", new[] { ".csv" });
                savePicker.SuggestedFileName = $"GeneratedCoupons_{DateTime.Now:yyyyMMdd_HHmmss}";

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    UpdateStatus("กำลังส่งออกข้อมูล...");
                    
                    var csvData = await _generatedCouponService.ExportToCsvAsync(
                        _selectedDefinitionId, _searchCode, _isUsedFilter, _createdFrom, _createdTo);
                    
                    await FileIO.WriteBytesAsync(file, csvData);
                    
                    UpdateStatus($"ส่งออกข้อมูลเรียบร้อย - {file.Name}");
                    
                    // Open file location
                    await Launcher.LaunchFolderAsync(await file.GetParentAsync());
                }
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ข้อผิดพลาดในการส่งออกข้อมูล", ex.Message);
                UpdateStatus("ส่งออกข้อมูลไม่สำเร็จ");
            }
        }

        private async void ApplyFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            await LoadDataAsync();
        }

        private async void ClearFilterButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all filters
            DefinitionComboBox.SelectedIndex = 0;
            SearchCodeTextBox.Text = string.Empty;
            StatusComboBox.SelectedIndex = 0; // set to 'All'
            CreatedByTextBox.Text = string.Empty;
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;

            UpdateSelectedDefinitionId(null);
            _searchCode = null;
            _isUsedFilter = null;
            _createdFrom = null;
            _createdTo = null;
            _createdBy = null;
            _currentPage = 1;

            await LoadDataAsync();
        }

        private async void PreviousPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await LoadDataAsync();
            }
        }

        private async void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                await LoadDataAsync();
            }
        }

        private void DefinitionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DefinitionComboBox.SelectedItem is CouponDefinition definition)
            {
                // If user selects the 'All' placeholder with Id == 0, treat as no filter
                UpdateSelectedDefinitionId(definition.Id == 0 ? null : definition.Id);
                PerformDelayedSearch();
            }
            else
            {
                // If no proper item is selected (e.g., cleared), reset filter
                UpdateSelectedDefinitionId(null);
                PerformDelayedSearch();
            }
        }

        private void SearchCodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchCode = string.IsNullOrWhiteSpace(SearchCodeTextBox.Text) ? null : SearchCodeTextBox.Text.Trim();
            PerformDelayedSearch();
        }

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // When the 'All' option is selected (Tag == ""), set _isUsedFilter to null
            if (StatusComboBox.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(tag))
                {
                    _isUsedFilter = null;
                }
                else
                {
                    // ensure parsing only when a valid boolean tag is provided
                    if (bool.TryParse(tag, out var parsed))
                        _isUsedFilter = parsed;
                    else
                        _isUsedFilter = null;
                }
                PerformDelayedSearch();
            }
        }

        private void CreatedByTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _createdBy = string.IsNullOrWhiteSpace(CreatedByTextBox.Text) ? null : CreatedByTextBox.Text.Trim();
            PerformDelayedSearch();
        }

        private void StartDatePicker_DateChanged(object sender, DatePickerValueChangedEventArgs e)
        {
            _createdFrom = StartDatePicker.SelectedDate?.Date;
            PerformDelayedSearch();
        }

        private void EndDatePicker_DateChanged(object sender, DatePickerValueChangedEventArgs e)
        {
            _createdTo = EndDatePicker.SelectedDate?.Date.AddDays(1).AddSeconds(-1);
            PerformDelayedSearch();
        }

        private async void CouponsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is GeneratedCouponDisplayModel coupon)
            {
                await ShowCouponDetailsAsync(coupon);
            }
        }

        private async Task ShowCouponDetailsAsync(GeneratedCouponDisplayModel coupon)
        {
            try
            {
                var fullCoupon = await _generatedCouponService.GetByIdAsync(coupon.Id);
                if (fullCoupon == null)
                {
                    await ShowErrorAsync("ข้อผิดพลาด", "ไม่พบข้อมูลคูปอง");
                    return;
                }

                var dialog = new CouponDetailDialog(fullCoupon);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ข้อผิดพลาดในการแสดงรายละเอียด", ex.Message);
            }
        }

        private async Task ShowErrorAsync(string title, string message)
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
    }
}