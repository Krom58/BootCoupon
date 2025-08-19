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

}