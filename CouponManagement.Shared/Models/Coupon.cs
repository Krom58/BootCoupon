using System.ComponentModel.DataAnnotations;

namespace CouponManagement.Shared.Models
{
    public class Coupon
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Code { get; set; } = string.Empty;

        // Renamed to BranchId to match DB column and domain terminology
        public int BranchId { get; set; }

        // Navigation property renamed
        public Branch Branch { get; set; } = null!;

        public Coupon() { }

        public Coupon(Branch branch)
        {
            Branch = branch;
            BranchId = branch?.Id ?? 0;
        }
    }
}