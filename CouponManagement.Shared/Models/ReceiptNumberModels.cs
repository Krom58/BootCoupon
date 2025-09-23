using System;

namespace CouponManagement.Shared.Models
{
    public class ReceiptNumberManager
    {
        public int Id { get; set; }
        public string Prefix { get; set; } = "INV";
        public int CurrentNumber { get; set; } = 5001;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public string? UpdatedBy { get; set; }
    }

    public class CanceledReceiptNumber
    {
        public int Id { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public DateTime CanceledDate { get; set; } = DateTime.Now;
        public string? Reason { get; set; }
    }
}