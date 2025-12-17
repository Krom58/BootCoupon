using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace CouponManagement.Shared.Models
{
    // File kept as Branch.cs for minimal file changes.
    // The CLR type is renamed to Branch to match DB table and new naming.
    [Table("Branch")]
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // New audit fields
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}