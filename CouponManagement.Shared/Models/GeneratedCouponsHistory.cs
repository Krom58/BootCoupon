using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponManagement.Shared.Models
{
    /// <summary>
    /// เก็บประวัติคูปองที่ถูกยกเลิก/ย้ายจาก GeneratedCoupons
    /// เมื่อใบเสร็จถูกยกเลิก คูปองจะถูกย้ายมาที่ตารางนี้เพื่อเก็บประวัติ
    /// </summary>
    public class GeneratedCouponsHistory
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

        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// ReceiptItemId ที่คูปองนี้เคยถูกใช้ (ก่อนถูกยกเลิก)
        /// </summary>
        public int? ReceiptItemId { get; set; }

        /// <summary>
        /// วันหมดอายุของคูปอง
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// บ่งชี้ว่าเป็นคูปองฟรี/คอมมิชชั่น (COM) หรือไม่
        /// </summary>
        public bool IsComplimentary { get; set; } = false;

        /// <summary>
        /// วันที่คูปองถูกย้ายมาที่ History (เมื่อใบเสร็จถูกยกเลิก)
        /// </summary>
        public DateTime MovedToHistoryAt { get; set; } = DateTime.Now;

        /// <summary>
        /// เหตุผลที่ย้ายมา History (เช่น "Receipt Cancelled")
        /// </summary>
        [StringLength(255)]
        public string? MovedReason { get; set; }

        // Navigation properties (optional - ไม่จำเป็นต้องมีก็ได้)
        public CouponDefinition? CouponDefinition { get; set; }
    }
}
