using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using CouponManagement.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace CouponManagement.Shared.Services
{
    public class CouponDefinitionService
    {
        // NOTE: Do not keep a long-lived DbContext as a field. Create a local context per operation to avoid
        // concurrent DataReader/connection issues when the service is reused across async/UI calls.

        // ดึงรายการทั้งหมดพร้อมตัวกรอง
        public async Task<List<CouponDefinition>> GetAllAsync(
            string? typeFilter = null,
            string? statusFilter = null,
            string? searchText = null)
        {
            using var context = new CouponContext();

            var query = context.CouponDefinitions
                .Include(cd => cd.CodeGenerator)
                .Include(cd => cd.CouponType) // Include CouponType navigation
                .AsNoTracking() // Ensure fresh data is loaded and not tracked to avoid stale cached entities
                .AsQueryable();

            // Filter by Type (which is now CouponTypeId)
            if (!string.IsNullOrEmpty(typeFilter) && typeFilter != "ALL")
            {
                if (int.TryParse(typeFilter, out int typeId))
                {
                    query = query.Where(cd => cd.CouponTypeId == typeId);
                }
            }

            // Filter by status
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "ALL")
            {
                var now = DateTime.Now;
                query = statusFilter switch
                {
                    "ACTIVE" => query.Where(cd => cd.IsActive && cd.ValidFrom <= now && cd.ValidTo > now),
                    "INACTIVE" => query.Where(cd => !cd.IsActive),
                    "EXPIRED" => query.Where(cd => cd.ValidTo < now),
                    "UPCOMING" => query.Where(cd => cd.ValidFrom > now),
                    _ => query
                };
            }

            // Search in code or name
            if (!string.IsNullOrEmpty(searchText))
            {
                query = query.Where(cd => cd.Code.Contains(searchText) || cd.Name.Contains(searchText));
            }

            return await query
                .OrderByDescending(cd => cd.CreatedAt)
                .ToListAsync();
        }

        // ดึงข้อมูลตาม ID
        public async Task<CouponDefinition?> GetByIdAsync(int id)
        {
            using var context = new CouponContext();

            return await context.CouponDefinitions
                .Include(cd => cd.CodeGenerator)
                .Include(cd => cd.GeneratedCoupons)
                .Include(cd => cd.CouponType) // Include CouponType navigation
                .FirstOrDefaultAsync(cd => cd.Id == id);
        }

        // ดึงข้อมูลตาม Code
        public async Task<CouponDefinition?> GetByCodeAsync(string code)
        {
            using var context = new CouponContext();

            return await context.CouponDefinitions
                .Include(cd => cd.CodeGenerator)
                .FirstOrDefaultAsync(cd => cd.Code == code);
        }

        // Get unique CouponTypes that are used in CouponDefinitions
        public async Task<List<CouponType>> GetUsedCouponTypesAsync()
        {
            using var context = new CouponContext();

            return await context.CouponDefinitions
                .Where(cd => cd.CouponTypeId > 0)
                .Select(cd => cd.CouponType!)
                .Distinct()
                .Where(ct => ct != null)
                .ToListAsync();
        }

        // สร้างใหม่
        public async Task<CouponDefinition> CreateAsync(CreateCouponRequest request, string userId)
        {
            try
            {
                // Debug: Log the incoming request
                System.Diagnostics.Debug.WriteLine($"CreateAsync called with Type: {request.CouponTypeId}");
                
                // ตรวจสอบ Code ซ้ำ
                var existingCode = await GetByCodeAsync(request.Code);
                if (existingCode != null)
                {
                    throw new InvalidOperationException($"รหัสคูปอง '{request.Code}' มีอยู่แล้ว");
                }

                // Validate that CouponType exists
                using var contextCheck = new CouponContext();
                var couponTypeExists = await contextCheck.CouponTypes.AnyAsync(ct => ct.Id == request.CouponTypeId);
                if (!couponTypeExists)
                {
                    throw new InvalidOperationException($"ไม่พบประเภทคูปอง ID: {request.CouponTypeId}");
                }

                // Validate parameters (simple coupon only)
                ValidateParameters(request.Params);

                using var context = new CouponContext();
                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    // สร้าง CouponDefinition
                    var couponDefinition = new CouponDefinition
                    {
                        Code = request.Code,
                        Name = request.Name,
                        CouponTypeId = request.CouponTypeId, // ตอนนี้ Type เป็น int แล้ว
                        Price = request.Price,
                        Params = request.Params,
                        ValidFrom = request.ValidFrom,
                        ValidTo = request.ValidTo,
                        CreatedBy = userId
                    };

                    System.Diagnostics.Debug.WriteLine($"Adding CouponDefinition with Type: {couponDefinition.CouponTypeId}");
                    
                    context.CouponDefinitions.Add(couponDefinition);
                    await context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"CouponDefinition saved with ID: {couponDefinition.Id}");

                    // สร้าง Code Generator
                    var codeGenerator = new CouponCodeGenerator
                    {
                        CouponDefinitionId = couponDefinition.Id,
                        Prefix = request.Prefix,
                        Suffix = request.Suffix,
                        SequenceLength = request.SequenceLength,
                        UpdatedBy = userId,
                        UpdatedAt = DateTime.Now
                    };

                    context.CouponCodeGenerators.Add(codeGenerator);
                    await context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    couponDefinition.CodeGenerator = codeGenerator;
                    return couponDefinition;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Exception in transaction: {ex}");
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception in CreateAsync: {ex}");
                throw;
            }
        }

        // แก้ไข
        public async Task<bool> UpdateAsync(int id, CreateCouponRequest request, string userId)
        {
            using var context = new CouponContext();

            var existing = await context.CouponDefinitions
                .Include(cd => cd.CodeGenerator)
                .Include(cd => cd.GeneratedCoupons)
                .FirstOrDefaultAsync(cd => cd.Id == id);

            if (existing == null) return false;

            // ตรวจสอบ Code ซ้ำ (ยกเว้นตัวเอง)
            if (existing.Code != request.Code)
            {
                var existingCode = await context.CouponDefinitions
                    .FirstOrDefaultAsync(cd => cd.Code == request.Code);
                if (existingCode != null)
                {
                    throw new InvalidOperationException($"รหัสคูปอง '{request.Code}' มีอยู่แล้ว");
                }
            }

            // Validate parameters (simple coupon only)
            ValidateParameters(request.Params);

            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                // Update CouponDefinition
                existing.Code = request.Code;
                existing.Name = request.Name;
                existing.CouponTypeId = request.CouponTypeId; // ตอนนี้ Type เป็น int แล้ว
                existing.Price = request.Price;
                existing.Params = request.Params;
                existing.ValidFrom = request.ValidFrom;
                existing.ValidTo = request.ValidTo;
                existing.UpdatedBy = userId;
                existing.UpdatedAt = DateTime.Now;

                // Update Code Generator
                if (existing.CodeGenerator != null)
                {
                    existing.CodeGenerator.Prefix = request.Prefix;
                    existing.CodeGenerator.Suffix = request.Suffix;
                    existing.CodeGenerator.SequenceLength = request.SequenceLength;
                    existing.CodeGenerator.UpdatedBy = userId;
                    existing.CodeGenerator.UpdatedAt = DateTime.Now;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // เปิด/ปิดการใช้งาน
        public async Task<bool> SetActiveAsync(int id, bool isActive, string userId)
        {
            using var context = new CouponContext();

            var existing = await context.CouponDefinitions
                .Include(cd => cd.CodeGenerator)
                .FirstOrDefaultAsync(cd => cd.Id == id);

            if (existing == null) return false;

            existing.IsActive = isActive;
            existing.UpdatedBy = userId;
            existing.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();
            return true;
        }

        // Preview คูปอง
        public Task<CouponPreviewResponse> PreviewAsync(CouponPreviewRequest request)
        {
            try
            {
                // Validate params generically (simple coupon only)
                ValidateParameters(request.Params);

                if (!string.IsNullOrWhiteSpace(request.Params))
                {
                    try
                    {
                        var cp = JsonSerializer.Deserialize<CouponParams>(request.Params);
                        if (cp != null && cp.value > 0) return Task.FromResult(new CouponPreviewResponse { Description = cp.GetDescription(), IsValid = true });
                    }
                    catch { }
                }

                return Task.FromResult(new CouponPreviewResponse { IsValid = false, ErrorMessage = "พารามิเตอร์คูปองไม่ถูกต้อง" });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new CouponPreviewResponse
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        // Preview รหัสคูปอง
        public async Task<CodePreviewResponse> PreviewCodeAsync(CodePreviewRequest request)
        {
            try
            {
                var definition = await GetByIdAsync(request.CouponDefinitionId);
                if (definition?.CodeGenerator == null)
                {
                    return new CodePreviewResponse
                    {
                        IsValid = false,
                        ErrorMessage = "ไม่พบข้อมูลคำนิยามคูปองหรือ Code Generator"
                    };
                }

                return new CodePreviewResponse
                {
                    PreviewCode = definition.CodeGenerator.PreviewCode(request.Quantity),
                    IsValid = true
                };
            }
            catch (Exception ex)
            {
                return new CodePreviewResponse
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // สร้างคูปอง
        public async Task<GenerateCouponsResponse> GenerateCouponsAsync(GenerateCouponsRequest request)
        {
            try
            {
                // Load definition within local context so updates and generated coupons are on the same context/transaction
                using var context = new CouponContext();

                var definition = await context.CouponDefinitions
                    .Include(d => d.CodeGenerator)
                    .FirstOrDefaultAsync(d => d.Id == request.CouponDefinitionId);

                if (definition?.CodeGenerator == null)
                {
                    throw new InvalidOperationException("ไม่พบข้อมูลคำนิยามคูปองหรือ Code Generator");
                }

                if (!definition.IsCurrentlyValid)
                {
                    throw new InvalidOperationException("คำนิยามคูปองไม่ได้อยู่ในสถานะใช้งานได้");
                }

                var generatedCodes = new List<string>();
                var currentBatch = await GetNextBatchNumberAsync(request.CouponDefinitionId);

                using var transaction = await context.Database.BeginTransactionAsync();
                try
                {
                    // สร้างคูปอง
                    for (int i = 1; i <= request.Quantity; i++)
                    {
                        var sequence = definition.CodeGenerator.CurrentSequence + i;
                        var code = GenerateCode(definition.CodeGenerator, sequence);

                        var generatedCoupon = new GeneratedCoupon
                        {
                            CouponDefinitionId = request.CouponDefinitionId,
                            GeneratedCode = code,
                            BatchNumber = currentBatch,
                            CreatedBy = request.CreatedBy
                        };

                        context.GeneratedCoupons.Add(generatedCoupon);
                        generatedCodes.Add(code);
                    }

                    // อัปเดต Code Generator
                    definition.CodeGenerator.CurrentSequence += request.Quantity;
                    definition.CodeGenerator.GeneratedCount += request.Quantity;
                    definition.CodeGenerator.UpdatedBy = request.CreatedBy;
                    definition.CodeGenerator.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new GenerateCouponsResponse
                    {
                        CouponDefinitionId = request.CouponDefinitionId,
                        GeneratedQuantity = request.Quantity,
                        BatchNumber = currentBatch,
                        Message = "สร้างคูปองเรียบร้อย",
                        GeneratedCodes = generatedCodes
                    };
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"ไม่สามารถสร้างคูปองได้: {ex.Message}");
            }
        }

        // Helper methods
        private async Task<int> GetNextBatchNumberAsync(int couponDefinitionId)
        {
            using var context = new CouponContext();

            var maxBatch = await context.GeneratedCoupons
                .Where(gc => gc.CouponDefinitionId == couponDefinitionId)
                .MaxAsync(gc => (int?)gc.BatchNumber);

            return (maxBatch ?? 0) + 1;
        }

        private string GenerateCode(CouponCodeGenerator generator, int sequence)
        {
            if (generator == null)
                throw new ArgumentNullException(nameof(generator));

            if (generator.SequenceLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(generator.SequenceLength), "SequenceLength ต้องมากกว่า 0");

            var paddedSequence = sequence.ToString().PadLeft(generator.SequenceLength, '0');
            return $"{generator.Prefix}{paddedSequence}{generator.Suffix}";
        }

        private void ValidateParameters(string paramsJson)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ValidateParameters called. params='{paramsJson}'");

                if (string.IsNullOrWhiteSpace(paramsJson))
                    throw new ArgumentException("พารามิเตอร์คูปองไม่สามารถเป็นค่าว่างได้");

                // Only support simple COUPON shape now
                try
                {
                    var couponParams = JsonSerializer.Deserialize<CouponParams>(paramsJson);
                    if (couponParams == null)
                        throw new ArgumentException("รูปแบบพารามิเตอร์ไม่ถูกต้องหรือไม่รองรับ");

                    if (couponParams.value <= 0)
                        throw new ArgumentException("มูลค่าคูปองต้องมากกว่า 0");

                    return; // valid
                }
                catch (JsonException)
                {
                    throw new ArgumentException("รูปแบบพารามิเตอร์ไม่ถูกต้องหรือไม่รองรับ");
                }
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ValidateParameters unexpected error: {ex.Message}");
                throw;
            }
        }
    }
}