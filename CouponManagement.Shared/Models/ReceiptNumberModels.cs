using System;

namespace CouponManagement.Shared.Models
{
    public class ReceiptNumberManager
    {
        public int Id { get; set; }
        public string Prefix { get; set; } = "INV";
        public int CurrentNumber { get; set; } = 1; // เริ่มจาก 1 แทน 5001
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public string? UpdatedBy { get; set; }
        
        // *** เพิ่มฟิลด์ YearCode สำหรับเก็บปี ค.ศ. 2 หลัก ***
        public int YearCode { get; set; } = DateTime.Now.Year % 100; // เช่น 25 สำหรับ 2025
    }

    public class CanceledReceiptNumber
    {
        public int Id { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public DateTime CanceledDate { get; set; } = DateTime.Now;
        public string? Reason { get; set; }
        public string? OwnerMachineId { get; set; }
    }
}