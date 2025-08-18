namespace BootCoupon
{
    public class Coupon
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Code { get; set; } = string.Empty;
        public int CouponTypeId { get; set; }
        
        // Change from required to nullable with non-null assertion operator
        public CouponType CouponType { get; set; } = null!;
        
        // Add parameterless constructor for XAML
        public Coupon() { }
        
        // Add constructor with required parameters for normal use
        public Coupon(CouponType couponType)
        {
            CouponType = couponType;
        }
    }
}