using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Dynamic;
using System.Collections.Generic;

namespace CouponManagement.Web.Controllers
{
    /// <summary>
    /// Controller for receipts operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ReceiptsController : ControllerBase
    {
        /// <summary>
        /// Get status text for display.
        /// </summary>
        /// <param name="status">Receipt status (Active/Cancelled).</param>
        /// <returns>Status text in English.</returns>
        private static string GetStatusText(string status)
        {
            return status switch
            {
                "Active" => "ใช้งาน",
                "Cancelled" => "ยกเลิก",
                _ => status ?? ""
            };
        }

        /// <summary>
        /// Test endpoint for language encoding verification.
        /// </summary>
        /// <returns>Test response with status values.</returns>
        [HttpGet("test-thai")]
        public IActionResult TestThai()
        {
            return Ok(new
            {
                message = "Test Thai Language",
                status = GetStatusText("Active"),
                cancelled = GetStatusText("Cancelled"),
                test = "Hello"
            });
        }

        /// <summary>
        /// Get receipts with optional filters and pagination.
        /// </summary>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Items per page.</param>
        /// <param name="receiptCode">Optional receipt code search text.</param>
        /// <param name="customerName">Optional customer name search text.</param>
        /// <param name="customerPhone">Optional customer phone search text.</param>
        /// <param name="dateFrom">Optional receipt date from datetime filter.</param>
        /// <param name="dateTo">Optional receipt date to datetime filter.</param>
        /// <param name="status">Optional status filter (Active/Cancelled).</param>
        /// <param name="salesPersonId">Optional sales person id filter.</param>
        /// <param name="paymentMethodId">Optional payment method id filter.</param>
        /// <param name="couponCode">Optional coupon code (definition code or generated code) to filter receipts.</param>
        /// <param name="branchId">Optional branch id filter (filters by CouponDefinition.BranchId).</param>
        /// <param name="saleEventId">Optional sale event id filter (filters by CouponDefinition.SaleEventId).</param>
        /// <returns>Paged result of receipts.</returns>
        [HttpGet]
        public async Task<IActionResult> GetReceipts(
            [FromQuery] int page =1,
            [FromQuery] int pageSize =25,
            [FromQuery] string? receiptCode = null,
            [FromQuery] string? customerName = null,
            [FromQuery] string? customerPhone = null,
            [FromQuery] DateTimeOffset? dateFrom = null,
            [FromQuery] DateTimeOffset? dateTo = null,
            [FromQuery] string? status = null,
            [FromQuery] int? salesPersonId = null,
            [FromQuery] int? paymentMethodId = null,
            [FromQuery] string? couponCode = null,
            [FromQuery] int? branchId = null,
            [FromQuery] int? saleEventId = null)
        {
            try
            {
                using var context = new CouponContext();

                var query = context.Receipts.AsQueryable();

                if (!string.IsNullOrWhiteSpace(receiptCode))
                    query = query.Where(r => r.ReceiptCode.Contains(receiptCode));

                if (!string.IsNullOrWhiteSpace(customerName))
                    query = query.Where(r => r.CustomerName.Contains(customerName));

                if (!string.IsNullOrWhiteSpace(customerPhone))
                    query = query.Where(r => r.CustomerPhoneNumber.Contains(customerPhone));

                if (dateFrom.HasValue)
                {
                    var startUtc = dateFrom.Value.UtcDateTime;
                    query = query.Where(r => r.ReceiptDate >= startUtc);
                }

                if (dateTo.HasValue)
                {
                    var endUtc = dateTo.Value.UtcDateTime;
                    query = query.Where(r => r.ReceiptDate <= endUtc);
                }

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(r => r.Status == status);

                if (salesPersonId.HasValue)
                    query = query.Where(r => r.SalesPersonId == salesPersonId.Value);

                if (paymentMethodId.HasValue)
                    query = query.Where(r => r.PaymentMethodId == paymentMethodId.Value);

                if (!string.IsNullOrWhiteSpace(couponCode))
                {
                    string search = couponCode.Trim();

                    var idsFromDefs = await (from ri in context.ReceiptItems
                                             join cd in context.CouponDefinitions on ri.CouponId equals cd.Id
                                             where cd.Code.Contains(search)
                                             select ri.ReceiptId).Distinct().ToListAsync();

                    var idsFromGenerated = await (from gc in context.GeneratedCoupons
                                                   join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                                                   where gc.GeneratedCode.Contains(search)
                                                   select ri.ReceiptId).Distinct().ToListAsync();

                    var matchingReceiptIds = idsFromDefs.Union(idsFromGenerated).Distinct().ToList();

                    if (!matchingReceiptIds.Any())
                    {
                        return Ok(new
                        {
                            items = new List<object>(),
                            page,
                            pageSize,
                            totalCount = 0,
                            totalPages = 0
                        });
                    }

                    query = query.Where(r => matchingReceiptIds.Contains(r.ReceiptID));
                }

                var defQuery = context.CouponDefinitions.AsQueryable();

                if (branchId.HasValue)
                {
                    defQuery = defQuery.Where(cd => cd.BranchId == branchId.Value);
                }

                if (saleEventId.HasValue)
                {
                    defQuery = defQuery.Where(cd => cd.SaleEventId == saleEventId.Value);
                }

                defQuery = defQuery.Where(cd => cd.Branch != null);

                var matchingDefIds = await defQuery
                    .Select(cd => cd.Id)
                    .ToListAsync();

                if (matchingDefIds.Any())
                {
                    query = query.Where(r => context.ReceiptItems.Any(ri => ri.ReceiptId == r.ReceiptID && matchingDefIds.Contains(ri.CouponId)));
                }
                else
                {
                    return Ok(new
                    {
                        items = new List<object>(),
                        page,
                        pageSize,
                        totalCount = 0,
                        totalPages = 0
                    });
                }

                var totalCount = await query.CountAsync();

                var receipts = await query
                    .OrderByDescending(r => r.ReceiptDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var paymentMethods = await context.PaymentMethods.ToDictionaryAsync(pm => pm.Id, pm => pm.Name);
                var salesPersons = await context.SalesPerson.ToDictionaryAsync(sp => sp.ID, sp => sp.Name);

                var items = new List<ExpandoObject>();
                foreach (var r in receipts)
                {
                    dynamic exp = new ExpandoObject();
                    var dict = (IDictionary<string, object?>)exp;

                    dict["ReceiptID"] = r.ReceiptID;
                    dict["ReceiptCode"] = r.ReceiptCode;
                    dict["ReceiptDate"] = r.ReceiptDate;
                    dict["CustomerName"] = r.CustomerName;
                    dict["CustomerPhoneNumber"] = r.CustomerPhoneNumber;
                    dict["Discount"] = r.Discount;
                    dict["TotalAmount"] = r.TotalAmount;
                    dict["Status"] = r.Status;
                    dict["SalesPersonId"] = r.SalesPersonId;
                    dict["SalesPersonName"] = r.SalesPersonId.HasValue && salesPersons.ContainsKey(r.SalesPersonId.Value)
                        ? salesPersons[r.SalesPersonId.Value]
                        : string.Empty;
                    dict["PaymentMethodId"] = r.PaymentMethodId;
                    dict["PaymentMethodName"] = r.PaymentMethodId.HasValue && paymentMethods.ContainsKey(r.PaymentMethodId.Value)
                        ? paymentMethods[r.PaymentMethodId.Value]
                        : string.Empty;
                    dict["StatusText"] = GetStatusText(r.Status);
                    dict["UnredeemedSummary"] = string.Empty;

                    items.Add(exp);
                }

                var receiptIds = receipts.Select(r => r.ReceiptID).ToList();
                if (receiptIds.Any())
                {
                    // ✅ ดึงราคารวมจาก ReceiptItems
                    var itemsPriceByReceipt = await (from ri in context.ReceiptItems
                                                      where receiptIds.Contains(ri.ReceiptId)
                                                      group ri by ri.ReceiptId into g
                                                      select new
                                                      {
                                                          ReceiptId = g.Key,
                                                          TotalPrice = g.Sum(ri => ri.TotalPrice)
                                                      }).ToDictionaryAsync(x => x.ReceiptId, x => x.TotalPrice);

                    // ✅ ดึง items พร้อม SaleEvent info
                    var itemsWithEvents = await (from ri in context.ReceiptItems
                                                 join cd in context.CouponDefinitions on ri.CouponId equals cd.Id
                                                 join se in context.SaleEvents on cd.SaleEventId equals se.Id into seGroup
                                                 from se in seGroup.DefaultIfEmpty()
                                                 where receiptIds.Contains(ri.ReceiptId)
                                                 select new
                                                 {
                                                     ri.ReceiptId,
                                                     ri.ReceiptItemId,
                                                     ri.UnitPrice,
                                                     ri.Quantity,
                                                     ri.IsCOM,
                                                     CouponDefinitionId = cd.Id,
                                                     SaleEventName = se != null ? se.Name : "",
                                                     IsOldEvent = se != null && se.Name.Contains("ไทยเที่ยวไทย") && se.Name.Contains("76")
                                                 }).ToListAsync();

                    // ✅ คำนวณส่วนลดจาก IsCOM (เฉพาะงานที่ไม่ใช่งาน 76)
                    var comDiscountByReceipt = itemsWithEvents
                        .Where(ri => ri.IsCOM && !ri.IsOldEvent)
                        .GroupBy(ri => ri.ReceiptId)
                        .ToDictionary(g => g.Key, g => g.Sum(ri => ri.UnitPrice * ri.Quantity));

                    // ✅ คำนวณส่วนลดจาก IsComplimentary (เฉพาะงาน 76)
                    var oldComplimentaryDiscountByReceipt = await (from gc in context.GeneratedCoupons
                                                                    join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                                                                    join cd in context.CouponDefinitions on gc.CouponDefinitionId equals cd.Id
                                                                    join se in context.SaleEvents on cd.SaleEventId equals se.Id into seGroup
                                                                    from se in seGroup.DefaultIfEmpty()
                                                                    where receiptIds.Contains(ri.ReceiptId)
                                                                          && gc.IsComplimentary
                                                                          && se != null
                                                                          && se.Name.Contains("ไทยเที่ยวไทย")
                                                                          && se.Name.Contains("76")
                                                                    select new
                                                                    {
                                                                        ReceiptId = ri.ReceiptId,
                                                                        gc.CouponDefinition!.Price
                                                                    }).GroupBy(x => x.ReceiptId)
                                                                      .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.Price));

                    // ✅ ดึงข้อมูลสำหรับ UnredeemedSummary
                    var gcStats = await (from gc in context.GeneratedCoupons
                                          join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                                          join cd in context.CouponDefinitions on gc.CouponDefinitionId equals cd.Id
                                          where receiptIds.Contains(ri.ReceiptId)
                                          select new
                                          {
                                              ReceiptId = ri.ReceiptId,
                                              gc.IsUsed,
                                              gc.IsComplimentary,
                                              ri.IsCOM,
                                              CouponDefinitionId = gc.CouponDefinitionId
                                          }).ToListAsync();

                    var defIds = gcStats.Select(x => x.CouponDefinitionId).Where(id => id != 0).Distinct().ToList();
                    var definitions = await context.CouponDefinitions
                        .Where(d => defIds.Contains(d.Id))
                        .ToDictionaryAsync(d => d.Id, d => d.IsLimited);

                    var enriched = gcStats.Select(x => new
                    {
                        x.ReceiptId,
                        x.IsUsed,
                        x.IsComplimentary,
                        x.IsCOM,
                        IsLimited = definitions.ContainsKey(x.CouponDefinitionId) ? definitions[x.CouponDefinitionId] : false
                    }).ToList();

                    var statsByReceipt = enriched.GroupBy(x => x.ReceiptId)
                        .ToDictionary(g => g.Key, g => new
                        {
                            LimitedUnredeemed = g.Count(x => x.IsLimited && !x.IsUsed),
                            LimitedComplimentary = g.Count(x => x.IsLimited && !x.IsUsed && (x.IsComplimentary || x.IsCOM)),
                            HasLimited = g.Any(x => x.IsLimited)
                        });

                    // ✅ อัพเดทส่วนลดและยอดสุทธิ (คำนวณใหม่ทั้งหมด)
                    for (int i = 0; i < items.Count; i++)
                    {
                        var dict = (IDictionary<string, object?>)items[i];
                        var rid = (int)dict["ReceiptID"]!;
                        string summary = string.Empty;

                        // ✅ ดึงราคารวมจาก Items
                        decimal totalPrice = itemsPriceByReceipt.ContainsKey(rid) ? itemsPriceByReceipt[rid] : 0m;

                        // ✅ ส่วนลดที่ผู้ใช้กรอก
                        decimal userDiscount = (decimal)(dict["Discount"] ?? 0m);

                        // ✅ ส่วนลดฟรี (COM หรือ IsComplimentary - เลือกอันเดียว)
                        decimal comDiscount = comDiscountByReceipt.ContainsKey(rid) ? comDiscountByReceipt[rid] : 0m;
                        decimal oldComplimentaryDiscount = oldComplimentaryDiscountByReceipt.ContainsKey(rid) ? oldComplimentaryDiscountByReceipt[rid] : 0m;
                        
                        // ✅ รวมส่วนลดทั้งหมด
                        decimal totalDiscount = userDiscount + comDiscount + oldComplimentaryDiscount;
                        
                        // ✅ คำนวณยอดสุทธิ = ราคารวม - ส่วนลดทั้งหมด
                        decimal netAmount = totalPrice - totalDiscount;

                        dict["Discount"] = totalDiscount;
                        dict["TotalAmount"] = netAmount;

                        if (statsByReceipt.TryGetValue(rid, out var s))
                        {
                            if (s.HasLimited)
                            {
                                summary = $"ยังไม่ได้ตัดอีก {s.LimitedUnredeemed} ใบ";
                                if (s.LimitedComplimentary > 0)
                                {
                                    summary += $" | COM (ฟรี) {s.LimitedComplimentary} ใบ";
                                }
                            }
                        }

                        dict["UnredeemedSummary"] = summary;
                    }
                }

                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                return Ok(new
                {
                    items,
                    page,
                    pageSize,
                    totalCount,
                    totalPages
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving receipts", error = ex.Message });
            }
        }

        /// <summary>
        /// Get receipt details by ID including items.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReceiptById(int id)
        {
            try
            {
                using var context = new CouponContext();

                var receipt = await context.Receipts
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.ReceiptID == id);

                if (receipt == null)
                    return NotFound(new { message = "Receipt not found" });

                var salesPerson = receipt.SalesPersonId.HasValue
                    ? await context.SalesPerson.FindAsync(receipt.SalesPersonId.Value)
                    : null;

                var paymentMethod = receipt.PaymentMethodId.HasValue
                    ? await context.PaymentMethods.FindAsync(receipt.PaymentMethodId.Value)
                    : null;

                // ✅ คำนวณราคารวมจาก Items
                decimal totalPrice = 0m;
                if (receipt.Items != null && receipt.Items.Any())
                {
                    totalPrice = receipt.Items.Sum(item => item.TotalPrice);
                }

                // ✅ คำนวณส่วนลดจาก IsCOM (เฉพาะงานที่ไม่ใช่งาน 76)
                decimal comDiscount = 0m;
                if (receipt.Items != null && receipt.Items.Any())
                {
                    // ✅ ดึง receipt item IDs ก่อน
                    var receiptItemIds = receipt.Items
                        .Where(ri => ri.IsCOM)
                        .Select(ri => ri.ReceiptItemId)
                        .ToList();

                    if (receiptItemIds.Any())
                    {
                        // ✅ Query database ด้วย IDs
                        var itemsWithEvents = await (from ri in context.ReceiptItems
                                                     join cd in context.CouponDefinitions on ri.CouponId equals cd.Id
                                                     join se in context.SaleEvents on cd.SaleEventId equals se.Id into seGroup
                                                     from se in seGroup.DefaultIfEmpty()
                                                     where receiptItemIds.Contains(ri.ReceiptItemId)
                                                     select new
                                                     {
                                                         ri.UnitPrice,
                                                         ri.Quantity,
                                                         SaleEventName = se != null ? se.Name : ""
                                                     }).ToListAsync();

                        comDiscount = itemsWithEvents
                            .Where(x => !(x.SaleEventName.Contains("ไทยเที่ยวไทย") && x.SaleEventName.Contains("76")))
                            .Sum(x => x.UnitPrice * x.Quantity);
                    }
                }

                // ✅ คำนวณส่วนลดจาก IsComplimentary (เฉพาะงาน 76)
                decimal oldComplimentaryDiscount = 0m;
                
                if (receipt.Items != null && receipt.Items.Any())
                {
                    var receiptItemIds = receipt.Items.Select(ri => ri.ReceiptItemId).ToList();
                    
                    oldComplimentaryDiscount = await (from gc in context.GeneratedCoupons
                                                       join cd in context.CouponDefinitions on gc.CouponDefinitionId equals cd.Id
                                                       join se in context.SaleEvents on cd.SaleEventId equals se.Id into seGroup
                                                       from se in seGroup.DefaultIfEmpty()
                                                       where receiptItemIds.Contains(gc.ReceiptItemId ?? 0)
                                                             && gc.IsComplimentary
                                                             && se != null
                                                             && se.Name.Contains("ไทยเที่ยวไทย") 
                                                             && se.Name.Contains("76")
                                                       select cd.Price).SumAsync();
                }

                // ✅ รวมส่วนลดทั้งหมด: Discount (ที่กรอก) + ส่วนลดฟรี (COM หรือ Complimentary)
                decimal totalDiscount = receipt.Discount + comDiscount + oldComplimentaryDiscount;

                // ✅ คำนวณยอดสุทธิที่ถูกต้อง
                decimal netAmount = totalPrice - totalDiscount;

                var items = new System.Collections.Generic.List<object>();
                if (receipt.Items != null && receipt.Items.Any())
                {
                    foreach (var item in receipt.Items)
                    {
                        var couponDef = await context.CouponDefinitions.FindAsync(item.CouponId);

                        var generatedCouponsData = await context.GeneratedCoupons
                            .Where(gc => gc.ReceiptItemId == item.ReceiptItemId)
                            .Select(gc => new
                            {
                                gc.GeneratedCode,
                                gc.IsComplimentary
                            })
                            .ToListAsync();

                        var generatedCodes = generatedCouponsData.Select(gc => gc.GeneratedCode).ToList();

                        var hasComplimentary = generatedCouponsData.Any(gc => gc.IsComplimentary);
                        var isCOM = item.IsCOM;

                        items.Add(new
                        {
                            item.ReceiptItemId,
                            item.CouponId,
                            CouponCode = couponDef?.Code ?? "",
                            CouponName = couponDef?.Name ?? "",
                            item.Quantity,
                            item.UnitPrice,
                            item.TotalPrice,
                            GeneratedCodes = generatedCodes,
                            IsComplimentary = hasComplimentary,
                            IsCOM = isCOM,
                            IsFreeCoupon = hasComplimentary || isCOM
                        });
                    }
                }

                return Ok(new
                {
                    receipt.ReceiptID,
                    receipt.ReceiptCode,
                    receipt.ReceiptDate,
                    receipt.CustomerName,
                    receipt.CustomerPhoneNumber,
                    TotalPrice = totalPrice,        // ✅ ราคารวมจาก Items
                    Discount = totalDiscount,       // ✅ ส่วนลดจาก COM + Complimentary
                    TotalAmount = netAmount,        // ✅ ยอดสุทธิที่ถูกต้อง
                    receipt.Status,
                    receipt.SalesPersonId,
                    SalesPersonName = salesPerson?.Name ?? "",
                    SalesPersonPhone = salesPerson?.Telephone ?? "",
                    receipt.PaymentMethodId,
                    PaymentMethodName = paymentMethod?.Name ?? "",
                    StatusText = GetStatusText(receipt.Status),
                    Items = items
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving receipt details", error = ex.Message });
            }
        }

        /// <summary>
        /// Request model for updating COM/Complimentary flag
        /// </summary>
        public class UpdateComRequest
        {
            /// <summary>
            /// Receipt Item ID
            /// </summary>
            public int ReceiptItemId { get; set; }

            /// <summary>
            /// Whether to mark as COM (free)
            /// </summary>
            public bool IsCOM { get; set; }
        }

        /// <summary>
        /// Update COM/Complimentary flag for a receipt item.
        /// Automatically determines whether to update ReceiptItems.IsCOM (new system) 
        /// or GeneratedCoupons.IsComplimentary (old system) based on SaleEvent.
        /// </summary>
        [HttpPost("update-com")]
        public async Task<IActionResult> UpdateCOM([FromBody] UpdateComRequest req)
        {
            try
            {
                using var context = new CouponContext();

                var receiptItem = await context.ReceiptItems
                    .FirstOrDefaultAsync(ri => ri.ReceiptItemId == req.ReceiptItemId);

                if (receiptItem == null)
                    return NotFound(new { message = "Receipt item not found" });

                var couponDef = await context.CouponDefinitions
                    .Include(cd => cd.SaleEvent)
                    .FirstOrDefaultAsync(cd => cd.Id == receiptItem.CouponId);

                if (couponDef == null)
                    return NotFound(new { message = "Coupon definition not found" });

                // ✅ ตรวจสอบว่าเป็นงาน "ไทยเที่ยวไทยครั้งที่ 76" หรือไม่
                bool isOldSystem = false;
                if (couponDef.SaleEvent != null)
                {
                    string eventName = couponDef.SaleEvent.Name.Trim();
                    isOldSystem = eventName.Contains("ไทยเที่ยวไทย") && eventName.Contains("76");
                }

                if (isOldSystem)
                {
                    // ✅ ระบบเก่า: บันทึกลง GeneratedCoupons.IsComplimentary
                    var generatedCoupons = await context.GeneratedCoupons
                        .Where(gc => gc.ReceiptItemId == req.ReceiptItemId)
                        .ToListAsync();

                    if (!generatedCoupons.Any())
                        return NotFound(new { message = "No generated coupons found for this receipt item" });

                    foreach (var gc in generatedCoupons)
                    {
                        gc.IsComplimentary = req.IsCOM;
                    }

                    context.GeneratedCoupons.UpdateRange(generatedCoupons);
                    await context.SaveChangesAsync();

                    return Ok(new
                    {
                        result = "Success",
                        message = $"Updated IsComplimentary for {generatedCoupons.Count} coupons (Old System: {couponDef.SaleEvent?.Name})",
                        system = "Old (GeneratedCoupons.IsComplimentary)",
                        saleEventName = couponDef.SaleEvent?.Name,
                        updatedCouponsCount = generatedCoupons.Count
                    });
                }
                else
                {
                    // ✅ ระบบใหม่: บันทึกลง ReceiptItems.IsCOM
                    receiptItem.IsCOM = req.IsCOM;
                    context.ReceiptItems.Update(receiptItem);
                    await context.SaveChangesAsync();

                    return Ok(new
                    {
                        result = "Success",
                        message = "Updated IsCOM for receipt item (New System)",
                        system = "New (ReceiptItems.IsCOM)",
                        saleEventName = couponDef.SaleEvent?.Name ?? "No Sale Event",
                        receiptItemId = receiptItem.ReceiptItemId
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating COM flag", error = ex.Message });
            }
        }
    }
}
