using CouponManagement.Shared.Models;
using CouponManagement.Shared.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CouponManagement.Dialogs
{
    public sealed partial class GenerateCouponDialog : ContentDialog
    {
        private readonly CouponDefinitionService _service;
        private readonly CouponDefinition _definition;

        public GenerateCouponDialog(CouponDefinition definition)
        {
            this.InitializeComponent();
            _service = new CouponDefinitionService();
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));

            // wire events for checkbox and start/end if controls exist
            var seqCb = this.FindName("UseSequenceCheckBox") as CheckBox;
            if (seqCb != null)
            {
                seqCb.Checked += UseSequenceCheckBox_Checked;
                seqCb.Unchecked += UseSequenceCheckBox_Unchecked;
            }
            var startNb = this.FindName("StartSequenceNumberBox") as NumberBox;
            var endNb = this.FindName("EndSequenceNumberBox") as NumberBox;
            if (startNb != null) startNb.ValueChanged += StartEnd_ValueChanged;
            if (endNb != null) endNb.ValueChanged += StartEnd_ValueChanged;

            LoadDefinitionInfo();
            UpdateCodePreview();
        }

        private void StartEnd_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            UpdateCodePreview();
        }

        private void UseSequenceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var qtyNb = this.FindName("QuantityNumberBox") as NumberBox;
            var startNb = this.FindName("StartSequenceNumberBox") as NumberBox;
            var endNb = this.FindName("EndSequenceNumberBox") as NumberBox;
            if (qtyNb != null) qtyNb.IsEnabled = false;
            if (startNb != null) startNb.IsEnabled = true;
            if (endNb != null) endNb.IsEnabled = true;
            UpdateCodePreview();
        }

        private void UseSequenceCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var qtyNb = this.FindName("QuantityNumberBox") as NumberBox;
            var startNb = this.FindName("StartSequenceNumberBox") as NumberBox;
            var endNb = this.FindName("EndSequenceNumberBox") as NumberBox;
            if (qtyNb != null) qtyNb.IsEnabled = true;
            if (startNb != null) startNb.IsEnabled = false;
            if (endNb != null) endNb.IsEnabled = false;
            UpdateCodePreview();
        }

        private async void LoadDefinitionInfo()
        {
            try
            {
                // Load definition info
                DefinitionCodeTextBlock.Text = _definition.Code;
                DefinitionNameTextBlock.Text = _definition.Name;
                DefinitionTypeTextBlock.Text = _definition.TypeDisplayText;
                DefinitionPriceTextBlock.Text = $"{_definition.Price:N2} บาท";

                // Load description from parsed params
                if (_definition.ParsedParams != null)
                {
                    DefinitionDescriptionTextBlock.Text = _definition.ParsedParams.GetDescription();
                }
                else
                {
                    DefinitionDescriptionTextBlock.Text = "ไม่มีคำอธิบาย";
                }

                // Load current status
                await LoadCurrentStatus();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("ข้อผิดพลาดในการโหลดข้อมูล", ex.Message);
            }
        }

        private async Task LoadCurrentStatus()
        {
            try
            {
                // Reload definition with fresh data
                var freshDefinition = await _service.GetByIdAsync(_definition.Id);
                if (freshDefinition?.GeneratedCoupons != null)
                {
                    var totalGenerated = freshDefinition.GeneratedCoupons.Count;
                    var totalUsed = freshDefinition.GeneratedCoupons.Count(gc => gc.IsUsed);
                    var available = totalGenerated - totalUsed;

                    GeneratedCountTextBlock.Text = $"{totalGenerated:N0} คูปอง";
                    UsedCountTextBlock.Text = $"{totalUsed:N0} คูปอง";
                    AvailableCountTextBlock.Text = $"{available:N0} คูปอง";
                }
                else
                {
                    GeneratedCountTextBlock.Text = "0 คูปอง";
                    UsedCountTextBlock.Text = "0 คูปอง";
                    AvailableCountTextBlock.Text = "0 คูปอง";
                }
            }
            catch (Exception ex)
            {
                // Log error but don't show to user - it's not critical
                System.Diagnostics.Debug.WriteLine($"Error loading current status: {ex.Message}");

                GeneratedCountTextBlock.Text = "ไม่ทราบ";
                UsedCountTextBlock.Text = "ไม่ทราบ";
                AvailableCountTextBlock.Text = "ไม่ทราบ";
            }
        }

        private void QuantityNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            int qty = 1;
            try
            {
                if (sender != null)
                {
                    // clamp to non-negative and within reasonable int range
                    var v = sender.Value;
                    if (double.IsNaN(v) || double.IsInfinity(v)) v = 1;
                    qty = Math.Max(1, (int)v);
                }
                else
                {
                    var qtyNb = this.FindName("QuantityNumberBox") as NumberBox;
                    if (qtyNb != null)
                    {
                        var v = qtyNb.Value;
                        if (double.IsNaN(v) || double.IsInfinity(v)) v = 1;
                        qty = Math.Max(1, (int)v);
                    }
                }
            }
            catch
            {
                qty = 1;
            }

            UpdateCodePreview(qty);
        }

        private int? GetNumberBoxValueNullable(NumberBox? nb)
        {
            if (nb == null) return null;
            var v = nb.Value;
            if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0) return null;
            return (int)v;
        }

        // Make UpdateCodePreview null-safe and optionally accept an explicit quantity
        private void UpdateCodePreview(int? explicitQuantity = null)
        {
            try
            {
                // If the preview text control is not yet created, abort safely.
                if (CodePreviewTextBlock == null)
                    return;

                int quantity = explicitQuantity ?? 1;

                // If explicitQuantity not provided, try reading from control safely
                if (!explicitQuantity.HasValue)
                {
                    var qtyNb = this.FindName("QuantityNumberBox") as NumberBox;
                    if (qtyNb != null)
                    {
                        var v = qtyNb.Value;
                        if (double.IsNaN(v) || double.IsInfinity(v)) v = 1;
                        quantity = Math.Max(1, (int)v);
                    }
                }

                // Definition must exist (constructor ensures this). Guard anyway.
                if (_definition == null)
                {
                    CodePreviewTextBlock.Text = "ข้อผิดพลาด: ไม่มีข้อมูลคำนิยาม";
                    return;
                }

                // If CodeGenerator missing, show informative message
                var generator = _definition.CodeGenerator;
                if (generator == null)
                {
                    CodePreviewTextBlock.Text = "ไม่สามารถสร้างรหัสได้ (ไม่มี Code Generator)";
                    return;
                }

                // Protect against invalid sequence length
                var seqLen = Math.Max(1, generator.SequenceLength);

                int startSequence;
                int endSequence;

                var seqCb = this.FindName("UseSequenceCheckBox") as CheckBox;
                if (seqCb != null && seqCb.IsChecked == true)
                {
                    var startNb = this.FindName("StartSequenceNumberBox") as NumberBox;
                    var endNb = this.FindName("EndSequenceNumberBox") as NumberBox;
                    var startVal = GetNumberBoxValueNullable(startNb);
                    var endVal = GetNumberBoxValueNullable(endNb);

                    if (startVal.HasValue && endVal.HasValue)
                    {
                        startSequence = startVal.Value;
                        endSequence = endVal.Value;
                    }
                    else if (startVal.HasValue && !endVal.HasValue)
                    {
                        startSequence = startVal.Value;
                        endSequence = startSequence + quantity - 1;
                    }
                    else if (!startVal.HasValue && endVal.HasValue)
                    {
                        endSequence = endVal.Value;
                        startSequence = endSequence - quantity + 1;
                        if (startSequence < 1) startSequence = 1;
                    }
                    else
                    {
                        startSequence = generator.CurrentSequence + 1;
                        endSequence = startSequence + quantity - 1;
                    }
                }
                else
                {
                    // default behavior: use quantity relative to current sequence
                    startSequence = generator.CurrentSequence + 1;
                    endSequence = generator.CurrentSequence + quantity;
                }

                var startCode = GenerateCode(generator, startSequence, seqLen);

                if (startSequence == endSequence)
                {
                    CodePreviewTextBlock.Text = startCode;
                }
                else
                {
                    var endCode = GenerateCode(generator, endSequence, seqLen);
                    CodePreviewTextBlock.Text = $"{startCode} ถึง {endCode}";
                }

                BuildCodeSamplesAndTooltip(generator, startSequence, endSequence);
            }
            catch (Exception ex)
            {
                // If preview control available, show message; otherwise log.
                if (CodePreviewTextBlock != null)
                    CodePreviewTextBlock.Text = $"ข้อผิดพลาด: {ex.Message}";
                else
                    System.Diagnostics.Debug.WriteLine($"UpdateCodePreview error: {ex.Message}");
            }
        }

        private void BuildCodeSamplesAndTooltip(CouponCodeGenerator generator, int startSequence, int endSequence)
        {
            try
            {
                if (CodeSamplesTextBlock == null) return;

                int total = endSequence - startSequence + 1;
                int maxShow = 5; // show up to 5 samples

                var samples = new List<string>();
                for (int s = startSequence; s <= Math.Min(endSequence, startSequence + maxShow - 1); s++)
                {
                    samples.Add(GenerateCode(generator, s, generator.SequenceLength));
                }

                if (total <= maxShow)
                {
                    CodeSamplesTextBlock.Text = "ตัวอย่าง: " + string.Join(", ", samples);
                    ToolTipService.SetToolTip(CodeSamplesTextBlock, null);
                }
                else
                {
                    CodeSamplesTextBlock.Text = $"ตัวอย่าง: {string.Join(", ", samples)} ... (ทั้งหมด {total} รายการ)";

                    // create full list tooltip (may be long)
                    var fullList = new System.Text.StringBuilder();
                    int limitInTooltip = 200; // cap tooltip length
                    int count = 0;
                    for (int s = startSequence; s <= endSequence; s++)
                    {
                        if (count > 0) fullList.Append(", ");
                        fullList.Append(GenerateCode(generator, s, generator.SequenceLength));
                        count++;
                        if (fullList.Length > limitInTooltip)
                        {
                            fullList.Append(" ...");
                            break;
                        }
                    }

                    var tb = new TextBlock { Text = fullList.ToString(), TextWrapping = TextWrapping.Wrap };
                    ToolTipService.SetToolTip(CodeSamplesTextBlock, tb);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BuildCodeSamplesAndTooltip error: {ex.Message}");
            }
        }

        private string GenerateCode(CouponCodeGenerator? generator, int sequence, int? overrideSequenceLength = null)
        {
            try
            {
                if (generator == null)
                    return "ERROR";

                var seqLen = overrideSequenceLength ?? Math.Max(1, generator.SequenceLength);
                var paddedSequence = sequence.ToString().PadLeft(seqLen, '0');

                var prefix = generator.Prefix ?? string.Empty;
                var suffix = generator.Suffix ?? string.Empty;

                return $"{prefix}{paddedSequence}{suffix}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GenerateCode error: {ex.Message}");
                return "ERROR";
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;

            try
            {
                var seqCb = this.FindName("UseSequenceCheckBox") as CheckBox;
                bool useSequence = seqCb != null && seqCb.IsChecked == true;

                var qtyNb = this.FindName("QuantityNumberBox") as NumberBox;
                int quantity = GetNumberBoxValueNullable(qtyNb) ??1;
                if (quantity < 1 || quantity > 10000)
                {
                    // แสดง inline error แทน (หรือซ่อนแล้วแสดง ContentDialog แต่แนะนำ inline)
                    await ShowErrorAsync("จำนวนไม่ถูกต้อง", "กรุณาระบุจำนวนระหว่าง 1-10,000");
                    return;
                }

                int startSequence = 0, endSequence = 0;
                int actualQty = quantity;

                var generator = _definition.CodeGenerator;
                if (generator == null)
                {
                    await ShowErrorAsync("ไม่สามารถสร้างคูปอง", "ไม่พบการตั้งค่ารหัสสำหรับคำนิยามนี้");
                    return;
                }

                if (useSequence)
                {
                    var startNb = this.FindName("StartSequenceNumberBox") as NumberBox;
                    var endNb = this.FindName("EndSequenceNumberBox") as NumberBox;
                    var startVal = GetNumberBoxValueNullable(startNb);
                    var endVal = GetNumberBoxValueNullable(endNb);

                    if (startVal.HasValue && endVal.HasValue)
                    {
                        startSequence = startVal.Value;
                        endSequence = endVal.Value;
                    }
                    else if (startVal.HasValue && !endVal.HasValue)
                    {
                        startSequence = startVal.Value;
                        endSequence = startSequence + quantity - 1;
                    }
                    else if (!startVal.HasValue && endVal.HasValue)
                    {
                        endSequence = endVal.Value;
                        startSequence = endSequence - quantity + 1;
                        if (startSequence < 1) startSequence = 1;
                    }
                    else
                    {
                        startSequence = generator.CurrentSequence + 1;
                        endSequence = startSequence + quantity - 1;
                    }

                    if (startSequence > endSequence)
                    {
                        await ShowErrorAsync("ช่วงตัวเลขไม่ถูกต้อง", "เลขเริ่มต้นต้องน้อยกว่าหรือเท่ากับเลขสิ้นสุด");
                        return;
                    }

                    actualQty = endSequence - startSequence + 1;
                }

                // Before confirm, build preview text
                var previewText = CodePreviewTextBlock?.Text ?? string.Empty;

                // Hide and confirm
                this.Hide();
                var confirm = await ShowConfirmationAsync(
                    "ยืนยันการสร้างคูปอง",
                    useSequence
                    ? $"คุณต้องการสร้างคูปอง {actualQty:N0} ใบ สำหรับ '{_definition.Name}' ใช่หรือไม่?\n\nรหัสที่จะสร้าง: {GenerateCode(generator, startSequence, generator.SequenceLength)} ถึง {GenerateCode(generator, endSequence, generator.SequenceLength)}\n\n⚠️ คูปองที่สร้างแล้วจะไม่สามารถลบได้"
                    : $"คุณต้องการสร้างคูปอง {quantity:N0} ใบ สำหรับ '{_definition.Name}' ใช่หรือไม่?\n\nรหัสที่จะสร้าง: {previewText}\n\n⚠️ คูปองที่สร้างแล้วจะไม่สามารถลบได้");

                if (!confirm)
                {
                    await this.ShowAsync();
                    return;
                }

                IsPrimaryButtonEnabled = false;
                PrimaryButtonText = "กำลังสร้าง...";

                var request = new GenerateCouponsRequest { CouponDefinitionId = _definition.Id, Quantity = actualQty, CreatedBy = Environment.UserName };
                GenerateCouponsResponse result;

                if (useSequence)
                {
                    // call service overload with range (service updated to accept range)
                    result = await _service.GenerateCouponsAsync(request, startSequence, endSequence);
                }
                else
                {
                    result = await _service.GenerateCouponsAsync(request);
                }

                await ShowSuccessAsync("สร้างคูปองเรียบร้อย", $"สร้างคูปอง {result.GeneratedQuantity:N0} ใบ เรียบร้อยแล้ว");
            }
            catch (Exception ex)
            {
                try { await ShowErrorAsync("เกิดข้อผิดพลาดในการสร้างคูปอง", ex.Message); }
                finally { /* no-op */ }
            }
            finally
            {
                IsPrimaryButtonEnabled = true;
                PrimaryButtonText = "สร้างคูปอง";
            }
        }

        private async Task<bool> ShowConfirmationAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "ยืนยัน",
                CloseButtonText = "ยกเลิก",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
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

        private async Task ShowSuccessAsync(string title, string message)
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
