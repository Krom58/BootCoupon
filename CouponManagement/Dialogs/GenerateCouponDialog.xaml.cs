using CouponManagement.Shared.Models;
using CouponManagement.Shared.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

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
            
            LoadDefinitionInfo();
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
            // Prefer reading quantity from the sender (safer) but fall back to control field
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
                else if (QuantityNumberBox != null)
                {
                    var v = QuantityNumberBox.Value;
                    if (double.IsNaN(v) || double.IsInfinity(v)) v = 1;
                    qty = Math.Max(1, (int)v);
                }
            }
            catch
            {
                qty = 1;
            }

            UpdateCodePreview(qty);
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
                    if (QuantityNumberBox != null)
                    {
                        var v = QuantityNumberBox.Value;
                        if (double.IsNaN(v) || double.IsInfinity(v))
                            v = 1;
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

                var startSequence = generator.CurrentSequence + 1;
                var endSequence = generator.CurrentSequence + quantity;

                var startCode = GenerateCode(generator, startSequence, seqLen);

                if (quantity == 1)
                {
                    CodePreviewTextBlock.Text = startCode;
                }
                else
                {
                    var endCode = GenerateCode(generator, endSequence, seqLen);
                    CodePreviewTextBlock.Text = $"{startCode} ถึง {endCode}";
                }
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

        // Make GenerateCode take the generator explicitly and be null-safe
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
                var quantity = (int)QuantityNumberBox.Value;
                if (quantity < 1 || quantity > 10000)
                {
                    // แสดง inline error แทน (หรือซ่อนแล้วแสดง ContentDialog แต่แนะนำ inline)
                    await ShowErrorAsync("จำนวนไม่ถูกต้อง", "กรุณาระบุจำนวนระหว่าง 1-10,000");
                    return;
                }

                // ก่อนเปิด confirmation ให้ซ่อน dialog ปัจจุบันก่อน
                this.Hide();

                var confirm = await ShowConfirmationAsync(
                    "ยืนยันการสร้างคูปอง",
                    $"คุณต้องการสร้างคูปอง {quantity:N0} ใบ สำหรับ '{_definition.Name}' ใช่หรือไม่?\n\nรหัสที่จะสร้าง: {CodePreviewTextBlock.Text}\n\n⚠️ คูปองที่สร้างแล้วจะไม่สามารถลบได้");

                if (!confirm)
                {
                    // ถ้าผู้ใช้ยกเลิก ให้เปิด dialog นี้ขึ้นมาใหม่เพื่อให้แก้ต่อได้
                    await this.ShowAsync();
                    return;
                }

                // ถ้าผู้ใช้ยืนยัน ให้ดำเนินการสร้าง แล้วแสดง success dialog (ตอนนี้ dialog ตัวนี้ปิดแล้ว จึงปลอดภัย)
                IsPrimaryButtonEnabled = false;
                PrimaryButtonText = "กำลังสร้าง...";

                var request = new GenerateCouponsRequest { CouponDefinitionId = _definition.Id, Quantity = quantity, CreatedBy = Environment.UserName };
                var result = await _service.GenerateCouponsAsync(request);

                await ShowSuccessAsync("สร้างคูปองเรียบร้อย", $"สร้างคูปอง {result.GeneratedQuantity:N0} ใบ เรียบร้อยแล้ว");

                // เสร็จแล้ว ไม่ต้องเปิด dialog นี้อีก (already hidden)
            }
            catch (Exception ex)
            {
                // ถ้าเกิด error ระหว่าง process: ถ้า dialog ถูกซ่อนแล้ว แสดง error dialog โดยตรง
                try { await ShowErrorAsync("เกิดข้อผิดพลาดในการสร้างคูปอง", ex.Message); }
                finally { /* ไม่ต้อง re-show dialog */ }
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
