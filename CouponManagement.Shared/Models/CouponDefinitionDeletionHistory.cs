using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponManagement.Shared.Models
{
    /// <summary>
    /// บันทึกประวัติการลบคำนิยามคูปอง (ทั้งที่ยังไม่สร้างคูปอง หรือสร้างแล้วแต่ไม่เคยถูกใช้งาน/ผูกใบเสร็จ)
    /// </summary>
    [Table("CouponDefinitionDeletionHistories")]
    public class CouponDefinitionDeletionHistory
    {
        [Key]
        public int Id { get; set; }

        public int CouponDefinitionId { get; set; }

        [Required]
        [StringLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string CouponCode { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        public string CouponName { get; set; } = string.Empty;

        public int BranchId { get; set; }

        [StringLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        public string? BranchName { get; set; }

        public int? SaleEventId { get; set; }

        [StringLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        public string? SaleEventName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public DateTime ValidFrom { get; set; }

        public DateTime ValidTo { get; set; }

        /// <summary>
        /// จำนวนคูปองที่สร้างไว้จากคำนิยามนี้ (0 ใบ กรณีไม่เคยสร้าง, หรือ N ใบ กรณีเคยสร้างแล้วถูกลบ)
        /// </summary>
        public int TotalCouponsCreated { get; set; }

        [Required]
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string DeletedBy { get; set; } = string.Empty;

        public DateTime DeletedAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        [Column(TypeName = "nvarchar(500)")]
        public string? DeletionReason { get; set; }

        [NotMapped]
        public string PriceDisplay => $"{Price:N2} บาท";

        [NotMapped]
        public string DeletedAtDisplay => DeletedAt.ToString("dd/MM/yyyy HH:mm:ss");

        [NotMapped]
        public string TotalCouponsCreatedDisplay => TotalCouponsCreated.ToString("N0");
    }
}
