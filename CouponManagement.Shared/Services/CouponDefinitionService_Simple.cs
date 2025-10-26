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
 if (string.IsNullOrWhiteSpace(code)) return null;
 var normalized = code.Trim().ToUpperInvariant();
 using var context = new CouponContext();

 // Normalize comparison to avoid case-sensitivity issues
 return await context.CouponDefinitions
 .Include(cd => cd.CodeGenerator)
 .FirstOrDefaultAsync(cd => cd.Code.ToUpper() == normalized);
 }

 // Get unique CouponTypes that are used in CouponDefinitions
 public async Task<List<CouponType>> GetUsedCouponTypesAsync()
 {
 using var context = new CouponContext();

 return await context.CouponDefinitions
 .Where(cd => cd.CouponTypeId >0)
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

 // normalize code
 var normalizedCode = (request.Code ?? string.Empty).Trim().ToUpperInvariant();

 // ตรวจสอบ Code ซ้ำ
 var existingCode = await GetByCodeAsync(normalizedCode);
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
 Code = normalizedCode,
 Name = request.Name,
 CouponTypeId = request.CouponTypeId, // ตอนนี้ Type เป็น int แล้ว
 Price = request.Price,
 Params = request.Params,
 ValidFrom = request.ValidFrom,
 ValidTo = request.ValidTo,
 CreatedBy = userId,
 IsLimited = request.IsLimited
 };

 System.Diagnostics.Debug.WriteLine($"Adding CouponDefinition with Type: {couponDefinition.CouponTypeId}");

 context.CouponDefinitions.Add(couponDefinition);
 try
 {
 await context.SaveChangesAsync();
 }
 catch (DbUpdateException dbEx)
 {
 var inner = dbEx.InnerException?.Message ?? dbEx.Message;
 if (inner.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >=0 || inner.IndexOf("UK_CouponDefinitions_Code", StringComparison.OrdinalIgnoreCase) >=0 || inner.IndexOf("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase) >=0)
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
 if (inner.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >=0 || inner.IndexOf("UK_CouponDefinitions_Code", StringComparison.OrdinalIgnoreCase) >=0 || inner.IndexOf("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase) >=0)
 {
 throw new InvalidOperationException($"รหัสคูปอง '{request.Code}' มีอยู่แล้ว");
 }

 // check if unique constraint on prefix/suffix caused failure
 if (inner.IndexOf("UK_CouponCodeGenerators_PrefixSuffix", StringComparison.OrdinalIgnoreCase) >=0 || inner.IndexOf("IX_CouponCodeGenerators_PrefixSuffix", StringComparison.OrdinalIgnoreCase) >=0)
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
 if (inner.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >=0 || inner.IndexOf("UK_CouponDefinitions_Code", StringComparison.OrdinalIgnoreCase) >=0 || inner.IndexOf("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase) >=0)
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
 existing.CouponTypeId = request.CouponTypeId; // ตอนนี้ Type เป็น int แล้ว
 existing.Price = request.Price;
 existing.Params = request.Params;
 existing.ValidFrom = request.ValidFrom;
 existing.ValidTo = request.ValidTo;
 existing.UpdatedBy = userId;
 existing.UpdatedAt = DateTime.Now;

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
 if (inner.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >=0 || inner.IndexOf("UK_CouponDefinitions_Code", StringComparison.OrdinalIgnoreCase) >=0 || inner.IndexOf("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase) >=0)
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
 public async Task<GenerateCouponsResponse> GenerateCouponsAsync(GenerateCouponsRequest request)
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
 // สร้างคูปอง
 for (int i =1; i <= request.Quantity; i++)
 {
 var sequence = definition.CodeGenerator.CurrentSequence + i;
 var code = GenerateCode(definition.CodeGenerator, sequence);

 var generatedCoupon = new GeneratedCoupon
 {
 CouponDefinitionId = request.CouponDefinitionId,
 GeneratedCode = code,
 BatchNumber = currentBatch,
 CreatedBy = request.CreatedBy,
 // copy definition expiration to generated coupon
 ExpiresAt = definition.ValidTo
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

 return (maxBatch ??0) +1;
 }

 private string GenerateCode(CouponCodeGenerator generator, int sequence)
 {
 if (generator == null)
 throw new ArgumentNullException(nameof(generator));

 if (generator.SequenceLength <=0)
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
 }
}