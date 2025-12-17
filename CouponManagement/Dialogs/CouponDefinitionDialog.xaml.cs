using System.Globalization;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using CouponManagement.Shared.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CouponManagement.Dialogs
{
    public sealed partial class CouponDefinitionDialog : ContentDialog, INotifyPropertyChanged
    {
        private readonly CouponDefinitionService _service;
        private readonly CouponService _couponService;
        private readonly CouponDefinition? _editingDefinition;
        private readonly bool _isEditMode;
        private bool _operationSuccess = false;
        private bool _isSaving = false;

        public bool OperationSuccess => _operationSuccess;
        public bool WasSaved => _operationSuccess;

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // เพิ่ม property ใหม่ด้านบนของคลาส
        private bool _isEventSelected;
        public bool IsEventSelected
        {
            get => _isEventSelected;
            set
            {
                if (_isEventSelected != value)
                {
                    _isEventSelected = value;
                    // Trigger binding update ถ้าใช้ x:Bind
                    OnPropertyChanged(nameof(IsEventSelected));
                }
            }
        }

        public CouponDefinitionDialog()
        {
            this.InitializeComponent();
            _service = new CouponDefinitionService();
            _couponService = new CouponService();
            _isEditMode = false;
            _editingDefinition = null;
            InitializeForm();

            // Load coupon types from database
            _ = LoadBranchTypesAsync();
            
            // **เพิ่ม: โหลดงานที่ออกขาย**
            _ = LoadSaleEventsAsync();
        }

        public CouponDefinitionDialog(CouponDefinition editingDefinition)
        {
            this.InitializeComponent();
            _service = new CouponDefinitionService();
            _couponService = new CouponService();
            _editingDefinition = editingDefinition;
            _isEditMode = true;
            InitializeForm();

            // Load coupon types from database
            _ = LoadBranchTypesAsync();
            
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
            {
                await LoadDataForEditAsync();
            });
        }

        private async Task LoadBranchTypesAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("LoadBranchTypesAsync started");

                var types = await _coupon_service_get_all_types_async();
                System.Diagnostics.Debug.WriteLine($"Retrieved {types.Count} branch types");

                if (TypeComboBox != null)
                {
                    TypeComboBox.Items.Clear();

                    // If there are no types, add a placeholder 'ไม่ระบุ' so that the dialog shows a clear state
                    // but does not create any DB records. The placeholder has no Tag (or Tag != CouponType)
                    // so BuildCreateCouponRequest will leave CouponTypeId as0 and validation will prevent saving.
                    if (types.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("No Branch types found - adding placeholder 'ไม่ระบุ'");
                        TypeComboBox.Items.Add(new ComboBoxItem { Content = "ไม่ระบุ", Tag = null, IsSelected = true });
                    }

                    // Add items; store actual CouponType in Tag so we can read Id/Name reliably
                    foreach (var t in types)
                    {
                        var item = new ComboBoxItem { Content = t.Name, Tag = t };
                        TypeComboBox.Items.Add(item);
                        System.Diagnostics.Debug.WriteLine($"Added BranchType: {t.Name} (ID: {t.Id})");
                    }

                    if (TypeComboBox.Items.Count > 0 && TypeComboBox.SelectedIndex < 0)
                    {
                        TypeComboBox.SelectedIndex = 0;
                        System.Diagnostics.Debug.WriteLine($"Selected default item: {TypeComboBox.SelectedIndex}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("TypeComboBox is null!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Branch types: {ex}");
            }
        }

        // wrapper to call service and guard exceptions when DB is unavailable
        private async Task<List<Branch>> _coupon_service_get_all_types_async()
        {
            try
            {
                return await _couponService.GetAllBranchesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching Branch types from DB: {ex.Message}");
                // return empty list to avoid crashing UI
                return new List<Branch>();
            }
        }

        private async Task LoadSaleEventsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("LoadSaleEventsAsync started");

                using var context = new CouponContext();
                var events = await context.SaleEvents
                    .Where(e => e.IsActive)
                    .OrderByDescending(e => e.StartDate)
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"Retrieved {events.Count} sale events");

                if (SaleEventComboBox != null)
                {
                    SaleEventComboBox.Items.Clear();

                    // เพิ่มตัวเลือก "ไม่ระบุ" (null)
                    var noEventItem = new ComboBoxItem
                    {
                        Content = "ไม่ระบุงาน",
                        Tag = null
                    };
                    SaleEventComboBox.Items.Add(noEventItem);

                    // เพิ่มรายการงาน
                    foreach (var evt in events)
                    {
                        var item = new ComboBoxItem
                        {
                            Content = $"{evt.Name} ({evt.DateRangeText})",
                            Tag = evt
                        };
                        SaleEventComboBox.Items.Add(item);
                        System.Diagnostics.Debug.WriteLine($"Added SaleEvent: {evt.Name} (ID: {evt.Id})");
                    }

                    // เลือกรายการแรก (ไม่ระบุ) เป็น default
                    if (SaleEventComboBox.Items.Count > 0)
                    {
                        SaleEventComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading sale events: {ex}");
            }
        }

        private async Task LoadDataForEditAsync()
        {
            if (_editingDefinition == null) return;

            try
            {
                CouponDefinition? fresh = null;
                try
                {
                    fresh = await _service.GetByIdAsync(_editingDefinition.Id);
                }
                catch
                {
                    fresh = _editingDefinition;
                }

                if (fresh == null) return;

                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
                {
                    try
                    {
                        CodeTextBox.Text = fresh.Code ?? string.Empty;
                        NameTextBox.Text = fresh.Name ?? string.Empty;
                        PriceNumberBox.Value = (double)fresh.Price;
                        ValidFromDatePicker.Date = fresh.ValidFrom;
                        ValidToDatePicker.Date = fresh.ValidTo;

                        // Load code generator
                        if (fresh.CodeGenerator != null)
                        {
                            PrefixTextBox.Text = fresh.CodeGenerator.Prefix ?? string.Empty;
                            SuffixTextBox.Text = fresh.CodeGenerator.Suffix ?? string.Empty;
                            SequenceLengthNumberBox.Value = fresh.CodeGenerator.SequenceLength;
                        }

                        // load params
                        _editingDefinition.Params = fresh.Params;
                        LoadParametersForEdit();

                        // Select coupon type by matching Type field (which is now CouponTypeId)
                        await LoadBranchTypesAsync();
                        
                        foreach (ComboBoxItem it in TypeComboBox.Items)
                        {
                            if (it.Tag is Branch ct && ct.Id == fresh.BranchId)
                            {
                                TypeComboBox.SelectedItem = it;
                                break;
                            }
                        }

                        // **เพิ่ม: เลือก SaleEvent ที่ถูกบันทึกไว้**
                        if (fresh.SaleEventId.HasValue)
                        {
                            await LoadSaleEventsAsync();

                            foreach (ComboBoxItem it in SaleEventComboBox.Items)
                            {
                                if (it.Tag is SaleEvent evt && evt.Id == fresh.SaleEventId.Value)
                                {
                                    SaleEventComboBox.SelectedItem = it;
                                    // ensure UI reflects read-only formatted dates
                                    UpdateEventDateDisplay(evt);
                                    break;
                                }
                            }
                        }

                        // Set limited/unlimited radio buttons and visibility
                        if (fresh.IsLimited)
                        {
                            LimitedRadioButton.IsChecked = true;
                            UnlimitedRadioButton.IsChecked = false;
                            if (CodeGeneratorBorder != null) CodeGeneratorBorder.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            LimitedRadioButton.IsChecked = false;
                            UnlimitedRadioButton.IsChecked = true;
                            if (CodeGeneratorBorder != null) CodeGeneratorBorder.Visibility = Visibility.Collapsed;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error applying edit data to UI: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data for edit: {ex.Message}");
            }
        }

        private void InitializeForm()
        {
            try
            {
                if (ValidFromDatePicker != null)
                    ValidFromDatePicker.Date = DateTime.Today;
                if (ValidToDatePicker != null)
                    ValidToDatePicker.Date = DateTime.Today.AddYears(1);
                
                Title = _isEditMode ? "แก้ไขคำนิยามคูปอง" : "สร้างคำนิยามคูปองใหม่";

                // Default: limited selected so code settings visible
                if (LimitedRadioButton != null) LimitedRadioButton.IsChecked = true;
                if (UnlimitedRadioButton != null) UnlimitedRadioButton.IsChecked = false;
                if (CodeGeneratorBorder != null) CodeGeneratorBorder.Visibility = Visibility.Visible;

                UpdateCodePreview();
                UpdateDescriptionPreview();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in InitializeForm: {ex.Message}");
            }
        }

        private void LoadParametersForEdit()
        {
            if (string.IsNullOrEmpty(_editingDefinition?.Params)) return;

            try
            {
                var couponParams = JsonSerializer.Deserialize<CouponParams>(_editingDefinition.Params);
                if (couponParams != null)
                {
                    // Coupon value removed; description only
                    if (CouponDescriptionTextBox != null)
                        CouponDescriptionTextBox.Text = couponParams.description;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading parameters: {ex.Message}");
            }
        }

        private void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (CouponParamsPanel != null)
                    CouponParamsPanel.Visibility = Visibility.Visible;
                UpdateDescriptionPreview();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in TypeComboBox_SelectionChanged: {ex.Message}");
            }
        }

        private void PriceNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                UpdateDescriptionPreview();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PriceNumberBox_ValueChanged: {ex.Message}");
            }
        }

        private void UpdatePreview(object sender, object e)
        {
            UpdateDescriptionPreview();
        }

        private void CodeGenerator_TextChanged(object sender, object e)
        {
            UpdateCodePreview();
        }

        private void UpdateCodePreview()
        {
            try
            {
                var prefix = (PrefixTextBox?.Text ?? string.Empty).Trim();
                var suffix = (SuffixTextBox?.Text ?? string.Empty).Trim();
                var sequenceLength = Math.Max(1, SafeIntFromDouble(SequenceLengthNumberBox?.Value ??3));

                if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix))
                {
                    if (CodePreviewTextBlock != null)
                        CodePreviewTextBlock.Text = "กรุณากำหนดรหัสหน้าหรือรหัสหลัง";
                    return;
                }

                var paddedSequence = "1".PadLeft(sequenceLength, '0');
                var preview = $"{prefix}{paddedSequence}{suffix}";
                if (CodePreviewTextBlock != null)
                    CodePreviewTextBlock.Text = $"ตัวอย่าง: {preview}";
            }
            catch (Exception ex)
            {
                if (CodePreviewTextBlock != null)
                    CodePreviewTextBlock.Text = $"ข้อผิดพลาด: {ex.Message}";
            }
        }

        private void UpdateDescriptionPreview()
        {
            try
            {
                var price = SafeDecimalFromDouble(PriceNumberBox?.Value ?? 0);
                // use Price as coupon value
                var couponValue = price;
                var couponDesc = CouponDescriptionTextBox?.Text?.Trim() ?? string.Empty;
                
                if (DescriptionPreviewTextBlock != null)
                {
                    DescriptionPreviewTextBlock.Text = $"คูปองมูลค่า {couponValue:N2} บาท (ราคาขาย {price:N2} บาท)" +
                        (string.IsNullOrEmpty(couponDesc) ? "" : $" - {couponDesc}");
                }
            }catch (Exception ex)
            {
                if (DescriptionPreviewTextBlock != null)
                    DescriptionPreviewTextBlock.Text = $"ข้อผิดพลาด: {ex.Message}";
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (_isSaving) return;

            _isSaving = true;
            IsPrimaryButtonEnabled = false;
            PrimaryButtonText = "กำลังบันทึก...";

            try
            {
                var request = BuildCreateCouponRequest();
                // normalize code at UI level too
                request.Code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
                
                // Debug: Log the request details
                System.Diagnostics.Debug.WriteLine($"Creating coupon with BranchId: {request.BranchId}");
                
                // New: check duplicate code early to provide inline validation
                try
                {
                    var existingByCode = await _service.GetByCodeAsync(request.Code);
                    if (!_isEditMode)
                    {
                        if (existingByCode != null)
                        {
                            await ShowInlineErrorAsync($"รหัสคูปอง '{request.Code}' มีอยู่แล้ว");
                            _isSaving = false;
                            IsPrimaryButtonEnabled = true;
                            PrimaryButtonText = "บันทึก";
                            return;
                        }
                    }
                    else
                    {
                        // Edit mode: if another definition uses the same code, block
                        if (existingByCode != null && _editingDefinition != null && existingByCode.Id != _editingDefinition.Id)
                        {
                            await ShowInlineErrorAsync($"รหัสคูปอง '{request.Code}' ถูกใช้งานโดยคำนิยามอื่น");
                            _isSaving = false;
                            IsPrimaryButtonEnabled = true;
                            PrimaryButtonText = "บันทึก";
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: log and continue - service may be unavailable but validation will be handled server-side
                    System.Diagnostics.Debug.WriteLine($"Warning: unable to check duplicate code: {ex.Message}");
                }
                
                var validationResult = ValidateRequest(request);
                if (!validationResult.IsValid)
                {
                    await ShowInlineErrorAsync(validationResult.ErrorMessage);
                    _isSaving = false;
                    IsPrimaryButtonEnabled = true;
                    PrimaryButtonText = "บันทึก";
                    return;
                }
                
                bool success = false;
                if (_isEditMode && _editingDefinition != null)
                {
                    try
                    {
                        success = await _service.UpdateAsync(_editingDefinition.Id, request, Environment.UserName);
                        if (!success)
                        {
                            await ShowInlineErrorAsync("ไม่พบข้อมูลคำนิยามคูปองที่ต้องการแก้ไข");
                            _isSaving = false;
                            IsPrimaryButtonEnabled = true;
                            PrimaryButtonText = "บันทึก";
                            return;
                        }
                    }
                    catch (InvalidOperationException invEx)
                    {
                        await ShowInlineErrorAsync(invEx.Message);
                        _isSaving = false;
                        IsPrimaryButtonEnabled = true;
                        PrimaryButtonText = "บันทึก";
                        return;
                    }
                    catch (DbUpdateException dbEx)
                    {
                        await ShowInlineErrorAsync($"ไม่สามารถบันทึกข้อมูล: {dbEx.Message}");
                        _isSaving = false;
                        IsPrimaryButtonEnabled = true;
                        PrimaryButtonText = "บันทึก";
                        return;
                    }
                }
                else
                {
                    try
                    {
                        var result = await _service.CreateAsync(request, Environment.UserName);
                        success = result != null;
                        if (!success)
                        {
                            await ShowInlineErrorAsync("ไม่สามารถสร้างคำนิยามคูปองได้");
                            _isSaving = false;
                            IsPrimaryButtonEnabled = true;
                            PrimaryButtonText = "บันทึก";
                            return;
                        }
                    }
                    catch (InvalidOperationException invEx)
                    {
                        await ShowInlineErrorAsync(invEx.Message);
                        _isSaving = false;
                        IsPrimaryButtonEnabled = true;
                        PrimaryButtonText = "บันทึก";
                        return;
                    }
                    catch (DbUpdateException dbEx)
                    {
                        await ShowInlineErrorAsync($"ไม่สามารถบันทึกข้อมูล: {dbEx.Message}");
                        _isSaving = false;
                        IsPrimaryButtonEnabled = true;
                        PrimaryButtonText = "บันทึก";
                        return;
                    }
                }

                _operationSuccess = true;
                IsPrimaryButtonEnabled = true;
                PrimaryButtonText = "บันทึก";
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => this.Hide());
            }
            catch (Exception ex)
            {
                // Enhanced error logging
                System.Diagnostics.Debug.WriteLine($"Exception in ContentDialog_PrimaryButtonClick: {ex}");
                
                var errorMessage = $"เกิดข้อผิดพลาด: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\nรายละเอียด: {ex.InnerException.Message}";
                }
                
                await ShowInlineErrorAsync(errorMessage);
            }
            finally
            {
                _isSaving = false;
                if (!_operationSuccess)
                {
                    IsPrimaryButtonEnabled = true;
                    PrimaryButtonText = "บันทึก";
                }
            }
        }

        private CreateCouponRequest BuildCreateCouponRequest()
        {
            // Get selected CouponType
            int selectedTypeId = 0;

            if (TypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                if (selectedItem.Tag is Branch ct)
                {
                    selectedTypeId = ct.Id;
                    System.Diagnostics.Debug.WriteLine($"Selected Branch: {ct.Name} (ID: {ct.Id})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Selected item Tag is not Branch: {selectedItem.Tag?.GetType()}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No item selected in TypeComboBox or TypeComboBox is null");
            }

            // **เพิ่ม: ดึง SaleEventId**
            int? selectedEventId = null;
            if (SaleEventComboBox?.SelectedItem is ComboBoxItem eventItem && 
                eventItem.Tag is SaleEvent evt)
            {
                selectedEventId = evt.Id;
                System.Diagnostics.Debug.WriteLine($"Selected SaleEvent: {evt.Name} (ID: {evt.Id})");
            }

            var request = new CreateCouponRequest
            {
                Code = (CodeTextBox?.Text ?? string.Empty).Trim(),
                Name = (NameTextBox?.Text ?? string.Empty).Trim(),
                BranchId = selectedTypeId,
                Price = SafeDecimalFromDouble(PriceNumberBox?.Value ?? 0),
                ValidFrom = ValidFromDatePicker?.Date.DateTime ?? DateTime.Today,
                ValidTo = ValidToDatePicker?.Date.DateTime ?? DateTime.Today.AddYears(1),
                SequenceLength = Math.Clamp(SafeIntFromDouble(SequenceLengthNumberBox?.Value ?? 3), 1, 10),
                SaleEventId = selectedEventId // **เพิ่มบรรทัดนี้**
            };

            // Set IsLimited based on radio button
            request.IsLimited = LimitedRadioButton?.IsChecked == true;

            if (request.IsLimited)
            {
                request.Prefix = (PrefixTextBox?.Text ?? string.Empty).Trim();
                request.Suffix = (SuffixTextBox?.Text ?? string.Empty).Trim();
            }
            else
            {
                request.Prefix = string.Empty;
                request.Suffix = string.Empty;
            }

            var jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

            var couponParams = new CouponParams
            {
                // value removed
                description = (CouponDescriptionTextBox?.Text ?? string.Empty).Trim()
            };
            request.Params = JsonSerializer.Serialize(couponParams, jsonOptions);

            System.Diagnostics.Debug.WriteLine($"BuildCreateCouponRequest - BranchId: {request.BranchId}, Code: {request.Code}, IsLimited: {request.IsLimited}");

            return request;
        }

        private (bool IsValid, string ErrorMessage) ValidateRequest(CreateCouponRequest request)
        {
            if (string.IsNullOrEmpty(request.Code)) return (false, "กรุณาระบุรหัสคูปอง");
            if (string.IsNullOrEmpty(request.Name)) return (false, "กรุณาระบุชื่อคูปอง");
            // Allow zero price, only disallow negative prices
            if (request.Price <0) return (false, "ราคาคูปองต้องไม่เป็นค่าติดลบ");
            if (request.ValidFrom >= request.ValidTo) return (false, "วันที่เริ่มต้องน้อยกว่าวันที่สิ้นสุด");

            // Only require code generator settings when limited
            if (request.IsLimited)
            {
                if (string.IsNullOrEmpty(request.Prefix) && string.IsNullOrEmpty(request.Suffix)) return (false, "กรุณาระบุรหัสหน้าหรือรหัสหลัง");
                if (request.SequenceLength <1 || request.SequenceLength >10) return (false, "ความยาวตัวเลขต้องอยู่ระหว่าง1-10");
            }

            if (request.BranchId <=0) return (false, "กรุณาเลือกสาขาคูปอง");

            try
            {
                // Ensure params JSON deserializes to the expected shape
                var couponParams = JsonSerializer.Deserialize<CouponParams>(request.Params);
                if (couponParams == null) return (false, "พารามิเตอร์คูปองไม่ถูกต้อง");
            }
            catch
            {
                return (false, "พารามิเตอร์คูปองไม่ถูกต้อง");
            }

            return (true, string.Empty);
        }

        private Task ShowInlineErrorAsync(String message)
        {
            IsPrimaryButtonEnabled = true;
            PrimaryButtonText = "บันทึก";

            var errorTextBlock = new TextBlock { Text = message, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,10,0,0) };

            if (Content is ScrollViewer scrollViewer && scrollViewer.Content is StackPanel mainPanel)
            {
                var existingErrors = new List<TextBlock>();
                foreach (var child in mainPanel.Children)
                {
                    if (child is TextBlock tb && tb.Foreground is Microsoft.UI.Xaml.Media.SolidColorBrush brush && brush.Color == Microsoft.UI.Colors.Red)
                    {
                        existingErrors.Add(tb);
                    }
                }
                foreach (var error in existingErrors) mainPanel.Children.Remove(error);

                mainPanel.Children.Insert(0, errorTextBlock);

                _ = Task.Run(async () => { await Task.Delay(3000); Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => { if (mainPanel.Children.Contains(errorTextBlock)) mainPanel.Children.Remove(errorTextBlock); }); });
            }

            return Task.CompletedTask;
        }

        private static decimal SafeDecimalFromDouble(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0m;
            if (value > (double)decimal.MaxValue) return decimal.MaxValue;
            if (value < (double)decimal.MinValue) return decimal.MinValue;
            try { return (decimal)value; } catch { return 0m; }
        }

        private static int SafeIntFromDouble(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            if (value <= int.MinValue) return int.MinValue;
            if (value >= int.MaxValue) return int.MaxValue;
            try { return (int)value; } catch { return 0; }
        }

        // Add type creation handler using a Flyout to avoid opening nested ContentDialog
        private void AddTypeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var input = new TextBox { PlaceholderText = "ชื่อสาขาคูปองใหม่", Width =240, Margin = new Thickness(0,0,0,8) };
                var addBtn = new Button { Content = "เพิ่ม", Margin = new Thickness(0,0,8,0) };
                var cancelBtn = new Button { Content = "ยกเลิก" };

                var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                buttonsPanel.Children.Add(addBtn);
                buttonsPanel.Children.Add(cancelBtn);

                var panel = new StackPanel { Padding = new Thickness(12) };
                panel.Children.Add(input);
                panel.Children.Add(buttonsPanel);

                var flyout = new Flyout { Content = panel };

                // Wire up handlers
                addBtn.Click += async (s, args) =>
                {
                    var name = input.Text?.Trim();
                    if (string.IsNullOrEmpty(name)) return;

                    try
                    {
                        var created = await _couponService.AddBranchAsync(name, Environment.UserName);
                        // Update combo box on UI thread; store CouponType in Tag
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
                        {
                            TypeComboBox.Items.Add(new ComboBoxItem { Content = created.Name, Tag = created });
                            TypeComboBox.SelectedIndex = TypeComboBox.Items.Count -1;
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating coupon type: {ex.Message}");
                    }
                    finally
                    {
                        flyout.Hide();
                    }
                };

                cancelBtn.Click += (s, args) => flyout.Hide();

                // Show the flyout at the button
                flyout.ShowAt(AddTypeButton);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing add type flyout: {ex.Message}");
            }
        }

        // Ensure there is a CloseButton handler (generated partial class expects this)
        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // No special action required on close; keep method so generated code wiring compiles.
        }

        // Radio button handlers
        private void LimitedRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CodeGeneratorBorder != null) CodeGeneratorBorder.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LimitedRadioButton_Checked error: {ex.Message}");
            }
        }

        private void UnlimitedRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CodeGeneratorBorder != null) CodeGeneratorBorder.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UnlimitedRadioButton_Checked error: {ex.Message}");
            }
        }

        // **Event Handler สำหรับเลือก Sale Event**
        private void SaleEventComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (SaleEventComboBox.SelectedItem is ComboBoxItem item && item.Tag is SaleEvent evt)
                {
                    // mark event selected and show read-only formatted dates
                    IsEventSelected = true;
                    UpdateEventDateDisplay(evt);
                }
                else
                {
                    IsEventSelected = false;
                    UpdateEventDateDisplay(null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaleEventComboBox_SelectionChanged error: {ex.Message}");
            }
        }

        // **Event Handler สำหรับเพิ่มงานใหม่**
        private void AddSaleEventButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var inputStack = new StackPanel { Spacing = 8, Padding = new Thickness(12) };

                var nameInput = new TextBox 
                { 
                    PlaceholderText = "ชื่องาน เช่น งานแฟร์สิงหาคม", 
                    Width = 300 
                };
                inputStack.Children.Add(new TextBlock { Text = "ชื่องาน:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                inputStack.Children.Add(nameInput);

                var startDatePicker = new Microsoft.UI.Xaml.Controls.DatePicker 
                { 
                    Header = "วันที่เริ่มงาน", 
                    Date = DateTime.Today 
                };
                inputStack.Children.Add(startDatePicker);

                var endDatePicker = new Microsoft.UI.Xaml.Controls.DatePicker 
                { 
                    Header = "วันที่จบงาน", 
                    Date = DateTime.Today.AddDays(7) 
                };
                inputStack.Children.Add(endDatePicker);

                var buttonsPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    HorizontalAlignment = HorizontalAlignment.Right, 
                    Spacing = 8, 
                    Margin = new Thickness(0, 8, 0, 0) 
                };

                var addBtn = new Button { Content = "เพิ่ม" };
                var cancelBtn = new Button { Content = "ยกเลิก" };

                buttonsPanel.Children.Add(addBtn);
                buttonsPanel.Children.Add(cancelBtn);
                inputStack.Children.Add(buttonsPanel);

                var flyout = new Flyout { Content = inputStack };

                addBtn.Click += async (s, args) =>
                {
                    var name = nameInput.Text?.Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        nameInput.PlaceholderText = "กรุณากรอกชื่องาน!";
                        return;
                    }

                    var startDate = startDatePicker.Date.DateTime;
                    var endDate = endDatePicker.Date.DateTime;

                    if (endDate < startDate)
                    {
                        // แสดงข้อผิดพลาด
                        await ShowErrorMessageAsync("วันที่จบงานต้องมากกว่าหรือเท่ากับวันที่เริ่มงาน");
                        return;
                    }

                    try
                    {
                        using var context = new CouponContext();
                        var newEvent = new SaleEvent
                        {
                            Name = name,
                            StartDate = startDate,
                            EndDate = endDate,
                            IsActive = true,
                            CreatedBy = Environment.UserName,
                            CreatedAt = DateTime.Now
                        };

                        context.SaleEvents.Add(newEvent);
                        await context.SaveChangesAsync();

                        // อัปเดต ComboBox
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
                        {
                            var item = new ComboBoxItem 
                            { 
                                Content = $"{newEvent.Name} ({newEvent.DateRangeText})", 
                                Tag = newEvent 
                            };
                            SaleEventComboBox.Items.Add(item);
                            SaleEventComboBox.SelectedItem = item;
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating sale event: {ex.Message}");
                        await ShowErrorMessageAsync($"ไม่สามารถสร้างงานได้: {ex.Message}");
                    }
                    finally
                    {
                        flyout.Hide();
                    }
                };

                cancelBtn.Click += (s, args) => flyout.Hide();

                flyout.ShowAt(AddSaleEventButton);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing add sale event flyout: {ex.Message}");
            }
        }

        // **Helper Method แสดง Error**
        private async Task ShowErrorMessageAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "แจ้งเตือน",
                Content = message,
                CloseButtonText = "ตกลง",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        // Add helper to update the UI for event dates (read-only view with good formatting)
        private void UpdateEventDateDisplay(SaleEvent? evt)
        {
            try
            {
                // Prepare Thai culture with Thai Buddhist calendar (matches app elsewhere)
                var thai = new CultureInfo("th-TH");
                thai.DateTimeFormat.Calendar = new ThaiBuddhistCalendar();

                if (evt != null)
                {
                    // show read-only TextBlocks with full date (day month year)
                    if (EventStartDatePicker != null) EventStartDatePicker.Visibility = Visibility.Collapsed;
                    if (EventEndDatePicker != null) EventEndDatePicker.Visibility = Visibility.Collapsed;

                    if (EventStartDateTextBlock != null)
                    {
                        EventStartDateTextBlock.Visibility = Visibility.Visible;
                        EventStartDateTextBlock.Text = evt.StartDate.ToString("dd MMMM yyyy", thai); // e.g. 16 ธันวาคม 2568
                    }

                    if (EventEndDateTextBlock != null)
                    {
                        EventEndDateTextBlock.Visibility = Visibility.Visible;
                        EventEndDateTextBlock.Text = evt.EndDate.ToString("dd MMMM yyyy", thai);
                    }

                    // keep underlying DatePickers values synced (useful for serialization)
                    if (EventStartDatePicker != null) EventStartDatePicker.Date = evt.StartDate;
                    if (EventEndDatePicker != null) EventEndDatePicker.Date = evt.EndDate;
                }
                else
                {
                    // no event selected -> show editable DatePickers, hide read-only TextBlocks
                    if (EventStartDatePicker != null) EventStartDatePicker.Visibility = Visibility.Visible;
                    if (EventEndDatePicker != null) EventEndDatePicker.Visibility = Visibility.Visible;

                    if (EventStartDateTextBlock != null) { EventStartDateTextBlock.Visibility = Visibility.Collapsed; EventStartDateTextBlock.Text = string.Empty; }
                    if (EventEndDateTextBlock != null) { EventEndDateTextBlock.Visibility = Visibility.Collapsed; EventEndDateTextBlock.Text = string.Empty; }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateEventDateDisplay error: {ex.Message}");
            }
        }
    }
}
