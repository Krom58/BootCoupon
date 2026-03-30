using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponManagement.Shared.Models
{
    /// <summary>
    /// โมเดลสำหรับงานที่ออกขาย (Sale Event)
    /// </summary>
    public class SaleEvent
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Column(TypeName = "nvarchar(200)")]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

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

        // Helper properties
        [NotMapped]
        public bool IsCurrentlyActive => 
            IsActive && 
            DateTime.Now >= StartDate && 
            DateTime.Now <= EndDate;

        [NotMapped]
        public bool IsUpcoming => StartDate > DateTime.Now;

        [NotMapped]
        public bool IsExpired => EndDate < DateTime.Now;

        [NotMapped]
        public string StatusText
        {
            get
            {
                if (!IsActive) return "ปิดใช้งาน";
                if (IsExpired) return "สิ้นสุดแล้ว";
                if (IsUpcoming) return "ยังไม่เริ่ม";
                return "กำลังดำเนินการ";
            }
        }

        [NotMapped]
        public string DateRangeText => $"{StartDate:dd/MM/yyyy} - {EndDate:dd/MM/yyyy}";
    }
}