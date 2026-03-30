using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CouponManagement.Shared.Models;

namespace CouponManagement.Shared.Services
{
    public class SaleEventService
    {
        /// <summary>
        /// ดึงงานขายทั้งหมดที่ Active
        /// </summary>
        public async Task<List<SaleEvent>> GetAllActiveSaleEventsAsync()
        {
            using var context = new CouponContext();
            return await context.SaleEvents
                .Where(se => se.IsActive)
                .OrderByDescending(se => se.StartDate)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// เพิ่มงานขายใหม่
        /// </summary>
        public async Task<SaleEvent> CreateSaleEventAsync(string name, DateTime startDate, DateTime endDate, string createdBy)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("ชื่องานไม่สามารถเป็นค่าว่างได้");

            if (endDate <= startDate)
                throw new ArgumentException("วันที่สิ้นสุดต้องหลังวันที่เริ่มต้น");

            using var context = new CouponContext();

            // Check duplicate name
            var exists = await context.SaleEvents
                .AnyAsync(se => se.Name.ToLower() == name.ToLower());

            if (exists)
                throw new InvalidOperationException($"งานขายชื่อ '{name}' มีอยู่แล้ว");

            var saleEvent = new SaleEvent
            {
                Name = name.Trim(),
                StartDate = startDate,
                EndDate = endDate,
                IsActive = true,
                CreatedBy = createdBy,
                CreatedAt = DateTime.Now
            };

            context.SaleEvents.Add(saleEvent);
            await context.SaveChangesAsync();

            return saleEvent;
        }

        /// <summary>
        /// ดึงงานขายตาม ID
        /// </summary>
        public async Task<SaleEvent?> GetByIdAsync(int id)
        {
            using var context = new CouponContext();
            return await context.SaleEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(se => se.Id == id);
        }

        /// <summary>
        /// อัปเดตงานขาย
        /// </summary>
        public async Task<bool> UpdateSaleEventAsync(int id, string name, DateTime startDate, DateTime endDate, string updatedBy)
        {
            if (endDate <= startDate)
                throw new ArgumentException("วันที่สิ้นสุดต้องหลังวันที่เริ่มต้น");

            using var context = new CouponContext();
            var existing = await context.SaleEvents.FindAsync(id);

            if (existing == null)
                return false;

            // Check duplicate name (exclude current)
            var duplicate = await context.SaleEvents
                .AnyAsync(se => se.Name.ToLower() == name.ToLower() && se.Id != id);

            if (duplicate)
                throw new InvalidOperationException($"งานขายชื่อ '{name}' มีอยู่แล้ว");

            existing.Name = name.Trim();
            existing.StartDate = startDate;
            existing.EndDate = endDate;
            existing.UpdatedBy = updatedBy;
            existing.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();
            return true;
        }
    }
}