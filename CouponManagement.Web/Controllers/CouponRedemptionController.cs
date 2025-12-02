using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CouponManagement.Shared.Services;
using System;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.IO.Font;
using iText.IO.Font.Constants;
using System.Globalization;
using CouponManagement.Shared;
using Microsoft.EntityFrameworkCore;

namespace CouponManagement.Web.Controllers
{
    /// <summary>
    /// Controller for coupon redemption and reporting operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CouponRedemptionController : ControllerBase
    {
        private readonly GeneratedCouponService _couponService;
        private readonly ILogger<CouponRedemptionController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="CouponRedemptionController"/>.
        /// </summary>
        /// <param name="couponService">Service for generated coupon operations.</param>
        /// <param name="logger">Logger instance.</param>
        public CouponRedemptionController(GeneratedCouponService couponService, ILogger<CouponRedemptionController> logger)
        {
            _couponService = couponService;
            _logger = logger;
        }

        /// <summary>
        /// Request model for redeeming a coupon.
        /// </summary>
        public class RedeemRequest
        {
            /// <summary>Coupon code to redeem.</summary>
            public string Code { get; set; } = string.Empty;
            /// <summary>User or terminal performing the redemption.</summary>
            public string RedeemedBy { get; set; } = string.Empty;
            /// <summary>Optional receipt item id to associate with the redemption.</summary>
            public int? ReceiptItemId { get; set; }
            /// <summary>Optional redeemed timestamp supplied by client as naive local string (e.g. yyyy-MM-ddTHH:mm:ss).</summary>
            public string? RedeemedAt { get; set; }
        }

        /// <summary>
        /// Redeem a coupon by code.
        /// </summary>
        /// <param name="req">Redeem request containing code and optional metadata.</param>
        /// <returns>ActionResult describing outcome of redeem operation.</returns>
        [HttpPost("redeem")]
        public async Task<IActionResult> Redeem([FromBody] RedeemRequest req)
        {
            _logger.LogInformation("Redeem request received: Code={Code}, RedeemedBy={By}, ReceiptItemId={ReceiptItemId}", req.Code, req.RedeemedBy, req.ReceiptItemId);

            if (string.IsNullOrWhiteSpace(req.Code))
            {
                _logger.LogWarning("Redeem failed: empty code");
                return BadRequest(new { result = "InvalidRequest", message = "code is required" });
            }

            // Parse client-sent redeemedAt which is expected as naive local string like "yyyy-MM-ddTHH:mm:ss"
            DateTime? redeemedAtLocal = null;
            if (!string.IsNullOrWhiteSpace(req.RedeemedAt))
            {
                // try exact parse first
                if (DateTime.TryParseExact(req.RedeemedAt, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    // treat as local/unspecified time (store as-is)
                    redeemedAtLocal = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
                }
                else
                {
                    // fallback to general parse (will interpret based on system settings)
                    if (DateTime.TryParse(req.RedeemedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed2))
                    {
                        redeemedAtLocal = DateTime.SpecifyKind(parsed2, DateTimeKind.Unspecified);
                    }
                }
            }

            var result = await _couponService.RedeemCouponAsync(req.Code.Trim(), req.RedeemedBy ?? "system", req.ReceiptItemId, redeemedAtLocal);

            _logger.LogInformation("Redeem outcome for {Code}: {Outcome} - {Message}", req.Code, result.Result, result.Message);

            var response = new
            {
                result = result.Result.ToString(),
                message = result.Message,
                couponId = result.CouponId,
                usedBy = result.UsedBy,
                // return the stored datetime as naive string and a pre-formatted local display string
                usedDateRaw = result.UsedDate.HasValue ? result.UsedDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                usedDateLocal = result.UsedDate.HasValue ? result.UsedDate.Value.ToString("dd/MM/yyyy HH:mm") : null
            };

            return result.Result switch
            {
                CouponManagement.Shared.Services.RedeemResultType.Success => Ok(response),
                CouponManagement.Shared.Services.RedeemResultType.NotFound => NotFound(response),
                CouponManagement.Shared.Services.RedeemResultType.Expired => BadRequest(response),
                CouponManagement.Shared.Services.RedeemResultType.AlreadyUsed => Conflict(response),
                CouponManagement.Shared.Services.RedeemResultType.ConcurrentUpdate => Conflict(response),
                _ => StatusCode(500, response)
            };
        }

        /// <summary>
        /// Get sold coupons (linked to a receipt item) with optional filters and pagination.
        /// </summary>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Items per page.</param>
        /// <param name="couponDefinitionId">Optional coupon definition id to filter.</param>
        /// <param name="searchCode">Optional generated code search text.</param>
        /// <param name="receiptItemId">Optional receipt item id to filter.</param>
        /// <param name="receiptId">Optional receipt id to filter.</param>
        /// <param name="soldBy">Optional soldBy/UsedBy filter.</param>
        /// <param name="couponDefinitionCode">Optional coupon definition code filter.</param>
        /// <param name="couponDefinitionName">Optional coupon definition name filter.</param>
        /// <param name="createdFrom">Optional created from datetime filter.</param>
        /// <param name="createdTo">Optional created to datetime filter.</param>
        /// <returns>Paged result of sold coupons.</returns>
        [HttpGet("sold")]
        public async Task<IActionResult> GetSold(
                [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
          [FromQuery] int? couponDefinitionId = null,
                [FromQuery] string? searchCode = null,
       [FromQuery] int? receiptItemId = null,
                [FromQuery] int? receiptId = null,
             [FromQuery] string? soldBy = null,
         [FromQuery] string? couponDefinitionCode = null,
        [FromQuery] string? couponDefinitionName = null,
       [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null)
        {
           var result = await _couponService.GetSoldCouponsAsync(page, pageSize, couponDefinitionId, searchCode, receiptItemId, receiptId, soldBy, couponDefinitionCode, couponDefinitionName, createdFrom, createdTo);
           return Ok(result);
}

        /// <summary>
     /// Export sold coupons to Excel format.
        /// </summary>
        /// <param name="couponDefinitionId">Optional coupon definition id to filter.</param>
     /// <param name="searchCode">Optional generated code search text.</param>
        /// <param name="receiptItemId">Optional receipt item id to filter.</param>
        /// <param name="receiptId">Optional receipt id to filter.</param>
        /// <param name="soldBy">Optional soldBy/UsedBy filter.</param>
      /// <param name="couponDefinitionCode">Optional coupon definition code filter.</param>
        /// <param name="couponDefinitionName">Optional coupon definition name filter.</param>
        /// <param name="createdFrom">Optional created from datetime filter.</param>
        /// <param name="createdTo">Optional created to datetime filter.</param>
        /// <param name="reportMode">"detailed" | "summary" | "both"</param>
        /// <param name="includeRetrievedDate">When true, include the retrieved/used date column label as 'วันที่ใช้/ขาย' in outputs.</param>
        /// <returns>Excel file containing sold coupons report or summary.</returns>
 [HttpGet("export/excel")]
        public async Task<IActionResult> ExportToExcel(
[FromQuery] int? couponDefinitionId = null,
        [FromQuery] string? searchCode = null,
            [FromQuery] int? receiptItemId = null,
  [FromQuery] int? receiptId = null,
     [FromQuery] string? soldBy = null,
        [FromQuery] string? couponDefinitionCode = null,
     [FromQuery] string? couponDefinitionName = null,
       [FromQuery] string? createdFrom = null,
       [FromQuery] string? createdTo = null,
       [FromQuery] string? reportMode = "detailed",
       [FromQuery] bool includeRetrievedDate = false)
        {
            try
          {
 // Parse client-supplied createdFrom/createdTo which are expected as naive local strings (yyyy-MM-ddTHH:mm:ss) or ISO with offset
 DateTime? usedFrom = null;
 DateTime? usedTo = null;
 if(!string.IsNullOrWhiteSpace(createdFrom))
 {
 if(DateTime.TryParseExact(createdFrom, "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedFrom))
 {
 usedFrom = DateTime.SpecifyKind(parsedFrom, DateTimeKind.Unspecified);
 }
 else if(DateTime.TryParse(createdFrom, out var parsedGenericFrom))
 {
 // if parsed as UTC, convert to local naive; otherwise treat as unspecified/local
 if(parsedGenericFrom.Kind == DateTimeKind.Utc)
 usedFrom = DateTime.SpecifyKind(parsedGenericFrom.ToLocalTime(), DateTimeKind.Unspecified);
 else
 usedFrom = DateTime.SpecifyKind(parsedGenericFrom, DateTimeKind.Unspecified);
 }
 }
 if(!string.IsNullOrWhiteSpace(createdTo))
 {
 if(DateTime.TryParseExact(createdTo, "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedTo))
 {
 usedTo = DateTime.SpecifyKind(parsedTo, DateTimeKind.Unspecified);
 }
 else if(DateTime.TryParse(createdTo, out var parsedGenericTo))
 {
 if(parsedGenericTo.Kind == DateTimeKind.Utc)
 usedTo = DateTime.SpecifyKind(parsedGenericTo.ToLocalTime(), DateTimeKind.Unspecified);
 else
 usedTo = DateTime.SpecifyKind(parsedGenericTo, DateTimeKind.Unspecified);
 }
 }

 // Use redeemed coupons (IsUsed == true) filtered by UsedDate (createdFrom/createdTo represent UsedDate range from client)
 var result = await _couponService.GetRedeemedCouponsAsync(
1, int.MaxValue, usedFrom, usedTo, soldBy, couponDefinitionCode, couponDefinitionName);

 using var workbook = new XLWorkbook();
 var worksheet = workbook.Worksheets.Add("รายละเอียดคูปอง");

 worksheet.Cell(1,1).Value = "รหัสคูปอง";
 worksheet.Cell(1,2).Value = "รหัสคำนิยาม";
 worksheet.Cell(1,3).Value = "ชื่อคำนิยาม";
 worksheet.Cell(1,4).Value = "ราคา";
 worksheet.Cell(1,5).Value = "สถานะ";
 worksheet.Cell(1,6).Value = "การใช้งาน";
 worksheet.Cell(1,7).Value = "ผู้ใช้";
 worksheet.Cell(1,8).Value = includeRetrievedDate ? "วันที่ใช้/ขาย" : "วันที่ใช้";
 worksheet.Cell(1,9).Value = "สร้างเมื่อ";
 worksheet.Cell(1,10).Value = "วันหมดอายุ";

 var headerRange = worksheet.Range(1,1,1,10);
 headerRange.Style.Font.Bold = true;
 headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
 headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

 int row =2;
 // จัดเรียงข้อมูลตามวันที่ใช้จากเก่าไปใหม่
 var sortedItems = result.Items.OrderBy(x => x.UsedDate ?? DateTime.MinValue).ToList();
 
 foreach (var item in sortedItems)
 {
worksheet.Cell(row,1).Value = item.GeneratedCode;
 worksheet.Cell(row,2).Value = item.CouponDefinitionCode;
 worksheet.Cell(row,3).Value = item.CouponDefinitionName;
 worksheet.Cell(row,4).Value = item.CouponDefinitionPrice;
 worksheet.Cell(row,5).Value = item.StatusText;
 worksheet.Cell(row,6).Value = item.UsageText;
 worksheet.Cell(row,7).Value = item.UsedBy ?? "";
 worksheet.Cell(row,8).Value = item.UsedDate?.ToString("dd/MM/yyyy HH:mm") ?? "";
 worksheet.Cell(row,9).Value = item.CreatedAt.ToString("dd/MM/yyyy HH:mm");
 worksheet.Cell(row,10).Value = item.ExpiresAt?.ToString("dd/MM/yyyy") ?? "";
 row++;
 }

 worksheet.Columns().AdjustToContents();

 // สร้างส่วนสรุปตามวันที่และชื่อคำนิยาม
 using var ctx = new CouponContext();
 
 // กรุ๊ปข้อมูลตามวันที่ใช้งานและชื่อคำนิยาม
 var summaryGroups = result.Items
 .Where(x => x.UsedDate.HasValue)
 .GroupBy(x => new { 
 UsedDate = x.UsedDate!.Value.Date, 
 DefinitionId = x.CouponDefinitionId,
 DefinitionCode = x.CouponDefinitionCode,
 DefinitionName = x.CouponDefinitionName 
 })
 .OrderBy(g => g.Key.UsedDate)
 .ThenBy(g => g.Key.DefinitionName)
 .ToList();

 if (summaryGroups.Any())
 {
 row += 2;
 worksheet.Cell(row, 1).Value = "สรุปการใช้คูปองแยกตามวันและชื่อคูปอง";
 worksheet.Cell(row, 1).Style.Font.Bold = true;
 worksheet.Cell(row, 1).Style.Font.FontSize = 12;
 row++;

 // Header สำหรับสรุป
 worksheet.Cell(row, 1).Value = "วันที่ใช้";
 worksheet.Cell(row, 2).Value = "รหัสคำนิยาม";
 worksheet.Cell(row, 3).Value = "ชื่อคำนิยาม";
 worksheet.Cell(row, 4).Value = "จำนวนทั้งหมดที่มี";
 worksheet.Cell(row, 5).Value = "ใช้ไปก่อนหน้า";
 worksheet.Cell(row, 6).Value = "คงเหลือก่อนวันนี้";
 worksheet.Cell(row, 7).Value = "ใช้ในวันนี้";
 worksheet.Cell(row, 8).Value = "คงเหลือหลังวันนี้";
 
 var summaryHeaderRange = worksheet.Range(row, 1, row, 8);
 summaryHeaderRange.Style.Font.Bold = true;
 summaryHeaderRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
 summaryHeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
 row++;

 // คำนวณข้อมูลสรุปสำหรับแต่ละกรุ๊ป
 foreach (var group in summaryGroups)
 {
 var usedDate = group.Key.UsedDate;
 var definitionId = group.Key.DefinitionId;
 var definitionCode = group.Key.DefinitionCode;
 var definitionName = group.Key.DefinitionName;

 // จำนวนทั้งหมดที่สร้างของคำนิยามนี้
 var totalGenerated = await ctx.GeneratedCoupons
 .CountAsync(gc => gc.CouponDefinitionId == definitionId);

 // จำนวนที่ใช้ไปก่อนวันนี้
 var usedBeforeToday = await ctx.GeneratedCoupons
 .CountAsync(gc => gc.CouponDefinitionId == definitionId 
 && gc.IsUsed 
 && gc.UsedDate.HasValue
 && gc.UsedDate.Value.Date < usedDate);

 // คงเหลือก่อนวันนี้
 var remainingBeforeToday = totalGenerated - usedBeforeToday;

 // จำนวนที่ใช้ในวันนี้
 var usedToday = group.Count();

 // คงเหลือหลังวันนี้
 var remainingAfterToday = remainingBeforeToday - usedToday;

 worksheet.Cell(row, 1).Value = usedDate.ToString("dd/MM/yyyy");
 worksheet.Cell(row, 2).Value = definitionCode;
 worksheet.Cell(row, 3).Value = definitionName;
 worksheet.Cell(row, 4).Value = totalGenerated;
 worksheet.Cell(row, 5).Value = usedBeforeToday;
 worksheet.Cell(row, 6).Value = remainingBeforeToday;
 worksheet.Cell(row, 7).Value = usedToday;
 worksheet.Cell(row, 8).Value = remainingAfterToday;
 
 row++;
 }

 worksheet.Columns().AdjustToContents();
 }

 // เพิ่มส่วนสรุปรวมตามคำนิยาม (รวมทุกวันในช่วงที่เลือก)
 var definitionSummary = result.Items
 .Where(x => x.UsedDate.HasValue)
 .GroupBy(x => new { 
 DefinitionId = x.CouponDefinitionId,
 DefinitionCode = x.CouponDefinitionCode,
 DefinitionName = x.CouponDefinitionName 
 })
 .OrderBy(g => g.Key.DefinitionName)
 .ToList();

 if (definitionSummary.Any())
 {
 row += 2;
 worksheet.Cell(row, 1).Value = "สรุปรวมตามชื่อคูปอง (ทุกวันในช่วงที่เลือก)";
 worksheet.Cell(row, 1).Style.Font.Bold = true;
 worksheet.Cell(row, 1).Style.Font.FontSize = 12;
 row++;

 // Header สำหรับสรุปรวม
 worksheet.Cell(row, 1).Value = "รหัสคำนิยาม";
 worksheet.Cell(row, 2).Value = "ชื่อคำนิยาม";
 worksheet.Cell(row, 3).Value = "จำนวนทั้งหมดที่มี";
 worksheet.Cell(row, 4).Value = "ใช้ไปก่อนช่วงวันที่";
 worksheet.Cell(row, 5).Value = "คงเหลือก่อนช่วงวันที่";
 worksheet.Cell(row, 6).Value = "ใช้ในช่วงวันที่";
 worksheet.Cell(row, 7).Value = "คงเหลือหลังช่วงวันที่";
 
 var definitionHeaderRange = worksheet.Range(row, 1, row, 7);
 definitionHeaderRange.Style.Font.Bold = true;
 definitionHeaderRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
 definitionHeaderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
 row++;

 // คำนวณข้อมูลสรุปสำหรับแต่ละกรุ๊ป
 foreach (var group in definitionSummary)
 {
 var definitionId = group.Key.DefinitionId;
 var definitionCode = group.Key.DefinitionCode;
 var definitionName = group.Key.DefinitionName;

 // จำนวนทั้งหมดที่สร้างของคำนิยามนี้
 var totalGenerated = await ctx.GeneratedCoupons
 .CountAsync(gc => gc.CouponDefinitionId == definitionId);

 // หาวันแรกที่มีการใช้ในกรุปนี้
 var firstUsedDate = group.Min(x => x.UsedDate!.Value.Date);

 // จำนวนที่ใช้ไปก่อนวันแรกของการใช้
 var usedBeforePeriod = await ctx.GeneratedCoupons
 .CountAsync(gc => gc.CouponDefinitionId == definitionId 
 && gc.IsUsed 
 && gc.UsedDate.HasValue
 && gc.UsedDate.Value.Date < firstUsedDate);

 // คงเหลือก่อนวันแรกของการใช้
 var remainingBeforePeriod = totalGenerated - usedBeforePeriod;

 // จำนวนที่ใช้ในช่วงวันที่มีการเลือก
 var usedInPeriod = group.Count();

 // คงเหลือหลังจากการใช้ในช่วงวันที่เลือก
 var remainingAfterPeriod = remainingBeforePeriod - usedInPeriod;

 worksheet.Cell(row, 1).Value = definitionCode;
 worksheet.Cell(row, 2).Value = definitionName;
 worksheet.Cell(row, 3).Value = totalGenerated;
 worksheet.Cell(row, 4).Value = usedBeforePeriod;
 worksheet.Cell(row, 5).Value = remainingBeforePeriod;
 worksheet.Cell(row, 6).Value = usedInPeriod;
 worksheet.Cell(row, 7).Value = remainingAfterPeriod;
 
 row++;
 }

 worksheet.Columns().AdjustToContents();
 }

 using var stream = new MemoryStream();
 workbook.SaveAs(stream);
 stream.Position = 0;

 var fileName = $"รายงานคูปอง_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.xlsx";
 return File(stream.ToArray(),
 "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
 fileName);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error exporting to Excel");
 return StatusCode(500, new { message = "Error exporting to Excel", error = ex.Message });
 }
 }

        /// <summary>
     /// Export sold coupons to PDF format with Thai font support.
        /// </summary>
        /// <param name="couponDefinitionId">Optional coupon definition id to filter.</param>
     /// <param name="searchCode">Optional generated code search text.</param>
        /// <param name="receiptItemId">Optional receipt item id to filter.</param>
        /// <param name="receiptId">Optional receipt id to filter.</param>
        /// <param name="soldBy">Optional soldBy/UsedBy filter.</param>
      /// <param name="couponDefinitionCode">Optional coupon definition code filter.</param>
        /// <param name="couponDefinitionName">Optional coupon definition name filter.</param>
        /// <param name="createdFrom">Optional created from datetime filter.</param>
        /// <param name="createdTo">Optional created to datetime filter.</param>
        /// <param name="reportMode">"detailed" | "summary" | "both"</param>
        /// <param name="includeRetrievedDate">When true, include the retrieved/used date column label as 'วันที่ใช้/ขาย' in outputs.</param>
        /// <returns>PDF file containing daily coupon report with Thai language support.</returns>
 [HttpGet("export/pdf")]
 public async Task<IActionResult> ExportToPdf(
 [FromQuery] int? couponDefinitionId = null,
 [FromQuery] string? searchCode = null,
   [FromQuery] int? receiptItemId = null,
            [FromQuery] int? receiptId = null,
 [FromQuery] string? soldBy = null,
        [FromQuery] string? couponDefinitionCode = null,
     [FromQuery] string? couponDefinitionName = null,
            [FromQuery] string? createdFrom = null,
            [FromQuery] string? createdTo = null,
 [FromQuery] string? reportMode = "detailed",
 [FromQuery] bool includeRetrievedDate = false)
 {
 try
 {
 // Parse client-supplied createdFrom/createdTo which may be ISO UTC or naive local, convert UTC -> local unspecified
 DateTime? usedFrom = null;
 DateTime? usedTo = null;
 if (!string.IsNullOrWhiteSpace(createdFrom))
 {
 if (DateTime.TryParseExact(createdFrom, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom))
 {
 usedFrom = DateTime.SpecifyKind(parsedFrom, DateTimeKind.Unspecified);
 }
 else if (DateTime.TryParse(createdFrom, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedGenericFrom))
 {
 var local = parsedGenericFrom.ToLocalTime();
 usedFrom = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
 }
 }

 if (!string.IsNullOrWhiteSpace(createdTo))
 {
 if (DateTime.TryParseExact(createdTo, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo))
 {
 usedTo = DateTime.SpecifyKind(parsedTo, DateTimeKind.Unspecified);
 }
 else if (DateTime.TryParse(createdTo, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedGenericTo))
 {
 var localTo = parsedGenericTo.ToLocalTime();
 usedTo = DateTime.SpecifyKind(localTo, DateTimeKind.Unspecified);
 }
 }

 var result = await _couponService.GetRedeemedCouponsAsync(
1, int.MaxValue, usedFrom, usedTo, soldBy, couponDefinitionCode, couponDefinitionName);

 using var stream = new MemoryStream();
 using var writer = new PdfWriter(stream);
 using var pdf = new PdfDocument(writer);
 using var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4);

 // Load Thai font - with better fallback options
 PdfFont thaiFont;
 try
 {
 string fontPath = "";
 
 fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "THSarabunNew.ttf");
 if (System.IO.File.Exists(fontPath))
 {
 _logger.LogInformation("Using font: THSarabunNew.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "angsa.ttf")))
 {
 _logger.LogInformation("Using font: angsa.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "cordia.ttf")))
 {
 _logger.LogInformation("Using font: cordia.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "browa.ttf")))
 {
 _logger.LogInformation("Using font: browa.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "tahoma.ttf")))
 {
 _logger.LogInformation("Using font: tahoma.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "ARIALUNI.TTF")))
 {
 _logger.LogInformation("Using font: ARIALUNI.TTF");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 else
 {
 _logger.LogWarning("No Thai font found in system. Thai text may not display correctly.");
 thaiFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
 }
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error loading Thai font, falling back to Helvetica");
 thaiFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
 }

 document.SetFont(thaiFont);
 document.SetMargins(20,20,20,20);

 var mainTitle = new Paragraph("รายงานคูปองประจำวัน")
 .SetTextAlignment(TextAlignment.CENTER)
 .SetFontSize(18)
 .SetFont(thaiFont);
 document.Add(mainTitle);

 var dateInfo = new Paragraph($"วันที่พิมพ์: {DateTime.Now:dd/MM/yyyy HH:mm}")
 .SetTextAlignment(TextAlignment.CENTER)
 .SetFontSize(10)
 .SetFont(thaiFont);
 document.Add(dateInfo);

 if (usedFrom.HasValue || usedTo.HasValue)
 {
 var dateRange = $"ช่วงวันที่: {usedFrom?.ToString("dd/MM/yyyy") ?? "N/A"} - {usedTo?.ToString("dd/MM/yyyy") ?? "N/A"}";
 document.Add(new Paragraph(dateRange)
 .SetTextAlignment(TextAlignment.CENTER)
 .SetFontSize(10)
 .SetFont(thaiFont));
 }

 document.Add(new Paragraph("\n").SetFontSize(3));

 var table = new Table(new float[] {10,10,15,8,8,10,10,10 });
 table.SetWidth(UnitValue.CreatePercentValue(100));
 table.SetFont(thaiFont);
 table.SetFontSize(6);

 var headerBgColor = new iText.Kernel.Colors.DeviceRgb(200,200,200);
 AddHeaderCell(table, "รหัสคูปอง", headerBgColor, thaiFont);
 AddHeaderCell(table, "รหัสคำนิยาม", headerBgColor, thaiFont);
 AddHeaderCell(table, "ชื่อคำนิยาม", headerBgColor, thaiFont);
 AddHeaderCell(table, "ราคา", headerBgColor, thaiFont);
 AddHeaderCell(table, "สถานะ", headerBgColor, thaiFont);
 AddHeaderCell(table, "ผู้ใช้", headerBgColor, thaiFont);
 AddHeaderCell(table, includeRetrievedDate ? "วันที่ใช้/ขาย" : "วันที่ใช้", headerBgColor, thaiFont);
 AddHeaderCell(table, "หมดอายุ", headerBgColor, thaiFont);

 // จัดเรียงข้อมูลตามวันที่ใช้จากเก่าไปใหม่
 var sortedItems = result.Items.OrderBy(x => x.UsedDate ?? DateTime.MinValue).ToList();
 
 foreach (var item in sortedItems)
 {
 table.AddCell(CreateCell(TruncateText(item.GeneratedCode,12), thaiFont));
 table.AddCell(CreateCell(TruncateText(item.CouponDefinitionCode,10), thaiFont));
 table.AddCell(CreateCell(TruncateText(item.CouponDefinitionName,15), thaiFont));
 table.AddCell(CreateCell(item.CouponDefinitionPrice.ToString("N0"), thaiFont));
 table.AddCell(CreateCell(item.StatusText, thaiFont));
 table.AddCell(CreateCell(TruncateText(item.UsedBy ?? "-",10), thaiFont));
                    var usedDateText = item.UsedDate.HasValue
                    ? $"{item.UsedDate.Value:dd/MM/yy} เวลา {item.UsedDate.Value:HH:mm} น."
                    : "-";
                    table.AddCell(CreateCell(usedDateText, thaiFont));
                    table.AddCell(CreateCell(item.ExpiresAt?.ToString("dd/MM/yy") ?? "-", thaiFont));
 }

 document.Add(table);

 // สร้างส่วนสรุปตามวันที่และชื่อคำนิยาม
 using var ctx = new CouponContext();
 
 var summaryGroups = result.Items
 .Where(x => x.UsedDate.HasValue)
 .GroupBy(x => new { 
 UsedDate = x.UsedDate!.Value.Date, 
 DefinitionId = x.CouponDefinitionId,
 DefinitionCode = x.CouponDefinitionCode,
 DefinitionName = x.CouponDefinitionName 
 })
 .OrderBy(g => g.Key.UsedDate)
 .ThenBy(g => g.Key.DefinitionName)
 .ToList();

                if (summaryGroups.Any())
                {
                    // ขึ้นหน้าใหม่สำหรับส่วนสรุป
                    document.Add(new AreaBreak(iText.Layout.Properties.AreaBreakType.NEXT_PAGE));

                    var summaryTitle = new Paragraph("สรุปการใช้คูปองแยกตามวันและชื่อคูปอง")
                    .SetFontSize(14)
                    .SetFont(thaiFont)
                    .SetTextAlignment(TextAlignment.CENTER);
                    document.Add(summaryTitle);

                    document.Add(new Paragraph("\n").SetFontSize(3));

                    var summaryTable = new Table(new float[] { 10, 12, 18, 10, 10, 12, 10, 12 });
                    summaryTable.SetWidth(UnitValue.CreatePercentValue(100));
                    summaryTable.SetFont(thaiFont);
                    summaryTable.SetFontSize(7);

                    var summaryHeaderBg = new iText.Kernel.Colors.DeviceRgb(173, 216, 230);
                    AddHeaderCell(summaryTable, "วันที่ใช้", summaryHeaderBg, thaiFont);
                    AddHeaderCell(summaryTable, "รหัสคำนิยาม", summaryHeaderBg, thaiFont);
                    AddHeaderCell(summaryTable, "ชื่อคำนิยาม", summaryHeaderBg, thaiFont);
                    AddHeaderCell(summaryTable, "ทั้งหมด", summaryHeaderBg, thaiFont);
                    AddHeaderCell(summaryTable, "ใช้ก่อนหน้า", summaryHeaderBg, thaiFont);
                    AddHeaderCell(summaryTable, "คงเหลือก่อน", summaryHeaderBg, thaiFont);
                    AddHeaderCell(summaryTable, "ใช้ในวันนี้", summaryHeaderBg, thaiFont);
                    AddHeaderCell(summaryTable, "คงเหลือหลัง", summaryHeaderBg, thaiFont);

                    foreach (var group in summaryGroups)
                    {
                        var usedDate = group.Key.UsedDate;
                        var definitionId = group.Key.DefinitionId;
                        var definitionCode = group.Key.DefinitionCode;
                        var definitionName = group.Key.DefinitionName;

                        var totalGenerated = await ctx.GeneratedCoupons
                            .CountAsync(gc => gc.CouponDefinitionId == definitionId);

                        var usedBeforeToday = await ctx.GeneratedCoupons
                            .CountAsync(gc => gc.CouponDefinitionId == definitionId 
                                && gc.IsUsed 
                                && gc.UsedDate.HasValue
                                && gc.UsedDate.Value.Date < usedDate);

                        var remainingBeforeToday = totalGenerated - usedBeforeToday;
                        var usedToday = group.Count();
                        var remainingAfterToday = remainingBeforeToday - usedToday;

                        summaryTable.AddCell(CreateCell(usedDate.ToString("dd/MM/yyyy"), thaiFont));
                        summaryTable.AddCell(CreateCell(TruncateText(definitionCode, 12), thaiFont));
                        summaryTable.AddCell(CreateCell(TruncateText(definitionName, 20), thaiFont));
                        summaryTable.AddCell(CreateCell(totalGenerated.ToString("N0"), thaiFont));
                        summaryTable.AddCell(CreateCell(usedBeforeToday.ToString("N0"), thaiFont));
                        summaryTable.AddCell(CreateCell(remainingBeforeToday.ToString("N0"), thaiFont));
                        summaryTable.AddCell(CreateCell(usedToday.ToString("N0"), thaiFont));
                        summaryTable.AddCell(CreateCell(remainingAfterToday.ToString("N0"), thaiFont));
                    }

                    document.Add(summaryTable);
                }

                // เพิ่มส่วนสรุปรวมตามคำนิยาม (รวมทุกวันในช่วงที่เลือก)
                var definitionSummary = result.Items
                    .Where(x => x.UsedDate.HasValue)
                    .GroupBy(x => new { 
                        DefinitionId = x.CouponDefinitionId,
                        DefinitionCode = x.CouponDefinitionCode,
                        DefinitionName = x.CouponDefinitionName 
                    })
                    .OrderBy(g => g.Key.DefinitionName)
                    .ToList();

                if (definitionSummary.Any())
                {
                    // ขึ้นหน้าใหม่สำหรับส่วนสรุป
                    document.Add(new AreaBreak(iText.Layout.Properties.AreaBreakType.NEXT_PAGE));

                    var defSummaryTitle = new Paragraph("สรุปรวมตามชื่อคูปอง (ทุกวันในช่วงที่เลือก)")
                        .SetFontSize(14)
                        .SetFont(thaiFont)
                        .SetTextAlignment(TextAlignment.CENTER);
                    document.Add(defSummaryTitle);

                    document.Add(new Paragraph("\n").SetFontSize(3));

                    var defSummaryTable = new Table(new float[] { 15, 20, 12, 12, 12, 12, 12 });
                    defSummaryTable.SetWidth(UnitValue.CreatePercentValue(100));
                    defSummaryTable.SetFont(thaiFont);
                    defSummaryTable.SetFontSize(8);

                    var defSummaryHeaderBg = new iText.Kernel.Colors.DeviceRgb(144, 238, 144);
                    AddHeaderCell(defSummaryTable, "รหัสคำนิยาม", defSummaryHeaderBg, thaiFont);
                    AddHeaderCell(defSummaryTable, "ชื่อคำนิยาม", defSummaryHeaderBg, thaiFont);
                    AddHeaderCell(defSummaryTable, "ทั้งหมด", defSummaryHeaderBg, thaiFont);
                    AddHeaderCell(defSummaryTable, "ใช้ก่อนช่วง", defSummaryHeaderBg, thaiFont);
                    AddHeaderCell(defSummaryTable, "คงเหลือก่อน", defSummaryHeaderBg, thaiFont);
                    AddHeaderCell(defSummaryTable, "ใช้ในช่วง", defSummaryHeaderBg, thaiFont);
                    AddHeaderCell(defSummaryTable, "คงเหลือหลัง", defSummaryHeaderBg, thaiFont);

                    foreach (var group in definitionSummary)
                    {
                        var definitionId = group.Key.DefinitionId;
                        var definitionCode = group.Key.DefinitionCode;
                        var definitionName = group.Key.DefinitionName;

                        var totalGenerated = await ctx.GeneratedCoupons
                            .CountAsync(gc => gc.CouponDefinitionId == definitionId);

                        var firstUsedDate = group.Min(x => x.UsedDate!.Value.Date);

                        var usedBeforePeriod = await ctx.GeneratedCoupons
                            .CountAsync(gc => gc.CouponDefinitionId == definitionId 
                                && gc.IsUsed 
                                && gc.UsedDate.HasValue
                                && gc.UsedDate.Value.Date < firstUsedDate);

                        var remainingBeforePeriod = totalGenerated - usedBeforePeriod;
                        var usedInPeriod = group.Count();
                        var remainingAfterPeriod = remainingBeforePeriod - usedInPeriod;

                        defSummaryTable.AddCell(CreateCell(TruncateText(definitionCode, 15), thaiFont));
                        defSummaryTable.AddCell(CreateCell(TruncateText(definitionName, 25), thaiFont));
                        defSummaryTable.AddCell(CreateCell(totalGenerated.ToString("N0"), thaiFont));
                        defSummaryTable.AddCell(CreateCell(usedBeforePeriod.ToString("N0"), thaiFont));
                        defSummaryTable.AddCell(CreateCell(remainingBeforePeriod.ToString("N0"), thaiFont));
                        defSummaryTable.AddCell(CreateCell(usedInPeriod.ToString("N0"), thaiFont));
                        defSummaryTable.AddCell(CreateCell(remainingAfterPeriod.ToString("N0"), thaiFont));
                    }

                    document.Add(defSummaryTable);
                }

                document.Close();

                var fileName = $"รายงานคูปองประจำวัน_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.pdf";
                return File(stream.ToArray(), "application/pdf", fileName);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error exporting to PDF: {Message}", ex.Message);
 return StatusCode(500, new { message = "Error exporting to PDF", error = ex.Message });
 }
 }

        /// <summary>
        /// Helper method to add a header cell to PDF table with styling.
        /// </summary>
        /// <param name="table">The table to add header cell to.</param>
        /// <param name="text">Header text.</param>
        /// <param name="bgColor">Background color for header.</param>
        /// <param name="font">Font to use for Thai language support.</param>
        private static void AddHeaderCell(Table table, string text, iText.Kernel.Colors.DeviceRgb bgColor, PdfFont font)
        {
            var cell = new Cell()
                .Add(new Paragraph(text).SetFont(font).SetFontSize(9))
                .SetBackgroundColor(bgColor)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SetPadding(3);
            table.AddHeaderCell(cell);
        }

        /// <summary>
   /// Helper method to create a styled cell for PDF table.
        /// </summary>
      /// <param name="text">Cell content text.</param>
        /// <param name="font">Font to use for Thai language support.</param>
        /// <returns>Configured table cell.</returns>
        private static Cell CreateCell(string text, PdfFont font)
        {
            return new Cell()
              .Add(new Paragraph(text ?? "-").SetFont(font).SetFontSize(7))
     .SetTextAlignment(TextAlignment.CENTER)
         .SetVerticalAlignment(VerticalAlignment.MIDDLE)
      .SetPadding(2);
 }

  /// <summary>
    /// Helper method to truncate text to specified maximum length.
        /// </summary>
     /// <param name="text">Text to truncate.</param>
 /// <param name="maxLength">Maximum length allowed.</param>
      /// <returns>Truncated text with ellipsis if exceeded max length.</returns>
private static string TruncateText(string text, int maxLength)
        {
      if (string.IsNullOrEmpty(text)) return "-";
       return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }
}

