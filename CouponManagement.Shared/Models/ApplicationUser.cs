using System;
using System.ComponentModel.DataAnnotations;

namespace CouponManagement.Shared.Models
{
 public class ApplicationUser
 {
 public int Id { get; set; }

 [Required]
 [StringLength(100)]
 public string Username { get; set; } = string.Empty;

 // For simplicity this project stores password as plain text in DB (managed by admin).
 // You can change to hashed passwords later and update authentication logic accordingly.
 [Required]
 [StringLength(200)]
 public string Password { get; set; } = string.Empty;

 [StringLength(200)]
 public string? DisplayName { get; set; }

 public bool IsActive { get; set; } = true;

 public DateTime CreatedAt { get; set; } = DateTime.Now;
 public string? UserType { get; set; } = null;
 }
}