using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BootCoupon.Models
{
    public class ReservedCoupon
    {
        public int Id { get; set; }
        public int CouponDefinitionId { get; set; }
        public string SessionId { get; set; } = null!;
        public int Quantity { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }
}
