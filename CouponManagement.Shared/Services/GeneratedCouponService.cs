using Microsoft.EntityFrameworkCore;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace CouponManagement.Shared.Services
{
    public class GeneratedCouponService
    {
        private readonly CouponContext _context;

        public GeneratedCouponService(CouponContext context)
        {
            _context = context;
        }

        public GeneratedCouponService() : this(new CouponContext())
        {
        }

        /// <summary>
        /// ดึงรายการคูปองที่ถูกสร้างทั้งหมดพร้อมการกรองและการแบ่งหน้า
        /// </summary>
        public async Task<PagedResult<GeneratedCouponDisplayModel>> GetGeneratedCouponsAsync(
            int page = 1,
            int pageSize = 50,
            int? couponDefinitionId = null,
            string? searchCode = null,
            bool? isUsed = null,
            DateTime? createdFrom = null,
            DateTime? createdTo = null,
            string? createdBy = null)
        {
            var query = _context.GeneratedCoupons
                .Include(gc => gc.CouponDefinition)
                .AsQueryable();

            // Apply filters
            if (couponDefinitionId.HasValue)
                query = query.Where(gc => gc.CouponDefinitionId == couponDefinitionId.Value);

            if (!string.IsNullOrWhiteSpace(searchCode))
                query = query.Where(gc => gc.GeneratedCode.Contains(searchCode));

            if (isUsed.HasValue)
                query = query.Where(gc => gc.IsUsed == isUsed.Value);

            if (createdFrom.HasValue)
                query = query.Where(gc => gc.CreatedAt >= createdFrom.Value);

            if (createdTo.HasValue)
                query = query.Where(gc => gc.CreatedAt <= createdTo.Value);

            if (!string.IsNullOrWhiteSpace(createdBy))
                query = query.Where(gc => gc.CouponDefinition != null && gc.CouponDefinition.CreatedBy.Contains(createdBy));

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and sorting
            var items = await query
                .OrderByDescending(gc => gc.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(gc => new GeneratedCouponDisplayModel
                {
                    Id = gc.Id,
                    GeneratedCode = gc.GeneratedCode,
                    CouponDefinitionId = gc.CouponDefinitionId,
                    CouponDefinitionCode = gc.CouponDefinition != null ? gc.CouponDefinition.Code : "",
                    CouponDefinitionName = gc.CouponDefinition != null ? gc.CouponDefinition.Name : "",
                    BatchNumber = gc.BatchNumber,
                    IsUsed = gc.IsUsed,
                    UsedDate = gc.UsedDate,
                    UsedBy = gc.UsedBy,
                    CreatedAt = gc.CreatedAt,
                    // populate CreatedBy for display
                    CreatedBy = gc.CreatedBy,
                    StatusText = gc.IsUsed ? "ใช้แล้ว" : "ยังไม่ใช้",
                    UsageText = gc.IsUsed && gc.UsedDate.HasValue ? 
                        $"ใช้โดย {gc.UsedBy} เมื่อ {gc.UsedDate.Value:dd/MM/yyyy HH:mm}" : 
                        "ยังไม่มีการใช้งาน"
                })
                .ToListAsync();

            return new PagedResult<GeneratedCouponDisplayModel>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        /// <summary>
        /// ดึงข้อมูลคูปองที่ถูกสร้างตาม ID
        /// </summary>
        public async Task<GeneratedCoupon?> GetByIdAsync(int id)
        {
            return await _context.GeneratedCoupons
                .Include(gc => gc.CouponDefinition)
                .FirstOrDefaultAsync(gc => gc.Id == id);
        }

        /// <summary>
        /// ดึงข้อมูลคูปองที่ถูกสร้างตามรหัส
        /// </summary>
        public async Task<GeneratedCoupon?> GetByCodeAsync(string code)
        {
            return await _context.GeneratedCoupons
                .Include(gc => gc.CouponDefinition)
                .FirstOrDefaultAsync(gc => gc.GeneratedCode == code);
        }

        /// <summary>
        /// ดึงสถิติคูปองที่ถูกสร้างตาม Definition
        /// </summary>
        public async Task<GeneratedCouponStatistics> GetStatisticsByDefinitionAsync(int couponDefinitionId)
        {
            var coupons = await _context.GeneratedCoupons
                .Where(gc => gc.CouponDefinitionId == couponDefinitionId)
                .ToListAsync();

            return new GeneratedCouponStatistics
            {
                TotalGenerated = coupons.Count,
                TotalUsed = coupons.Count(gc => gc.IsUsed),
                TotalAvailable = coupons.Count(gc => !gc.IsUsed),
                LatestBatchNumber = coupons.Any() ? coupons.Max(gc => gc.BatchNumber) : 0,
                LastGeneratedDate = coupons.Any() ? coupons.Max(gc => gc.CreatedAt) : (DateTime?)null
            };
        }

        /// <summary>
        /// ดึงคูปองทั้งหมดในแบทช์เดียวกัน
        /// </summary>
        public async Task<List<GeneratedCouponDisplayModel>> GetByBatchAsync(int couponDefinitionId, int batchNumber)
        {
            return await _context.GeneratedCoupons
                .Include(gc => gc.CouponDefinition)
                .Where(gc => gc.CouponDefinitionId == couponDefinitionId && gc.BatchNumber == batchNumber)
                .OrderBy(gc => gc.GeneratedCode)
                .Select(gc => new GeneratedCouponDisplayModel
                {
                    Id = gc.Id,
                    GeneratedCode = gc.GeneratedCode,
                    CouponDefinitionId = gc.CouponDefinitionId,
                    CouponDefinitionCode = gc.CouponDefinition != null ? gc.CouponDefinition.Code : "",
                    CouponDefinitionName = gc.CouponDefinition != null ? gc.CouponDefinition.Name : "",
                    BatchNumber = gc.BatchNumber,
                    IsUsed = gc.IsUsed,
                    UsedDate = gc.UsedDate,
                    UsedBy = gc.UsedBy,
                    CreatedAt = gc.CreatedAt,
                    // populate CreatedBy
                    CreatedBy = gc.CreatedBy,
                    StatusText = gc.IsUsed ? "ใช้แล้ว" : "ยังไม่ใช้",
                    UsageText = gc.IsUsed && gc.UsedDate.HasValue ? 
                        $"ใช้โดย {gc.UsedBy} เมื่อ {gc.UsedDate.Value:dd/MM/yyyy HH:mm}" : 
                        "ยังไม่มีการใช้งาน"
                })
                .ToListAsync();
        }

        /// <summary>
        /// ส่งออกข้อมูลคูปองเป็น CSV
        /// </summary>
        public async Task<byte[]> ExportToCsvAsync(
            int? couponDefinitionId = null,
            string? searchCode = null,
            bool? isUsed = null,
            DateTime? createdFrom = null,
            DateTime? createdTo = null)
        {
            var result = await GetGeneratedCouponsAsync(1, int.MaxValue, couponDefinitionId, searchCode, isUsed, createdFrom, createdTo);
            
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("รหัสคูปอง,รหัสคำนิยาม,ชื่อคำนิยาม,แบทช์,สถานะ,สร้างโดย,ใช้โดย,วันที่ใช้,วันที่สร้าง");
            
            foreach (var item in result.Items)
            {
                csv.AppendLine($"\"{item.GeneratedCode}\",\"{item.CouponDefinitionCode}\",\"{item.CouponDefinitionName}\",{item.BatchNumber},\"{item.StatusText}\",\"{item.CreatedBy ?? ""}\",\"{item.UsedBy ?? ""}\",\"{item.UsedDate?.ToString("dd/MM/yyyy HH:mm") ?? ""}\",\"{item.CreatedAt:dd/MM/yyyy HH:mm}\"");
            }
            
            return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }

    // Supporting models
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    public class GeneratedCouponDisplayModel
    {
        public int Id { get; set; }
        public string GeneratedCode { get; set; } = string.Empty;
        public int CouponDefinitionId { get; set; }
        public string CouponDefinitionCode { get; set; } = string.Empty;
        public string CouponDefinitionName { get; set; } = string.Empty;
        public int BatchNumber { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? UsedDate { get; set; }
        public string? UsedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        // New: include CreatedBy for display in UI
        public string? CreatedBy { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string UsageText { get; set; } = string.Empty;
    }

    public class GeneratedCouponStatistics
    {
        public int TotalGenerated { get; set; }
        public int TotalUsed { get; set; }
        public int TotalAvailable { get; set; }
        public int LatestBatchNumber { get; set; }
        public DateTime? LastGeneratedDate { get; set; }
    }
}