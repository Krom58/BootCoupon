using System.ComponentModel.DataAnnotations;

namespace CouponManagement.Shared.Models
{
    public class Coupon
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Code { get; set; } = string.Empty;
        public int CouponTypeId { get; set; }
        
        public CouponType CouponType { get; set; } = null!;
        
        public Coupon() { }
        
        public Coupon(CouponType couponType)
        {
            CouponType = couponType;
        }
    }
}