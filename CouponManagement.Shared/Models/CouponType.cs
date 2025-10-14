using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponManagement.Shared.Models
{
    [Table("CouponTypes")]
    public class CouponType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // New audit fields
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}