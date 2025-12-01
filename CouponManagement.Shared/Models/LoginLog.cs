using System;

namespace CouponManagement.Shared.Models
{
 public class LoginLog
 {
 public long LogId { get; set; }
 public DateTime LoggedAt { get; set; }
 public string UserName { get; set; } = string.Empty;
 public string Action { get; set; } = string.Empty;
 public string? Location { get; set; }
 public string App { get; set; } = string.Empty;
 }
}
