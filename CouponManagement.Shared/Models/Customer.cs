using System;
using System.ComponentModel.DataAnnotations;

namespace CouponManagement.Shared.Models
{
    public class Customer
    {
        public int Id { get; set; }
        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;
        [StringLength(50)]
        public string? Phone { get; set; }
        [StringLength(200)]
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}