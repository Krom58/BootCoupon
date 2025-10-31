﻿using Microsoft.EntityFrameworkCore;
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
        /// Try to redeem a coupon by code. Atomic: uses a single SQL UPDATE to avoid race conditions.
        /// This version allows marking a coupon as used even if it is already linked to a ReceiptItem (sold),
        /// as long as it has not been marked used (IsUsed == false).
        /// If caller provides a non-null receiptItemId, it will be stored; otherwise existing ReceiptItemId is preserved.
        /// </summary>
        public async Task<RedeemResult> RedeemCouponAsync(string code, string redeemedBy, int? receiptItemId = null, DateTime? redeemedAtUtc = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                return new RedeemResult(RedeemResultType.InvalidRequest, "Coupon code is required.");

            redeemedAtUtc ??= DateTime.UtcNow;

            // Load coupon metadata first
            var coupon = await _context.GeneratedCoupons
                .AsNoTracking()
                .FirstOrDefaultAsync(gc => gc.GeneratedCode == code);

            if (coupon == null)
                return new RedeemResult(RedeemResultType.NotFound, "Coupon not found.");

            if (coupon.ExpiresAt.HasValue && coupon.ExpiresAt.Value < redeemedAtUtc.Value)
                return new RedeemResult(RedeemResultType.Expired, "Coupon has expired.");

            // If already marked used, cannot redeem
            if (coupon.IsUsed)
            {
                return new RedeemResult(RedeemResultType.AlreadyUsed, "Coupon already used.");
            }

            // Atomic update:
            // - set IsUsed =1, UsedDate, UsedBy
            // - set ReceiptItemId only if caller provided a non-null receiptItemId (do not clear existing)
            // WHERE clause only checks IsUsed =0 so sold coupons (ReceiptItemId != null) can be marked used too.
            var rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE GeneratedCoupons
                SET IsUsed =1,
                    UsedDate = {redeemedAtUtc.Value},
                    UsedBy = {redeemedBy},
                    ReceiptItemId = CASE WHEN {receiptItemId} IS NOT NULL THEN {receiptItemId} ELSE ReceiptItemId END
                WHERE Id = {coupon.Id} AND IsUsed =0
            ");

            if (rows ==0)
            {
                // Concurrent update or already used
                return new RedeemResult(RedeemResultType.ConcurrentUpdate, "Coupon could not be redeemed (already used or updated).");
            }

            var updated = await _context.GeneratedCoupons
                .AsNoTracking()
                .FirstOrDefaultAsync(gc => gc.Id == coupon.Id);

            return new RedeemResult(RedeemResultType.Success, "Coupon redeemed successfully")
            {
                CouponId = updated?.Id,
                GeneratedCode = updated?.GeneratedCode,
                UsedBy = updated?.UsedBy,
                UsedDate = updated?.UsedDate,
                ReceiptItemId = updated?.ReceiptItemId
            };
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
                .Include(gc => gc.ReceiptItem)
                .AsQueryable();

            // Apply filters
            if (couponDefinitionId.HasValue)
                query = query.Where(gc => gc.CouponDefinitionId == couponDefinitionId.Value);

            if (!string.IsNullOrWhiteSpace(searchCode))
                query = query.Where(gc => gc.GeneratedCode.Contains(searchCode));

            if (isUsed.HasValue)
            {
                // Treat a coupon as 'used' if it has been redeemed (IsUsed == true)
                // or if it has been allocated/sold (ReceiptItemId != null).
                if (isUsed.Value)
                {
                    query = query.Where(gc => gc.IsUsed || gc.ReceiptItemId != null);
                }
                else
                {
                    query = query.Where(gc => !gc.IsUsed && gc.ReceiptItemId == null);
                }
            }

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
                    // ExpiresAt copied from generated coupon
                    ExpiresAt = gc.ExpiresAt,
                    // If ReceiptItemId is present, treat as sold (ขายแล้ว)
                    IsSold = gc.ReceiptItemId != null,
                    // store ReceiptItemId for later lookup if needed
                    ReceiptItemId = gc.ReceiptItemId,
                    StatusText = gc.ReceiptItemId != null ? "ขายแล้ว" : "ยังไม่ได้ขาย",
                    UsageText = gc.ReceiptItemId != null && gc.ReceiptItem != null ?
                        $"ขายในใบเสร็จ #{gc.ReceiptItem.ReceiptId}" :
                        (gc.IsUsed && gc.UsedDate.HasValue ?
                            $"ใช้โดย {gc.UsedBy} เมื่อ {gc.UsedDate.Value:dd/MM/yyyy HH:mm}" :
                            "ยังไม่มีการใช้งาน")
                })
                .ToListAsync();

            // NOTE: removed lookup for SoldReceiptCode — only ReceiptItemId is returned to caller.

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
                .Include(gc => gc.ReceiptItem)
                .FirstOrDefaultAsync(gc => gc.Id == id);
        }

        /// <summary>
        /// ดึงข้อมูลคูปองที่ถูกสร้างตามรหัส
        /// </summary>
        public async Task<GeneratedCoupon?> GetByCodeAsync(string code)
        {
            return await _context.GeneratedCoupons
                .Include(gc => gc.CouponDefinition)
                .Include(gc => gc.ReceiptItem)
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
            var items = await _context.GeneratedCoupons
                .Include(gc => gc.CouponDefinition)
                .Include(gc => gc.ReceiptItem)
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
                    ExpiresAt = gc.ExpiresAt,
                    IsSold = gc.ReceiptItemId != null,
                    ReceiptItemId = gc.ReceiptItemId,
                    StatusText = gc.ReceiptItemId != null ? "ขายแล้ว" : "ยังไม่ได้ขาย",
                    UsageText = gc.ReceiptItemId != null && gc.ReceiptItem != null ?
                        $"ขายในใบเสร็จ #{gc.ReceiptItem.ReceiptId}" :
                        (gc.IsUsed && gc.UsedDate.HasValue ?
                            $"ใช้โดย {gc.UsedBy} เมื่อ {gc.UsedDate.Value:dd/MM/yyyy HH:mm}" :
                            "ยังไม่มีการใช้งาน")
                })
                .ToListAsync();

            // NOTE: removed lookup for SoldReceiptCode — only ReceiptItemId is returned to caller.

            return items;
        }

        /// <summary>
        /// ดึงเฉพาะคูปองที่ขายแล้ว (ReceiptItemId != null) พร้อมกรองและแบ่งหน้า
        /// </summary>
        public async Task<PagedResult<GeneratedCouponDisplayModel>> GetSoldCouponsAsync(
            int page = 1,
            int pageSize = 50,
            int? couponDefinitionId = null,
            string? searchCode = null,
            int? receiptItemId = null,
            int? receiptId = null,
            string? soldBy = null,
            string? couponDefinitionCode = null,
            string? couponDefinitionName = null,
            DateTime? createdFrom = null,
            DateTime? createdTo = null)
        {
            var query = _context.GeneratedCoupons
                .Include(gc => gc.CouponDefinition)
                .Include(gc => gc.ReceiptItem)
                .AsQueryable();

            // Only sold (linked to a receipt item)
            query = query.Where(gc => gc.ReceiptItemId != null);

            if (couponDefinitionId.HasValue)
                query = query.Where(gc => gc.CouponDefinitionId == couponDefinitionId.Value);

            if (!string.IsNullOrWhiteSpace(searchCode))
                query = query.Where(gc => gc.GeneratedCode.Contains(searchCode));

            if (receiptItemId.HasValue)
                query = query.Where(gc => gc.ReceiptItemId == receiptItemId.Value);

            if (receiptId.HasValue)
                query = query.Where(gc => gc.ReceiptItem != null && gc.ReceiptItem.ReceiptId == receiptId.Value);

            if (!string.IsNullOrWhiteSpace(soldBy))
                query = query.Where(gc => gc.UsedBy != null && gc.UsedBy.Contains(soldBy));

            if (!string.IsNullOrWhiteSpace(couponDefinitionCode))
                query = query.Where(gc => gc.CouponDefinition != null && gc.CouponDefinition.Code.Contains(couponDefinitionCode));

            if (!string.IsNullOrWhiteSpace(couponDefinitionName))
                query = query.Where(gc => gc.CouponDefinition != null && gc.CouponDefinition.Name.Contains(couponDefinitionName));

            if (createdFrom.HasValue)
                query = query.Where(gc => gc.CreatedAt >= createdFrom.Value);

            if (createdTo.HasValue)
                query = query.Where(gc => gc.CreatedAt <= createdTo.Value);

            var totalCount = await query.CountAsync();

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
                    CreatedBy = gc.CreatedBy,
                    ExpiresAt = gc.ExpiresAt,
                    IsSold = gc.ReceiptItemId != null,
                    ReceiptItemId = gc.ReceiptItemId,
                    StatusText = "ขายแล้ว",
                    UsageText = gc.ReceiptItem != null ? $"ขายในใบเสร็จ #{gc.ReceiptItem.ReceiptId}" : "ขายแล้ว"
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
            csv.AppendLine("รหัสคูปอง,รหัสคำนิยาม,ชื่อคำนิยาม,สถานะ,สร้างโดย,ใช้โดย/ขายในใบเสร็จ,ใบเสร็จ,วันที่ใช้/ขาย,วันที่สร้าง,วันหมดอายุ");

            foreach (var item in result.Items)
            {
                csv.AppendLine($"\"{item.GeneratedCode}\",\"{item.CouponDefinitionCode}\",\"{item.CouponDefinitionName}\",\"{item.StatusText}\",\"{item.CreatedBy ?? ""}\",\"{item.UsedBy ?? (item.StatusText == "ขายแล้ว" ? item.UsageText : "") }\",\"{item.ReceiptItemId?.ToString() ?? ""}\",\"{item.UsedDate?.ToString("dd/MM/yyyy HH:mm") ?? (item.StatusText == "ขายแล้ว" ? item.UsageText : "") }\",\"{item.CreatedAt:dd/MM/yyyy HH:mm}\",\"{item.ExpiresAt?.ToString("dd/MM/yyyy") ?? ""}\"");
            }

            return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }

    // Redeem result helper types
    public enum RedeemResultType
    {
        Success,
        NotFound,
        Expired,
        AlreadyUsed,
        ConcurrentUpdate,
        InvalidRequest,
        Error
    }

    public class RedeemResult
    {
        public RedeemResultType Result { get; set; }
        public string Message { get; set; } = string.Empty;

        public int? CouponId { get; set; }
        public string? GeneratedCode { get; set; }
        public string? UsedBy { get; set; }
        public DateTime? UsedDate { get; set; }
        public int? ReceiptItemId { get; set; }

        public RedeemResult() { }

        public RedeemResult(RedeemResultType result, string message)
        {
            Result = result;
            Message = message;
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
        public string? CreatedBy { get; set; }
        public bool IsSold { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public int? ReceiptItemId { get; set; }
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