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
        string? searchText = null,
        string? saleEventFilter = null) // added saleEventFilter for Sale Event dropdown filtering
        {
            using var context = new CouponContext();

            var query = context.CouponDefinitions
            .Include(cd => cd.CodeGenerator)
            .Include(cd => cd.Branch) // Include Branch navigation
            .Include(cd => cd.SaleEvent) // <-- Ensure SaleEvent is loaded so SaleEventName works
            .AsNoTracking() // Ensure fresh data is loaded and not tracked to avoid stale cached entities
            .AsQueryable();

            // Filter by Type (which is now CouponTypeId / BranchId)
            if (!string.IsNullOrEmpty(typeFilter) && typeFilter != "ALL")
            {
                if (int.TryParse(typeFilter, out int typeId))
                {
                    query = query.Where(cd => cd.BranchId == typeId);
                }
            }

            // Filter by SaleEvent (explicit dropdown selection)
            if (!string.IsNullOrEmpty(saleEventFilter) && saleEventFilter != "ALL")
            {
                if (int.TryParse(saleEventFilter, out int saleEventId))
                {
                    query = query.Where(cd => cd.SaleEventId == saleEventId);
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

            // Search in code, name or sale event name (case-insensitive LIKE)
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var s = searchText.Trim();
                query = query.Where(cd =>
                    (cd.Code != null && EF.Functions.Like(cd.Code, $"%{s}%")) ||
                    (cd.Name != null && EF.Functions.Like(cd.Name, $"%{s}%")) ||
                    (cd.SaleEvent != null && cd.SaleEvent.Name != null && EF.Functions.Like(cd.SaleEvent.Name, $"%{s}%"))
                );
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
            .Include(cd => cd.Branch) // Include Branch navigation
            .Include(cd => cd.SaleEvent) // <-- include SaleEvent so caller sees SaleEvent and SaleEventName
            .FirstOrDefaultAsync(cd => cd.Id == id);
        }

        // ดึงข้อมูลตาม Code
        public async Task<CouponDefinition?> GetByCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var normalized = code.Trim().ToUpperInvariant();
            using var context = new CouponContext();

            // Normalize comparison to avoid case-sensitivity issues
            return await context.CouponDefinitions
            .Include(cd => cd.CodeGenerator)
            .Include(cd => cd.SaleEvent) // include SaleEvent for callers that need it
            .FirstOrDefaultAsync(cd => cd.Code.ToUpper() == normalized);
        }

        // Get unique CouponTypes that are used in CouponDefinitions
        public async Task<List<Branch>> GetUsedBranchesAsync()
        {
            using var context = new CouponContext();

            return await context.CouponDefinitions
            .Where(cd => cd.BranchId > 0)
            .Select(cd => cd.Branch!)
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
                System.Diagnostics.Debug.WriteLine($"CreateAsync called with BranchId: {request.BranchId}");

                // normalize code
                var normalizedCode = (request.Code ?? string.Empty).Trim().ToUpperInvariant();

                // ตรวจสอบ Code ซ้ำ
                var existingCode = await GetByCodeAsync(normalizedCode);
                if (existingCode != null)
                {
                    throw new InvalidOperationException($"รหัสคูปอง '{request.Code}' มีอยู่แล้ว");
                }

                // Validate that Branch exists
                using var contextCheck = new CouponContext();
                var branchExists = await contextCheck.Branches.AnyAsync(b => b.Id == request.BranchId);
                if (!branchExists)
                {
                    throw new InvalidOperationException($"ไม่พบสาขา ID: {request.BranchId}");
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
                        Code = normalizedCode,
                        Name = request.Name,
                        BranchId = request.BranchId, // ตอนนี้ Type เป็น int แล้ว
                        Price = request.Price,
                        Params = request.Params,
                        ValidFrom = request.ValidFrom,
                        ValidTo = request.ValidTo,
                        CreatedBy = userId,
                        IsLimited = request.IsLimited,
                        SaleEventId = request.SaleEventId // <-- persist SaleEventId from request
                    };

                    System.Diagnostics.Debug.WriteLine($"Adding CouponDefinition with Branch: {couponDefinition.BranchId}");

                    context.CouponDefinitions.Add(couponDefinition);
                    try
                    {
                        await context.SaveChangesAsync();
                    }
                    catch (DbUpdateException dbEx)
                    {
                        var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                        if (inner.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 || inner.IndexOf("UK_CouponDefinitions_Code", StringComparison.OrdinalIgnoreCase) >= 0 || inner.IndexOf("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            throw new InvalidOperationException($"รหัสคูปอง '{request.Code}' มีอยู่แล้ว");
                        }

                        throw;
                    }

                    System.Diagnostics.Debug.WriteLine($"CouponDefinition saved with ID: {couponDefinition.Id}");

                    // สร้าง Code Generator only when IsLimited == true
                    if (request.IsLimited)
                    {
                        // ensure prefix/suffix uniqueness
                        var existingGenerator = await GetCodeGeneratorByPrefixSuffixAsync(request.Prefix, request.Suffix);
                        if (existingGenerator != null)
                        {
                            throw new InvalidOperationException($"รหัสหน้า/รหัสหลัง '{request.Prefix}{request.Suffix}' ถูกใช้งานโดยคำนิยามอื่น");
                        }

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
                        try
                        {
                            await context.SaveChangesAsync();
                        }
                        catch (DbUpdateException dbEx)
                        {
                            var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                            if (inner.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 || inner.IndexOf("UK_CouponDefinitions_Code", StringComparison.OrdinalIgnoreCase) >= 0 || inner.IndexOf("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                throw new InvalidOperationException($"รหัสคูปอง '{request.Code}' มีอยู่แล้ว");
                            }

                            // check if unique constraint on prefix/suffix caused failure
                            if (inner.IndexOf("UK_CouponCodeGenerators_PrefixSuffix", StringComparison.OrdinalIgnoreCase) >= 0 || inner.IndexOf("IX_CouponCodeGenerators_PrefixSuffix", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                throw new InvalidOperationException($"รหัสหน้า/รหัสหลัง '{request.Prefix}{request.Suffix}' ถูกใช้งานโดยคำนิยามอื่น");
                            }

                            throw;
                        }

                        couponDefinition.CodeGenerator = codeGenerator;
                    }

                    await transaction.CommitAsync();

                    return couponDefinition;
                }
                catch (DbUpdateException dbEx)
                {
                    await transaction.RollbackAsync();
                    var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                    if (inner.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 || inner.IndexOf("UK_CouponDefinitions_Code", StringComparison.OrdinalIgnoreCase) >= 0 || inner.IndexOf("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        throw new InvalidOperationException($"รหัสคูปอง '{request.Code}' มีอยู่แล้ว");
                    }
                    throw;
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

            // normalize code
            var normalizedCode = (request.Code ?? string.Empty).Trim().ToUpperInvariant();

            // ตรวจสอบ Code ซ้ำ (ยกเว้นตัวเอง)
            if (!string.Equals(existing.Code, normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                var existingCode = await context.CouponDefinitions
                .FirstOrDefaultAsync(cd => cd.Code.ToUpper() == normalizedCode);
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
                existing.Code = normalizedCode;
                existing.Name = request.Name;
                existing.BranchId = request.BranchId; // ตอนนี้ Type เป็น int แล้ว
                existing.Price = request.Price;
                existing.Params = request.Params;
                existing.ValidFrom = request.ValidFrom;
                existing.ValidTo = request.ValidTo;
                existing.UpdatedBy = userId;
                existing.UpdatedAt = DateTime.Now;
                existing.SaleEventId = request.SaleEventId; // <-- persist updated SaleEventId

                // Update IsLimited flag and manage CodeGenerator accordingly
                var wasLimited = existing.IsLimited;
                // New rule: once a definition is created as Limited or Unlimited it cannot be toggled to the other mode.
                if (wasLimited != request.IsLimited)
                {
                    throw new InvalidOperationException("ไม่สามารถเปลี่ยนประเภทคูปองได้ กรุณาสร้างคำนิยามใหม่หากต้องการเปลี่ยนประเภท");
                }

                existing.IsLimited = request.IsLimited;

                // Since toggling is not allowed, only update generator settings if exists and still limited
                if (existing.CodeGenerator != null && request.IsLimited)
                {
                    existing.CodeGenerator.Prefix = request.Prefix;
                    existing.CodeGenerator.Suffix = request.Suffix;
                    existing.CodeGenerator.SequenceLength = request.SequenceLength;
                    existing.CodeGenerator.UpdatedBy = userId;
                    existing.CodeGenerator.UpdatedAt = DateTime.Now;
                }

                try
                {
                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (DbUpdateException dbEx)
                {
                    await transaction.RollbackAsync();
                    var inner = dbEx.InnerException?.Message ?? dbEx.Message;
                    if (inner.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 || inner.IndexOf("UK_CouponDefinitions_Code", StringComparison.OrdinalIgnoreCase) >= 0 || inner.IndexOf("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        throw new InvalidOperationException($"รหัสคูปอง '{request.Code}' มีอยู่แล้ว");
                    }

                    throw;
                }
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
                        if (cp != null)
                        {
                            // Use provided Params (description). Price is validated separately if needed.
                            return Task.FromResult(new CouponPreviewResponse { Description = cp.GetDescription(), IsValid = true });
                        }
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
                if (definition == null)
                {
                    return new CodePreviewResponse
                    {
                        IsValid = false,
                        ErrorMessage = "ไม่พบข้อมูลคำนิยามคูปอง"
                    };
                }

                if (!definition.IsLimited || definition.CodeGenerator == null)
                {
                    return new CodePreviewResponse
                    {
                        IsValid = false,
                        ErrorMessage = "คูปองนี้ไม่มีการตั้งค่ารหัส"
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
        public async Task<GenerateCouponsResponse> GenerateCouponsAsync(GenerateCouponsRequest request, int? startSequence = null, int? endSequence = null)
        {
            try
            {
                // Load definition within local context so updates and generated coupons are on the same context/transaction
                using var context = new CouponContext();

                var definition = await context.CouponDefinitions
                .Include(d => d.CodeGenerator)
                .FirstOrDefaultAsync(d => d.Id == request.CouponDefinitionId);

                if (definition == null)
                {
                    throw new InvalidOperationException("ไม่พบข้อมูลคำนิยามคูปอง");
                }

                if (!definition.IsLimited)
                {
                    throw new InvalidOperationException("คูปองนี้เป็นแบบไม่จำกัดจำนวน - ไม่มีรหัสให้สร้าง");
                }

                if (definition?.CodeGenerator == null)
                {
                    throw new InvalidOperationException("ไม่พบข้อมูล Code Generator สำหรับคูปองนี้");
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
                    // determine start/end sequences and actual quantity
                    int seqLen = Math.Max(1, definition.CodeGenerator.SequenceLength);
                    int startSeq;
                    int endSeq;
                    int actualQty;

                    if (startSequence.HasValue && endSequence.HasValue)
                    {
                        startSeq = startSequence.Value;
                        endSeq = endSequence.Value;
                        if (startSeq > endSeq) throw new InvalidOperationException("เลขเริ่มต้นต้องไม่มากกว่าเลขสิ้นสุด");
                        actualQty = endSeq - startSeq + 1;
                    }
                    else if (startSequence.HasValue && !endSequence.HasValue)
                    {
                        startSeq = startSequence.Value;
                        actualQty = request.Quantity;
                        endSeq = startSeq + actualQty - 1;
                    }
                    else if (!startSequence.HasValue && endSequence.HasValue)
                    {
                        endSeq = endSequence.Value;
                        actualQty = request.Quantity;
                        startSeq = endSeq - actualQty + 1;
                        if (startSeq < 1) startSeq = 1;
                    }
                    else
                    {
                        // default: continue from current sequence
                        startSeq = definition.CodeGenerator.CurrentSequence + 1;
                        actualQty = request.Quantity;
                        endSeq = startSeq + actualQty - 1;
                    }

                    if (actualQty <= 0) throw new InvalidOperationException("จำนวนที่จะสร้างต้องไม่น้อยกว่า1");
                    if (actualQty > 10000) throw new InvalidOperationException("จำนวนการสร้างต่อครั้งต้องไม่เกิน10,000");

                    // Validate duplicates: parse existing sequences
                    var existingCodes = await context.GeneratedCoupons
                    .Where(gc => gc.CouponDefinitionId == request.CouponDefinitionId)
                    .Select(gc => gc.GeneratedCode)
                    .ToListAsync();

                    var existingSeqs = new HashSet<int>();
                    foreach (var code in existingCodes)
                    {
                        var parsed = TryParseSequence(code, definition.CodeGenerator.Prefix, definition.CodeGenerator.Suffix, seqLen);
                        if (parsed.HasValue) existingSeqs.Add(parsed.Value);
                    }

                    var duplicates = new List<int>();
                    for (int s = startSeq; s <= endSeq; s++)
                    {
                        if (existingSeqs.Contains(s)) duplicates.Add(s);
                    }

                    if (duplicates.Any())
                    {
                        var sample = string.Join(",", duplicates.Take(10));
                        throw new InvalidOperationException($"ช่วงที่ระบุมีรหัสซ้ำอยู่แล้ว (ตัวอย่าง): {sample}{(duplicates.Count > 10 ? ", ..." : "")} ");
                    }

                    // create coupons
                    for (int seq = startSeq; seq <= endSeq; seq++)
                    {
                        var code = GenerateCode(definition.CodeGenerator, seq);
                        var generatedCoupon = new GeneratedCoupon
                        {
                            CouponDefinitionId = request.CouponDefinitionId,
                            GeneratedCode = code,
                            BatchNumber = currentBatch,
                            CreatedBy = request.CreatedBy,
                            ExpiresAt = definition.ValidTo
                        };
                        context.GeneratedCoupons.Add(generatedCoupon);
                        generatedCodes.Add(code);
                    }

                    // Update Code Generator: advance CurrentSequence if endSeq is greater
                    definition.CodeGenerator.CurrentSequence = Math.Max(definition.CodeGenerator.CurrentSequence, endSeq);
                    definition.CodeGenerator.GeneratedCount += actualQty;
                    definition.CodeGenerator.UpdatedBy = request.CreatedBy;
                    definition.CodeGenerator.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new GenerateCouponsResponse
                    {
                        CouponDefinitionId = request.CouponDefinitionId,
                        GeneratedQuantity = actualQty,
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

        // helper to parse sequence number from code
        private int? TryParseSequence(string code, string prefix, string suffix, int seqLen)
        {
            try
            {
                if (string.IsNullOrEmpty(code)) return null;
                var p = (prefix ?? string.Empty).Length;
                if (code.Length < p + seqLen) return null;
                var seqStr = code.Substring(p, seqLen);
                if (int.TryParse(seqStr, out var n)) return n;
                return null;
            }
            catch { return null; }
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
                throw new ArgumentOutOfRangeException(nameof(generator.SequenceLength), "SequenceLength ต้องมากกว่า0");

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

                    // 'value' field removed; Price is validated elsewhere. Accept params as long as shape is correct.

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

        public async Task<CouponCodeGenerator?> GetCodeGeneratorByPrefixSuffixAsync(string prefix, string suffix)
        {
            if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(suffix)) return null;
            var p = (prefix ?? string.Empty).Trim().ToUpperInvariant();
            var s = (suffix ?? string.Empty).Trim().ToUpperInvariant();
            using var context = new CouponContext();
            return await context.CouponCodeGenerators
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Prefix.ToUpper() == p && g.Suffix.ToUpper() == s);
        }

        // ดึงสถิติรวมของคูปองทั้งหมด
        public async Task<CouponDefinitionStatistics> GetStatisticsAsync()
        {
            using var context = new CouponContext();

            // นับจำนวนคูปองทั้งหมดที่สร้าง
            var totalGenerated = await context.GeneratedCoupons.CountAsync();

            // นับจำนวนคงเหลือ (ไม่ใช้และไม่ขาย)
            var totalRemaining = await context.GeneratedCoupons
         .CountAsync(gc => !gc.IsUsed && gc.ReceiptItemId == null);

            // นับจำนวนที่ขายแล้ว (มี ReceiptItemId)
            var totalSold = await context.GeneratedCoupons
      .CountAsync(gc => gc.ReceiptItemId != null);

            // นับจำนวนที่ใช้แล้ว (IsUsed = true)
            var totalUsed = await context.GeneratedCoupons
              .CountAsync(gc => gc.IsUsed);

            return new CouponDefinitionStatistics
            {
                TotalGenerated = totalGenerated,
                TotalRemaining = totalRemaining,
                TotalSold = totalSold,
                TotalUsed = totalUsed
            };
        }

        // ดึงสถิติของ CouponDefinition แต่ละตัว
        public async Task<CouponDefinitionStatistics> GetStatisticsByDefinitionIdAsync(int couponDefinitionId)
        {
            using var context = new CouponContext();

            // นับจำนวนคูปองที่สร้างสำหรับ definition นี้
            var totalGenerated = await context.GeneratedCoupons
     .CountAsync(gc => gc.CouponDefinitionId == couponDefinitionId);

            // นับจำนวนคงเหลือ (ไม่ใช้และไม่ขาย)
            var totalRemaining = await context.GeneratedCoupons
      .CountAsync(gc => gc.CouponDefinitionId == couponDefinitionId
        && !gc.IsUsed
 && gc.ReceiptItemId == null);

            // นับจำนวนที่ขายแล้ว (มี ReceiptItemId)
            var totalSold = await context.GeneratedCoupons
               .CountAsync(gc => gc.CouponDefinitionId == couponDefinitionId
           && gc.ReceiptItemId != null);

            // นับจำนวนที่ใช้แล้ว (IsUsed = true)
            var totalUsed = await context.GeneratedCoupons
                    .CountAsync(gc => gc.CouponDefinitionId == couponDefinitionId
         && gc.IsUsed);

            return new CouponDefinitionStatistics
            {
                TotalGenerated = totalGenerated,
                TotalRemaining = totalRemaining,
                TotalSold = totalSold,
                TotalUsed = totalUsed
            };
        }

        #region Cleanup Unused / Expired Coupon Definitions

        /// <summary>
        /// ดึงรายการคำนิยามคูปองที่หมดอายุ / งานจบแล้ว และไม่เคยผูกกับใบเสร็จ (ทั้งที่ยังไม่สร้าง หรือสร้างแล้วแต่ไม่เคยใช้)
        /// </summary>
        public async Task<List<CleanupCouponDefinitionItem>> GetCleanupEligibleDefinitionsAsync(string? searchText = null, int? branchId = null)
        {
            using var context = new CouponContext();
            await context.EnsureDeletionHistoryTableCreatedAsync();

            var today = DateTime.Today;

            // ค้นหา CouponDefinitions พร้อม Branch, SaleEvent, GeneratedCoupons
            var query = context.CouponDefinitions
                .Include(cd => cd.Branch)
                .Include(cd => cd.SaleEvent)
                .Include(cd => cd.GeneratedCoupons)
                .AsNoTracking()
                .AsQueryable();

            // 1) วันหมดอายุของคูปองต้องหมดแล้ว (ValidTo.Date < today)
            query = query.Where(cd => cd.ValidTo.Date < today);

            // 2) ถ้ามีงานขาย งานขายนั้นต้องจบไปแล้วด้วย (SaleEvent.EndDate.Date < today)
            query = query.Where(cd => cd.SaleEventId == null || (cd.SaleEvent != null && cd.SaleEvent.EndDate.Date < today));

            // 3) ถ้ามีการสร้างคูปองแล้ว ต้องไม่มีใบใดผูกกับใบเสร็จ (ReceiptItemId != null) หรือถูกใช้งาน (IsUsed == true)
            query = query.Where(cd => !cd.GeneratedCoupons.Any(gc => gc.ReceiptItemId != null || gc.IsUsed));

            // 4) ตรวจสอบความปลอดภัยสูงสุด: ต้องไม่มีรายการใบเสร็จใดๆ ที่อ้างอิงถึง CouponDefinitionId นี้
            var soldCouponDefIds = await context.ReceiptItems
                .Select(ri => ri.CouponId)
                .Distinct()
                .ToListAsync();

            if (soldCouponDefIds.Count > 0)
            {
                query = query.Where(cd => !soldCouponDefIds.Contains(cd.Id));
            }

            // กรองสาขา
            if (branchId.HasValue && branchId.Value > 0)
            {
                query = query.Where(cd => cd.BranchId == branchId.Value);
            }

            // กรองคำค้นหา
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.Trim().ToLower();
                query = query.Where(cd => cd.Code.ToLower().Contains(term) || cd.Name.ToLower().Contains(term));
            }

            var list = await query.OrderByDescending(cd => cd.ValidTo).ToListAsync();

            return list.Select(cd => new CleanupCouponDefinitionItem
            {
                Id = cd.Id,
                Code = cd.Code,
                Name = cd.Name,
                BranchId = cd.BranchId,
                BranchName = cd.Branch?.Name ?? $"สาขา {cd.BranchId}",
                SaleEventId = cd.SaleEventId,
                SaleEventName = cd.SaleEvent?.Name ?? "ไม่มีงานขาย",
                SaleEventEndDate = cd.SaleEvent?.EndDate,
                Price = cd.Price,
                ValidFrom = cd.ValidFrom,
                ValidTo = cd.ValidTo,
                TotalGeneratedCount = cd.GeneratedCoupons.Count,
                CreatedBy = cd.CreatedBy,
                CreatedAt = cd.CreatedAt
            }).ToList();
        }

        /// <summary>
        /// ลบคำนิยามคูปอง และคูปองที่สร้างไว้แต่ไม่เคยใช้ พร้อมบันทึกประวัติ
        /// </summary>
        public async Task<(bool Success, string Message)> DeleteCleanupDefinitionAsync(int couponDefinitionId, string deletedBy, string? reason = null)
        {
            using var context = new CouponContext();
            await context.EnsureDeletionHistoryTableCreatedAsync();

            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var def = await context.CouponDefinitions
                    .Include(d => d.Branch)
                    .Include(d => d.SaleEvent)
                    .Include(d => d.CodeGenerator)
                    .Include(d => d.GeneratedCoupons)
                    .FirstOrDefaultAsync(d => d.Id == couponDefinitionId);

                if (def == null)
                {
                    return (false, "ไม่พบข้อมูลคำนิยามคูปองในระบบ (อาจถูกลบไปแล้ว)");
                }

                var today = DateTime.Today;

                // ตรวจสอบวันหมดอายุซ้ำ
                if (def.ValidTo.Date >= today)
                {
                    return (false, $"คูปองนี้ยังไม่หมดอายุ (วันหมดอายุ: {def.ValidTo:dd/MM/yyyy}) ไม่อนุญาตให้ลบ");
                }

                // ตรวจสอบงานขายซ้ำ
                if (def.SaleEvent != null && def.SaleEvent.EndDate.Date >= today)
                {
                    return (false, $"งานขาย '{def.SaleEvent.Name}' ยังไม่สิ้นสุด (สิ้นสุดวันที่: {def.SaleEvent.EndDate:dd/MM/yyyy}) ไม่อนุญาตให้ลบ");
                }

                // ตรวจสอบความปลอดภัยกับใบเสร็จ
                var hasReceiptItems = await context.ReceiptItems.AnyAsync(ri => ri.CouponId == def.Id);
                if (hasReceiptItems)
                {
                    return (false, "คำนิยามคูปองนี้เคยถูกออกใบเสร็จขายแล้ว ไม่อนุญาตให้ลบเด็ดขาด");
                }

                // ตรวจสอบคูปองที่สร้าง
                var hasUsedOrSoldGeneratedCoupons = def.GeneratedCoupons.Any(gc => gc.ReceiptItemId != null || gc.IsUsed);
                if (hasUsedOrSoldGeneratedCoupons)
                {
                    return (false, "พบว่ามีคูปองที่สร้างถูกผูกกับใบเสร็จหรือถูกใช้งานไปแล้ว ไม่อนุญาตให้ลบ");
                }

                int totalCouponsCreated = def.GeneratedCoupons.Count;

                // บันทึกประวัติการลบ (Audit History)
                var history = new CouponDefinitionDeletionHistory
                {
                    CouponDefinitionId = def.Id,
                    CouponCode = def.Code,
                    CouponName = def.Name,
                    BranchId = def.BranchId,
                    BranchName = def.Branch?.Name ?? $"สาขา {def.BranchId}",
                    SaleEventId = def.SaleEventId,
                    SaleEventName = def.SaleEvent?.Name,
                    Price = def.Price,
                    ValidFrom = def.ValidFrom,
                    ValidTo = def.ValidTo,
                    TotalCouponsCreated = totalCouponsCreated,
                    DeletedBy = string.IsNullOrWhiteSpace(deletedBy) ? Environment.UserName : deletedBy,
                    DeletedAt = DateTime.Now,
                    DeletionReason = reason ?? (totalCouponsCreated > 0
                        ? $"ลบคำนิยามคูปองและคูปองที่สร้างไว้ {totalCouponsCreated} ใบ (ไม่เคยผูกใบเสร็จ)"
                        : "ลบคำนิยามคูปองที่หมดอายุ (ยังไม่เคยสร้างคูปอง)")
                };

                context.CouponDefinitionDeletionHistories.Add(history);
                await context.SaveChangesAsync();

                // ลบคูปองที่สร้างไว้ (ถ้ามี)
                if (def.GeneratedCoupons.Count > 0)
                {
                    context.GeneratedCoupons.RemoveRange(def.GeneratedCoupons);
                }

                // ลบตัวสร้างรหัสคูปอง (ถ้ามี)
                if (def.CodeGenerator != null)
                {
                    context.CouponCodeGenerators.Remove(def.CodeGenerator);
                }

                // ลบตัวคำนิยามคูปอง
                context.CouponDefinitions.Remove(def);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, totalCouponsCreated > 0
                    ? $"ลบคำนิยาม '{def.Code}' และคูปองทั้งหมด {totalCouponsCreated:N0} ใบเรียบร้อยแล้ว"
                    : $"ลบคำนิยาม '{def.Code}' เรียบร้อยแล้ว");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"เกิดข้อผิดพลาดในการลบ: {ex.Message}");
            }
        }

        /// <summary>
        /// ดึงรายการประวัติการลบคำนิยามคูปอง
        /// </summary>
        public async Task<List<CouponDefinitionDeletionHistory>> GetDeletionHistoriesAsync(int page = 1, int pageSize = 100)
        {
            using var context = new CouponContext();
            await context.EnsureDeletionHistoryTableCreatedAsync();

            return await context.CouponDefinitionDeletionHistories
                .AsNoTracking()
                .OrderByDescending(h => h.DeletedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        /// <summary>
        /// ลบคำนิยามคูปองแบบหลายรายการพร้อมกัน (Batch Deletion)
        /// </summary>
        public async Task<(int SuccessCount, int FailCount, int TotalCouponsDeleted, List<string> ErrorMessages)> DeleteCleanupDefinitionsBatchAsync(
            IEnumerable<int> couponDefinitionIds,
            string deletedBy,
            string? reason = null)
        {
            var idList = couponDefinitionIds.Distinct().ToList();
            if (idList.Count == 0)
            {
                return (0, 0, 0, new List<string>());
            }

            using var context = new CouponContext();
            await context.EnsureDeletionHistoryTableCreatedAsync();

            int successCount = 0;
            int totalCouponsDeleted = 0;
            var errorMessages = new List<string>();

            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var today = DateTime.Today;

                var definitions = await context.CouponDefinitions
                    .Include(d => d.Branch)
                    .Include(d => d.SaleEvent)
                    .Include(d => d.CodeGenerator)
                    .Include(d => d.GeneratedCoupons)
                    .Where(d => idList.Contains(d.Id))
                    .ToListAsync();

                // ป้องกันคำนิยามที่เคยออกใบเสร็จ
                var soldCouponDefIds = await context.ReceiptItems
                    .Select(ri => ri.CouponId)
                    .Distinct()
                    .ToListAsync();

                foreach (var def in definitions)
                {
                    if (def.ValidTo.Date >= today)
                    {
                        errorMessages.Add($"คำนิยาม '{def.Code}' ยังไม่หมดอายุ");
                        continue;
                    }

                    if (def.SaleEvent != null && def.SaleEvent.EndDate.Date >= today)
                    {
                        errorMessages.Add($"คำนิยาม '{def.Code}' งานขายยังไม่สิ้นสุด");
                        continue;
                    }

                    if (soldCouponDefIds.Contains(def.Id))
                    {
                        errorMessages.Add($"คำนิยาม '{def.Code}' เคยออกใบเสร็จขายแล้ว");
                        continue;
                    }

                    if (def.GeneratedCoupons.Any(gc => gc.ReceiptItemId != null || gc.IsUsed))
                    {
                        errorMessages.Add($"คำนิยาม '{def.Code}' มีคูปองที่ผูกใบเสร็จหรือใช้งานแล้ว");
                        continue;
                    }

                    int totalCreated = def.GeneratedCoupons.Count;

                    var history = new CouponDefinitionDeletionHistory
                    {
                        CouponDefinitionId = def.Id,
                        CouponCode = def.Code,
                        CouponName = def.Name,
                        BranchId = def.BranchId,
                        BranchName = def.Branch?.Name ?? $"สาขา {def.BranchId}",
                        SaleEventId = def.SaleEventId,
                        SaleEventName = def.SaleEvent?.Name,
                        Price = def.Price,
                        ValidFrom = def.ValidFrom,
                        ValidTo = def.ValidTo,
                        TotalCouponsCreated = totalCreated,
                        DeletedBy = string.IsNullOrWhiteSpace(deletedBy) ? Environment.UserName : deletedBy,
                        DeletedAt = DateTime.Now,
                        DeletionReason = reason ?? (totalCreated > 0
                            ? $"ลบคำนิยามคูปองและคูปองที่สร้างไว้ {totalCreated} ใบ (ลบแบบเลือกหลายรายการ)"
                            : "ลบคำนิยามคูปองที่หมดอายุ (ลบแบบเลือกหลายรายการ)")
                    };
                    context.CouponDefinitionDeletionHistories.Add(history);

                    if (def.GeneratedCoupons.Count > 0)
                    {
                        context.GeneratedCoupons.RemoveRange(def.GeneratedCoupons);
                        totalCouponsDeleted += totalCreated;
                    }

                    if (def.CodeGenerator != null)
                    {
                        context.CouponCodeGenerators.Remove(def.CodeGenerator);
                    }

                    context.CouponDefinitions.Remove(def);
                    successCount++;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (successCount, idList.Count - successCount, totalCouponsDeleted, errorMessages);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                errorMessages.Add($"เกิดข้อผิดพลาดในการลบกลุ่ม: {ex.Message}");
                return (0, idList.Count, 0, errorMessages);
            }
        }

        #endregion
    } // End of CouponDefinitionService class

    // Statistics model
    public class CouponDefinitionStatistics
    {
        public int TotalGenerated { get; set; }
        public int TotalRemaining { get; set; }
        public int TotalSold { get; set; }
        public int TotalUsed { get; set; }
    }

    // Model สำหรับแสดงรายการคำนิยามที่พร้อมลบ
    public class CleanupCouponDefinitionItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int? SaleEventId { get; set; }
        public string SaleEventName { get; set; } = string.Empty;
        public DateTime? SaleEventEndDate { get; set; }
        public decimal Price { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public int TotalGeneratedCount { get; set; }
        public bool HasGeneratedCoupons => TotalGeneratedCount > 0;
        public string StatusBadgeText => HasGeneratedCoupons
            ? $"สร้างแล้ว {TotalGeneratedCount:N0} ใบ (ไม่เคยผูกใบเสร็จ)"
            : "ยังไม่เคยสร้างคูปอง (0 ใบ)";
        public string StatusBadgeBackground => HasGeneratedCoupons ? "#FFF7ED" : "#F3F4F6";
        public string StatusBadgeBorder => HasGeneratedCoupons ? "#FDBA74" : "#D1D5DB";
        public string StatusBadgeForeground => HasGeneratedCoupons ? "#C2410C" : "#4B5563";
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public string PriceDisplay => $"{Price:N2}";
        public string ValidFromDisplay => ValidFrom.ToString("dd/MM/yyyy");
        public string ValidToDisplay => ValidTo.ToString("dd/MM/yyyy");
        public string CreatedAtDisplay => CreatedAt.ToString("dd/MM/yyyy HH:mm");
    }
}