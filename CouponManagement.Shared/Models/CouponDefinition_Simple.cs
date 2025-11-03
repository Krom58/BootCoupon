using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;

namespace CouponManagement.Shared.Models
{
    public class CouponDefinition
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string Code { get; set; } = string.Empty;
        
        [Required]
        [StringLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        public string Name { get; set; } = string.Empty;
        
        // ใช้ CouponTypeId โดยตรง - ง่ายและชัดเจน
        public int CouponTypeId { get; set; }

        [ForeignKey(nameof(CouponTypeId))]
        public CouponType? CouponType { get; set; }
        
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }
        
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Params { get; set; } = string.Empty;
        
        public DateTime ValidFrom { get; set; }
        
        public DateTime ValidTo { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        [Required]
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string CreatedBy { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? UpdatedBy { get; set; }
        
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public CouponCodeGenerator? CodeGenerator { get; set; }
        public List<GeneratedCoupon> GeneratedCoupons { get; set; } = new();

        // New flag to indicate limited (has codes) vs unlimited
        public bool IsLimited { get; set; } = false;
        
        // Helper properties
        [NotMapped]
        public string StatusText => GetStatusText();
        
        [NotMapped]
        public string TypeDisplayText => GetTypeDisplayText();
        
        [NotMapped]
        public bool IsExpired => ValidTo < DateTime.Now;
        
        [NotMapped]
        public bool IsUpcoming => ValidFrom > DateTime.Now;
        
        [NotMapped]
        public bool IsCurrentlyValid => DateTime.Now >= ValidFrom && DateTime.Now <= ValidTo && IsActive;
        
        [NotMapped]
        public CouponParameters? ParsedParams
        {
            get
            {
                try
                {
                    // เนื่องจากตอนนี้ Type เป็น int แล้ว ให้ใช้ default เป็น COUPON
                    return JsonSerializer.Deserialize<CouponParams>(Params);
                }
                catch
                {
                    return null;
                }
            }
        }

        [NotMapped]
        public string DescriptionPreview => ParsedParams?.GetDescription() ?? string.Empty;

        // Safe accessors to avoid XAML NullReference when CodeGenerator is null
        [NotMapped]
        public string NextCode => CodeGenerator?.NextCode ?? string.Empty;

        [NotMapped]
        public int GeneratedCount => CodeGenerator?.GeneratedCount ?? 0;
        
        private string GetStatusText()
        {
            if (!IsActive) return "ไม่ใช้งาน";
            if (IsExpired) return "หมดอายุ";
            if (IsUpcoming) return "ยังไม่เริ่ม";
            return "ใช้งานได้";
        }
        
        private string GetTypeDisplayText()
        {
            // ใช้ CouponType navigation property หรือแสดง Type ID
            return CouponType?.Name ?? $"ประเภท {CouponTypeId}";
        }
    }
    
    // Code Generator Model
    public class CouponCodeGenerator
    {
        public int Id { get; set; }
        public int CouponDefinitionId { get; set; }
        
        [Required]
        [StringLength(10)]
        [Column(TypeName = "nvarchar(10)")]
        public string Prefix { get; set; } = string.Empty;
        
        [Required]
        [StringLength(10)]
        [Column(TypeName = "nvarchar(10)")]
        public string Suffix { get; set; } = string.Empty;
        
        public int CurrentSequence { get; set; } = 0;
        
        [Range(1, 10)]
        public int SequenceLength { get; set; } = 3;
        
        public int GeneratedCount { get; set; } = 0;
        
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? UpdatedBy { get; set; }
        
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation property
        public CouponDefinition? CouponDefinition { get; set; }
        
        // Helper methods
        [NotMapped]
        public string NextCode => GenerateCode(CurrentSequence + 1);
        
        public string PreviewCode(int quantity = 1)
        {
            if (quantity == 1)
                return GenerateCode(CurrentSequence + 1);
            
            var startCode = GenerateCode(CurrentSequence + 1);
            var endCode = GenerateCode(CurrentSequence + quantity);
            return $"{startCode} ถึง {endCode}";
        }
        
        private string GenerateCode(int sequence)
        {
            var paddedSequence = sequence.ToString().PadLeft(SequenceLength, '0');
            return $"{Prefix}{paddedSequence}{Suffix}";
        }
    }
    
    // Generated Coupon Model
    public class GeneratedCoupon
    {
        public int Id { get; set; }
        public int CouponDefinitionId { get; set; }

        [Required]
        [StringLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string GeneratedCode { get; set; } = string.Empty;

        public int BatchNumber { get; set; }
        public bool IsUsed { get; set; } = false;
        public DateTime? UsedDate { get; set; }

        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? UsedBy { get; set; }

        // New: record who generated this coupon
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // New: link to ReceiptItem when this generated coupon was allocated/sold
        public int? ReceiptItemId { get; set; }
        public DatabaseReceiptItem? ReceiptItem { get; set; }

        // New: optional expiration date for this generated coupon
        public DateTime? ExpiresAt { get; set; }

        // Navigation property
        public CouponDefinition? CouponDefinition { get; set; }
    }
    
    // Parameter classes
    public abstract class CouponParameters
    {
        public abstract string GetDescription();
    }
    
    // คูปองธรรมดา
    public class CouponParams : CouponParameters
    {
        // removed numeric value - project now uses Price for amount
        public string description { get; set; } = string.Empty;
        
        public override string GetDescription()
        {
            // Return description only; Price is shown from CouponDefinition.Price elsewhere
            return description;
        }
    }
    
    // Removed BuyXFreeYParams and BuyXPlusYParams - project now only supports simple COUPON
    
    // Request/Response models
    public class CreateCouponRequest
    {
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;
        
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public int CouponTypeId { get; set; } 

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }
        
        [Required]
        public string Params { get; set; } = string.Empty;
        
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        // Indicates whether this definition uses generated codes (limited quantity)
        public bool IsLimited { get; set; } = false;
        
        // Code Generator settings (optional unless IsLimited == true)
        [StringLength(10)]
        public string Prefix { get; set; } = string.Empty;
        
        [StringLength(10)]
        public string Suffix { get; set; } = string.Empty;
        
        [Range(1,10)]
        public int SequenceLength { get; set; } =3;
    }
    
    public class GenerateCouponsRequest
    {
        public int CouponDefinitionId { get; set; }
        
        [Range(1, 10000)]
        public int Quantity { get; set; }
        
        [Required]
        public string CreatedBy { get; set; } = string.Empty;
    }
    
    public class GenerateCouponsResponse
    {
        public int CouponDefinitionId { get; set; }
        public int GeneratedQuantity { get; set; }
        public int BatchNumber { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> GeneratedCodes { get; set; } = new();
    }
    
    public class CouponPreviewRequest
    {
        public int Type { get; set; } // เปลี่ยนเป็น int
        public string Params { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
    
    public class CouponPreviewResponse
    {
        public string Description { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
    
    public class CodePreviewRequest
    {
        public int CouponDefinitionId { get; set; }
        public int Quantity { get; set; } = 1;
    }
    
    public class CodePreviewResponse
    {
        public string PreviewCode { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
}