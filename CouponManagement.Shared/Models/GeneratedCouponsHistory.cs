using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponManagement.Shared.Models
{
    /// <summary>
    /// เก็บประวัติคูปองจำกัดจำนวน (เมื่อมีการเปลี่ยนแปลงสถานะ)
    /// เช่น: ตั้งค่าเป็น COM, ยกเลิกใบเสร็จ, เปลี่ยนสถานะการใช้งาน
    /// </summary>
    [Table("GeneratedCouponsHistory")]
    public class GeneratedCouponsHistory
    {
        [Key]
        [Column("HistoryId")]
        public int Id { get; set; }

        /// <summary>
        /// ID ของคูปองใน GeneratedCoupons ที่ถูกบันทึกประวัติ
        /// </summary>
        public int GeneratedCouponId { get; set; }

        public int CouponDefinitionId { get; set; }

        [Required]
        [StringLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string GeneratedCode { get; set; } = string.Empty;

        public int BatchNumber { get; set; }
        public bool IsComplimentary { get; set; } = false;
        public bool IsUsed { get; set; } = false;
        public DateTime? UsedDate { get; set; }

        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? UsedBy { get; set; }

        /// <summary>
        /// ReceiptItemId ที่คูปองนี้เคยถูกใช้
        /// </summary>
        public int? ReceiptItemId { get; set; }

        /// <summary>
        /// ReceiptId ของใบเสร็จที่เกี่ยวข้อง
        /// </summary>
        public int? ReceiptId { get; set; }

        [StringLength(50)]
        public string? ReceiptCode { get; set; }

        /// <summary>
        /// วันหมดอายุของคูปอง
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// ประเภทของการกระทำ เช่น "SetComplimentary", "ReceiptCancelled"
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// ผู้ทำรายการ
        /// </summary>
        [StringLength(100)]
        public string? ActionBy { get; set; }

        /// <summary>
        /// เวลาที่ทำรายการ
        /// </summary>
        public DateTime ActionAt { get; set; } = DateTime.Now;

        /// <summary>
        /// เหตุผล/หมายเหตุ
        /// </summary>
        [StringLength(500)]
        public string? ActionReason { get; set; }

        // Navigation property
        public CouponDefinition? CouponDefinition { get; set; }
    }
}
