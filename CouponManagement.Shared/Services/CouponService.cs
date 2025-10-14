using Microsoft.EntityFrameworkCore;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace CouponManagement.Shared.Services
{
    public class CouponService
    {
        private readonly CouponContext _context;

        public CouponService(CouponContext context)
        {
            _context = context;
        }

        public CouponService() : this(new CouponContext())
        {
        }

        // CouponType Methods
        public async Task<List<CouponType>> GetAllCouponTypesAsync()
        {
            return await _context.CouponTypes.ToListAsync();
        }

        // New overload to accept createdBy
        public async Task<CouponType> AddCouponTypeAsync(string name, string createdBy)
        {
            var couponType = new CouponType { Name = name, CreatedBy = createdBy, CreatedAt = DateTime.Now };
            _context.CouponTypes.Add(couponType);
            await _context.SaveChangesAsync();
            return couponType;
        }

        // Backward compatible method
        public async Task<CouponType> AddCouponTypeAsync(string name)
        {
            return await AddCouponTypeAsync(name, Environment.UserName);
        }

        public async Task<bool> UpdateCouponTypeAsync(int id, string name)
        {
            var couponType = await _context.CouponTypes.FindAsync(id);
            if (couponType == null) return false;

            couponType.Name = name;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteCouponTypeAsync(int id)
        {
            var couponType = await _context.CouponTypes.FindAsync(id);
            if (couponType == null) return false;

            // ตรวจสอบว่ามีคูปองใช้ประเภทนี้อยู่หรือไม่
            var hasUsage = await _context.Coupons.AnyAsync(c => c.CouponTypeId == id);
            if (hasUsage) return false;

            _context.CouponTypes.Remove(couponType);
            await _context.SaveChangesAsync();
            return true;
        }

        // Coupon Methods
        public async Task<List<Coupon>> GetAllCouponsAsync()
        {
            // Do not include CouponType navigation; consumers should use CouponTypeId or call CouponService.GetAllCouponTypesAsync
            return await _context.Coupons.ToListAsync();
        }

        public async Task<Coupon> AddCouponAsync(string name, decimal price, string code, int couponTypeId)
        {
            var coupon = new Coupon
            {
                Name = name,
                Price = price,
                Code = code,
                CouponTypeId = couponTypeId
            };

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            // Do not load CouponType navigation here
            return coupon;
        }

        public async Task<bool> UpdateCouponAsync(int id, string name, decimal price, string code, int couponTypeId)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return false;

            coupon.Name = name;
            coupon.Price = price;
            coupon.Code = code;
            coupon.CouponTypeId = couponTypeId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteCouponAsync(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return false;

            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();
            return true;
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}