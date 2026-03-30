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

        // Branch (was CouponType) Methods
        public async Task<List<Branch>> GetAllBranchesAsync()
        {
            return await _context.Branches.ToListAsync();
        }

        // New overload to accept createdBy
        public async Task<Branch> AddBranchAsync(string name, string createdBy)
        {
            var branch = new Branch { Name = name, CreatedBy = createdBy, CreatedAt = DateTime.Now };
            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();
            return branch;
        }

        // Backward compatible method (keeps old name for internal convenience)
        public async Task<Branch> AddBranchAsync(string name)
        {
            return await AddBranchAsync(name, Environment.UserName);
        }

        public async Task<bool> UpdateBranchAsync(int id, string name)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return false;

            branch.Name = name;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteBranchAsync(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return false;

            // ตรวจสอบว่ามีคูปองใช้สาขานี้อยู่หรือไม่
            var hasUsage = await _context.Coupons.AnyAsync(c => c.BranchId == id);
            if (hasUsage) return false;

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();
            return true;
        }

        // Coupon Methods
        public async Task<List<Coupon>> GetAllCouponsAsync()
        {
            // Do not include Branch navigation; consumers should use BranchId or call CouponService.GetAllBranchesAsync
            return await _context.Coupons.ToListAsync();
        }

        public async Task<Coupon> AddCouponAsync(string name, decimal price, string code, int branchId)
        {
            var coupon = new Coupon
            {
                Name = name,
                Price = price,
                Code = code,
                BranchId = branchId
            };

            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            // Do not load Branch navigation here
            return coupon;
        }

        public async Task<bool> UpdateCouponAsync(int id, string name, decimal price, string code, int branchId)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return false;

            coupon.Name = name;
            coupon.Price = price;
            coupon.Code = code;
            coupon.BranchId = branchId;

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