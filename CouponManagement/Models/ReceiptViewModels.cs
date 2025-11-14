namespace CouponManagement
{
 // View models used by ReceiptPage
 public class ReceiptRow
 {
 public int Id { get; set; }
 public string ReceiptCode { get; set; } = string.Empty;
 public string CustomerName { get; set; } = string.Empty;
 public string CustomerPhone { get; set; } = string.Empty;
 public System.DateTime ReceiptDate { get; set; }
 public string ReceiptDateString { get; set; } = string.Empty;
 public string SalesPersonName { get; set; } = string.Empty;
 public string PaymentMethodName { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;
 }

 public class ReceiptItemRow
 {
 public string CouponName { get; set; } = string.Empty;
 public int Quantity { get; set; }
 public decimal UnitPrice { get; set; }
 public decimal TotalPrice { get; set; }
 public int ReceiptItemId { get; set; }
 public bool IsComplimentary { get; set; }

 // New properties to display in the edit-items dialog
 public string CouponCode { get; set; } = string.Empty;
 public string CouponTypeName { get; set; } = string.Empty;

 // Generated coupon codes associated with this receipt item (may be empty)
 public System.Collections.Generic.List<string> GeneratedCodes { get; set; } = new System.Collections.Generic.List<string>();
 }
}
