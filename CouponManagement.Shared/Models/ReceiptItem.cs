namespace CouponManagement.Shared.Models
{
    public class ReceiptItem
    {
        public Coupon Coupon { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal TotalPrice => Coupon.Price * Quantity;
    }
}