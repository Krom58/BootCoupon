using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations; // Add this for the [Key] attribute

namespace BootCoupon
{
    public class SalesPerson
    {
        [Key]
        public int ID { get; set; }
        public string Branch { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Telephone { get; set; } = string.Empty;

    }
}