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
            [FromQuery] string? couponCode = null)
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

                // Handle date range robustly: dateFrom/dateTo are DateTimeOffset in UTC or with offset.
                // We'll convert to UTC and use inclusive start and exclusive end (next day) comparison to avoid timezone issues.
                if (dateFrom.HasValue)
                {
                    var startUtc = dateFrom.Value.UtcDateTime;
                    query = query.Where(r => r.ReceiptDate >= startUtc);
                }

                if (dateTo.HasValue)
                {
                    // Use end-exclusive by adding1 millisecond (or better: add1 tick) is risky; instead interpret provided dateTo as exact instant
                    // If client sends end of day in local timezone (23:59:59.999) converted to UTC, it's safe to use <= comparison.
                    var endUtc = dateTo.Value.UtcDateTime;
                    query = query.Where(r => r.ReceiptDate <= endUtc);
                }

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(r => r.Status == status);

                if (salesPersonId.HasValue)
                    query = query.Where(r => r.SalesPersonId == salesPersonId.Value);

                if (paymentMethodId.HasValue)
                    query = query.Where(r => r.PaymentMethodId == paymentMethodId.Value);

                // NEW: if couponCode provided, narrow receipts to those that have at least one ReceiptItem
                // where the CouponDefinition.Code contains the couponCode OR any GeneratedCoupon.GeneratedCode contains couponCode
                if (!string.IsNullOrWhiteSpace(couponCode))
                {
                    string search = couponCode.Trim();

                    // receiptIds from ReceiptItems -> CouponDefinitions.Code
                    var idsFromDefs = await (from ri in context.ReceiptItems
                                             join cd in context.CouponDefinitions on ri.CouponId equals cd.Id
                                             where cd.Code.Contains(search)
                                             select ri.ReceiptId).Distinct().ToListAsync();

                    // receiptIds from GeneratedCoupons -> ReceiptItems -> ReceiptId
                    var idsFromGenerated = await (from gc in context.GeneratedCoupons
                                                   join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                                                   where gc.GeneratedCode.Contains(search)
                                                   select ri.ReceiptId).Distinct().ToListAsync();

                    var matchingReceiptIds = idsFromDefs.Union(idsFromGenerated).Distinct().ToList();

                    if (!matchingReceiptIds.Any())
                    {
                        // no matches -> return empty result
                        return Ok(new
                        {
                            items = new List<object>(),
                            page,
                            pageSize,
                            totalCount =0,
                            totalPages =0
                        });
                    }

                    query = query.Where(r => matchingReceiptIds.Contains(r.ReceiptID));
                }

                // NEW (existing behavior): only include receipts that have at least one ReceiptItem whose CouponDefinition's CouponType
                // is either "ASIA BANGKOK" or "ONLINE".
                var wantedTypes = new[] { "ASIA BANGKOK", "ONLINE" };
                var wantedUpper = wantedTypes.Select(w => w.ToUpper()).ToList();

                var matchingDefIds = await context.CouponDefinitions
                    .Include(cd => cd.CouponType)
                    .Where(cd => cd.CouponType != null && wantedUpper.Contains(cd.CouponType.Name.ToUpper()))
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
                        totalCount =0,
                        totalPages =0
                    });
                }

                var totalCount = await query.CountAsync();

                var receipts = await query
                    .OrderByDescending(r => r.ReceiptDate)
                    .Skip((page -1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var paymentMethods = await context.PaymentMethods.ToDictionaryAsync(pm => pm.Id, pm => pm.Name);
                var salesPersons = await context.SalesPerson.ToDictionaryAsync(sp => sp.ID, sp => sp.Name);

                // Build items as dynamic ExpandoObjects so we can add UnredeemedSummary later
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
                    // add placeholder for UnredeemedSummary
                    dict["UnredeemedSummary"] = string.Empty;

                    items.Add(exp);
                }

                // Compute unredeemed coupon summary per receipt for limited coupons only
                var receiptIds = receipts.Select(r => r.ReceiptID).ToList();
                if (receiptIds.Any())
                {
                    var gcStats = await (from gc in context.GeneratedCoupons
                                          join ri in context.ReceiptItems on gc.ReceiptItemId equals ri.ReceiptItemId
                                          where receiptIds.Contains(ri.ReceiptId)
                                          select new
                                          {
                                              ReceiptId = ri.ReceiptId,
                                              gc.IsUsed,
                                              gc.IsComplimentary,
                                              // fetch IsLimited via navigation may be null; join to CouponDefinitions safer
                                              CouponDefinitionId = gc.CouponDefinitionId
                                          }).ToListAsync();

                    // load definitions for those ids
                    var defIds = gcStats.Select(x => x.CouponDefinitionId).Where(id => id !=0).Distinct().ToList();
                    var definitions = await context.CouponDefinitions
                        .Where(d => defIds.Contains(d.Id))
                        .ToDictionaryAsync(d => d.Id, d => d.IsLimited);

                    var enriched = gcStats.Select(x => new
                    {
                        x.ReceiptId,
                        x.IsUsed,
                        x.IsComplimentary,
                        IsLimited = definitions.ContainsKey(x.CouponDefinitionId) ? definitions[x.CouponDefinitionId] : false
                    }).ToList();

                    var statsByReceipt = enriched.GroupBy(x => x.ReceiptId)
                        .ToDictionary(g => g.Key, g => new
                        {
                            LimitedUnredeemed = g.Count(x => x.IsLimited && !x.IsUsed),
                            LimitedComplimentary = g.Count(x => x.IsLimited && !x.IsUsed && x.IsComplimentary),
                            HasLimited = g.Any(x => x.IsLimited)
                        });

                    // Attach UnredeemedSummary to items
                    for (int i =0; i < items.Count; i++)
                    {
                        var dict = (IDictionary<string, object?>)items[i];
                        var rid = (int)dict["ReceiptID"]!;
                        string summary = string.Empty;

                        if (statsByReceipt.TryGetValue(rid, out var s))
                        {
                            if (s.HasLimited)
                            {
                                summary = $"ยังไม่ได้ตัดอีก {s.LimitedUnredeemed} ใบ";
                                if (s.LimitedComplimentary >0)
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
        /// <param name="id">Receipt ID.</param>
        /// <returns>Receipt with items details.</returns>
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

                var items = new System.Collections.Generic.List<object>();
                if (receipt.Items != null && receipt.Items.Any())
                {
                    foreach (var item in receipt.Items)
                    {
                        var couponDef = await context.CouponDefinitions.FindAsync(item.CouponId);

                        var generatedCodes = await context.GeneratedCoupons
                            .Where(gc => gc.ReceiptItemId == item.ReceiptItemId)
                            .Select(gc => gc.GeneratedCode)
                            .ToListAsync();

                        items.Add(new
                        {
                            item.ReceiptItemId,
                            item.CouponId,
                            CouponCode = couponDef?.Code ?? "",
                            CouponName = couponDef?.Name ?? "",
                            item.Quantity,
                            item.UnitPrice,
                            item.TotalPrice,
                            GeneratedCodes = generatedCodes
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
                    receipt.Discount,
                    receipt.TotalAmount,
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
    }
}
