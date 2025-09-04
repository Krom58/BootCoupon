using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace BootCoupon
{
    public class ReceiptModel
    {
        public int ReceiptID { get; set; }
        public DateTime ReceiptDate { get; set; }

        // Add receipt code field
        public string ReceiptCode { get; set; } = string.Empty;

        // Customer information directly in ReceiptModel
        public string CustomerName { get; set; } = null!;
        public string CustomerPhoneNumber { get; set; } = null!;

        public decimal TotalAmount { get; set; }
        public List<DatabaseReceiptItem> Items { get; set; } = new();

        public int? SalesPersonId { get; set; }
        
        // เพิ่มสถานะของใบเสร็จ
        public string Status { get; set; } = "Active";
        
        // เพิ่มวิธีการชำระเงิน
        public int? PaymentMethodId { get; set; }
    }

    public class DatabaseReceiptItem
    {
        [Key] // Add this attribute to mark this as the primary key
        public int ReceiptItemId { get; set; }
        public int ReceiptId { get; set; }
        public int CouponId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }

    // เพิ่ม class สำหรับแสดงข้อมูลใน DataGrid
    public class ReceiptDisplayModel
    {
        public int ReceiptID { get; set; }
        public DateTime ReceiptDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhoneNumber { get; set; } = string.Empty;
        public string ReceiptCode { get; set; } = string.Empty;
        public int? SalesPersonId { get; set; }
        public string Status { get; set; } = "Active";
        public int? PaymentMethodId { get; set; }
        
        // Properties สำหรับแสดงผล
        public string TotalAmountFormatted => TotalAmount.ToString("N2");
        public string ReceiptDateFormatted => ReceiptDate.ToString("dd/MM/yyyy HH:mm:ss");
        public string StatusText => Status == "Active" ? "ใช้งาน" : Status == "Cancelled" ? "ยกเลิก" : Status;
    }
}