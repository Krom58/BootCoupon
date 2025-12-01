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
using CouponManagement.Shared; // for CouponContext
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
 DateTime? createdFromRange = null;
 DateTime? createdToRange = null;
 if(!string.IsNullOrWhiteSpace(createdFrom))
 {
 if(DateTime.TryParseExact(createdFrom, "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedFrom))
 {
 usedFrom = DateTime.SpecifyKind(parsedFrom, DateTimeKind.Unspecified);
 createdFromRange = usedFrom;
 }
 else if(DateTime.TryParse(createdFrom, out var parsedGenericFrom))
 {
 // if parsed as UTC, convert to local naive; otherwise treat as unspecified/local
 if(parsedGenericFrom.Kind == DateTimeKind.Utc)
 usedFrom = DateTime.SpecifyKind(parsedGenericFrom.ToLocalTime(), DateTimeKind.Unspecified);
 else
 usedFrom = DateTime.SpecifyKind(parsedGenericFrom, DateTimeKind.Unspecified);
 createdFromRange = usedFrom;
 }
 }
 if(!string.IsNullOrWhiteSpace(createdTo))
 {
 if(DateTime.TryParseExact(createdTo, "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedTo))
 {
 usedTo = DateTime.SpecifyKind(parsedTo, DateTimeKind.Unspecified);
 createdToRange = usedTo;
 }
 else if(DateTime.TryParse(createdTo, out var parsedGenericTo))
 {
 if(parsedGenericTo.Kind == DateTimeKind.Utc)
 usedTo = DateTime.SpecifyKind(parsedGenericTo.ToLocalTime(), DateTimeKind.Unspecified);
 else
 usedTo = DateTime.SpecifyKind(parsedGenericTo, DateTimeKind.Unspecified);
 createdToRange = usedTo;
 }
 }

 // Use redeemed coupons (IsUsed == true) filtered by UsedDate (createdFrom/createdTo represent UsedDate range from client)
 var result = await _couponService.GetRedeemedCouponsAsync(
1, int.MaxValue, usedFrom, usedTo, soldBy, couponDefinitionCode, couponDefinitionName);

 using var workbook = new XLWorkbook();

 var wantDetailed = string.Equals(reportMode, "detailed", StringComparison.OrdinalIgnoreCase) || string.Equals(reportMode, "both", StringComparison.OrdinalIgnoreCase);
 var wantSummary = string.Equals(reportMode, "summary", StringComparison.OrdinalIgnoreCase) || string.Equals(reportMode, "both", StringComparison.OrdinalIgnoreCase);

 if (wantDetailed)
 {
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
 foreach (var item in result.Items)
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

 worksheet.Cell(row +1,1).Value = "สรุป (จากรายการที่แสดง)";
 worksheet.Cell(row +1,1).Style.Font.Bold = true;
 worksheet.Cell(row +2,1).Value = $"จำนวนรายการ: {result.TotalCount}";
 worksheet.Cell(row +3,1).Value = $"ยอดรวม: {result.Items.Sum(x => x.CouponDefinitionPrice):N2} บาท";
 }

 if (wantSummary)
 {
 // Compute summary counts from GeneratedCoupons within createdFromRange..createdToRange
 using var ctx = new CouponContext();
 var query = ctx.GeneratedCoupons.AsQueryable();
 if (createdFromRange.HasValue) query = query.Where(gc => gc.CreatedAt >= createdFromRange.Value);
 if (createdToRange.HasValue) query = query.Where(gc => gc.CreatedAt <= createdToRange.Value);

 var totalGenerated = await query.CountAsync();
 var totalCOM = await query.CountAsync(gc => gc.IsComplimentary);
 var totalRedeemed = await query.CountAsync(gc => gc.IsUsed);
 var totalUnredeemed = await query.CountAsync(gc => !gc.IsUsed && gc.ReceiptItemId == null);
 var comRedeemed = await query.CountAsync(gc => gc.IsComplimentary && gc.IsUsed);
 var comUnredeemed = await query.CountAsync(gc => gc.IsComplimentary && !gc.IsUsed && gc.ReceiptItemId == null);

 var sumSheet = workbook.Worksheets.Add("สรุป");
 int r =1;
 sumSheet.Cell(r++,1).Value = "สรุปข้อมูลคูปอง";
 sumSheet.Cell(r -1,1).Style.Font.Bold = true;

 sumSheet.Cell(r++,1).Value = $"ช่วงวันที่: {(createdFromRange.HasValue ? createdFromRange.Value.ToString("dd/MM/yyyy") : "N/A")} - {(createdToRange.HasValue ? createdToRange.Value.ToString("dd/MM/yyyy") : "N/A")}";
 sumSheet.Cell(r++,1).Value = $"คูปองทั้งหมด (สร้าง): {totalGenerated:N0}";
 sumSheet.Cell(r++,1).Value = $"คูปอง COM ทั้งหมด: {totalCOM:N0}";
 sumSheet.Cell(r++,1).Value = $"คูปองที่ยังไม่ถูกตัด: {totalUnredeemed:N0}";
 sumSheet.Cell(r++,1).Value = $"คูปองที่ถูกตัดแล้ว: {totalRedeemed:N0}";
 sumSheet.Cell(r++,1).Value = $"คูปอง COM ที่ยังไม่ถูกตัด: {comUnredeemed:N0}";
 sumSheet.Cell(r++,1).Value = $"คูปอง COM ที่ถูกตัดแล้ว: {comRedeemed:N0}";

 sumSheet.Columns().AdjustToContents();
 }

 using var stream = new MemoryStream();
 workbook.SaveAs(stream);
 stream.Position =0;

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
 /// <param name="includeRetrievedDate">When true, include the retrieved/used date label as 'วันที่ใช้/ขาย' in outputs.</param>
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
 DateTime? createdFromRange = null;
 DateTime? createdToRange = null;
 if (!string.IsNullOrWhiteSpace(createdFrom))
 {
 if (DateTime.TryParseExact(createdFrom, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom))
 {
 usedFrom = DateTime.SpecifyKind(parsedFrom, DateTimeKind.Unspecified);
 createdFromRange = usedFrom;
 }
 else if (DateTime.TryParse(createdFrom, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedGenericFrom))
 {
 // parsedGenericFrom is in UTC (AssumeUniversal) -> convert to local then treat as unspecified
 var local = parsedGenericFrom.ToLocalTime();
 usedFrom = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
 createdFromRange = usedFrom;
 }
 }

 if (!string.IsNullOrWhiteSpace(createdTo))
 {
 if (DateTime.TryParseExact(createdTo, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo))
 {
 usedTo = DateTime.SpecifyKind(parsedTo, DateTimeKind.Unspecified);
 createdToRange = usedTo;
 }
 else if (DateTime.TryParse(createdTo, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedGenericTo))
 {
 var localTo = parsedGenericTo.ToLocalTime();
 usedTo = DateTime.SpecifyKind(localTo, DateTimeKind.Unspecified);
 createdToRange = usedTo;
 }
 }

 var result = await _couponService.GetRedeemedCouponsAsync(
1, int.MaxValue, usedFrom, usedTo, soldBy, couponDefinitionCode, couponDefinitionName);

 using var stream = new MemoryStream();
 using var writer = new PdfWriter(stream);
 using var pdf = new PdfDocument(writer);
 using var document = new Document(pdf, iText.Kernel.Geom.PageSize.A4); // A4 Portrait

 // Load Thai font - with better fallback options
 PdfFont thaiFont;
 try
 {
 // Try multiple Thai font options
 string fontPath = "";
 
 // Option1: TH Sarabun New (Best for Thai)
fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "THSarabunNew.ttf");
 if (System.IO.File.Exists(fontPath))
 {
 _logger.LogInformation("Using font: THSarabunNew.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 // Option2: Angsana New
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "angsa.ttf")))
 {
 _logger.LogInformation("Using font: angsa.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 // Option3: Cordia New
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "cordia.ttf")))
 {
 _logger.LogInformation("Using font: cordia.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 // Option4: Browallia New
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "browa.ttf")))
 {
 _logger.LogInformation("Using font: browa.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 // Option5: Try Tahoma (supports Thai)
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "tahoma.ttf")))
{
_logger.LogInformation("Using font: tahoma.ttf");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 // Option6: Try Arial Unicode MS (supports Thai)
else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "ARIALUNI.TTF")))
 {
 _logger.LogInformation("Using font: ARIALUNI.TTF");
 thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
 }
 else
 {
 _logger.LogWarning("No Thai font found in system. Thai text may not display correctly.");
 // Last resort: use Helvetica (won't show Thai properly)
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

 // Main Title
 var mainTitle = new Paragraph("รายงานคูปองประจำวัน")
 .SetTextAlignment(TextAlignment.CENTER)
 .SetFontSize(18)
 .SetFont(thaiFont);
 document.Add(mainTitle);

 // Date info
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

 var wantDetailed = string.Equals(reportMode, "detailed", StringComparison.OrdinalIgnoreCase) || string.Equals(reportMode, "both", StringComparison.OrdinalIgnoreCase);
 var wantSummary = string.Equals(reportMode, "summary", StringComparison.OrdinalIgnoreCase) || string.Equals(reportMode, "both", StringComparison.OrdinalIgnoreCase);

 if (wantSummary)
 {
 // Compute summary
 using var ctx = new CouponContext();
 var q = ctx.GeneratedCoupons.AsQueryable();
 if (createdFromRange.HasValue) q = q.Where(gc => gc.CreatedAt >= createdFromRange.Value);
 if (createdToRange.HasValue) q = q.Where(gc => gc.CreatedAt <= createdToRange.Value);

 var totalGenerated = await q.CountAsync();
 var totalCOM = await q.CountAsync(gc => gc.IsComplimentary);
 var totalRedeemed = await q.CountAsync(gc => gc.IsUsed);
 var totalUnredeemed = await q.CountAsync(gc => !gc.IsUsed && gc.ReceiptItemId == null);
 var comRedeemed = await q.CountAsync(gc => gc.IsComplimentary && gc.IsUsed);
 var comUnredeemed = await q.CountAsync(gc => gc.IsComplimentary && !gc.IsUsed && gc.ReceiptItemId == null);

 // earlier summary title in PDF export
 var summaryTitlePdf = new Paragraph("สรุปข้อมูลคูปอง")
 .SetFont(thaiFont).SetFontSize(12);
 document.Add(summaryTitlePdf);

 var summaryTable = new Table(2).SetWidth(UnitValue.CreatePercentValue(60));
 summaryTable.AddCell(new Cell().Add(new Paragraph("ช่วงวันที่").SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
 summaryTable.AddCell(new Cell().Add(new Paragraph($"{(createdFromRange?.ToString("dd/MM/yyyy") ?? "N/A")} - {(createdToRange?.ToString("dd/MM/yyyy") ?? "N/A")} ").SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);

 summaryTable.AddCell(new Cell().Add(new Paragraph("คูปองทั้งหมด (สร้าง)").SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
 summaryTable.AddCell(new Cell().Add(new Paragraph(totalGenerated.ToString("N0")).SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);

 summaryTable.AddCell(new Cell().Add(new Paragraph("คูปอง COM ทั้งหมด").SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
 summaryTable.AddCell(new Cell().Add(new Paragraph(totalCOM.ToString("N0")).SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);

 summaryTable.AddCell(new Cell().Add(new Paragraph("คูปองที่ยังไม่ถูกตัด").SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
 summaryTable.AddCell(new Cell().Add(new Paragraph(totalUnredeemed.ToString("N0")).SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);

 summaryTable.AddCell(new Cell().Add(new Paragraph("คูปองที่ถูกตัดแล้ว").SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
 summaryTable.AddCell(new Cell().Add(new Paragraph(totalRedeemed.ToString("N0")).SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);

 summaryTable.AddCell(new Cell().Add(new Paragraph("คูปอง COM ที่ยังไม่ถูกตัด").SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
 summaryTable.AddCell(new Cell().Add(new Paragraph(comUnredeemed.ToString("N0")).SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);

 summaryTable.AddCell(new Cell().Add(new Paragraph("คูปอง COM ที่ถูกตัดแล้ว").SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);
 summaryTable.AddCell(new Cell().Add(new Paragraph(comRedeemed.ToString("N0")).SetFont(thaiFont))).SetBorder(iText.Layout.Borders.Border.NO_BORDER);

 document.Add(summaryTable);
 document.Add(new Paragraph("\n").SetFontSize(4));
 }

 if (wantDetailed)
 {
 // Create table - adjusted for A4 portrait
 var table = new Table(new float[] {10,10,15,8,8,12,10,10,10 }); //9 columns (removed one date column)
 table.SetWidth(UnitValue.CreatePercentValue(100));
 table.SetFont(thaiFont);
 table.SetFontSize(8);

 // Header
 var headerBgColor = new iText.Kernel.Colors.DeviceRgb(200,200,200);
 AddHeaderCell(table, "รหัสคูปอง", headerBgColor, thaiFont);
 AddHeaderCell(table, "รหัสคำนิยาม", headerBgColor, thaiFont);
 AddHeaderCell(table, "ชื่อคำนิยาม", headerBgColor, thaiFont);
AddHeaderCell(table, "ราคา", headerBgColor, thaiFont);
 AddHeaderCell(table, "สถานะ", headerBgColor, thaiFont);
 AddHeaderCell(table, "การใช้งาน", headerBgColor, thaiFont);
 AddHeaderCell(table, "ผู้ใช้", headerBgColor, thaiFont);
 AddHeaderCell(table, includeRetrievedDate ? "วันที่ใช้/ขาย" : "วันที่ใช้", headerBgColor, thaiFont);
 AddHeaderCell(table, "หมดอายุ", headerBgColor, thaiFont);

 // Data rows - compact for A4 portrait
 foreach (var item in result.Items)
 {
 table.AddCell(CreateCell(TruncateText(item.GeneratedCode,12), thaiFont));
 table.AddCell(CreateCell(TruncateText(item.CouponDefinitionCode,10), thaiFont));
 table.AddCell(CreateCell(TruncateText(item.CouponDefinitionName,15), thaiFont));
 table.AddCell(CreateCell(item.CouponDefinitionPrice.ToString("N0"), thaiFont)); // No decimals to save space
 table.AddCell(CreateCell(item.StatusText, thaiFont));
 table.AddCell(CreateCell(TruncateText(item.UsageText,15), thaiFont));
 table.AddCell(CreateCell(TruncateText(item.UsedBy ?? "-",10), thaiFont));
 table.AddCell(CreateCell(item.UsedDate?.ToString("dd/MM/yy\nHH:mm") ?? "-", thaiFont)); // Shorter date format
 table.AddCell(CreateCell(item.ExpiresAt?.ToString("dd/MM/yy") ?? "-", thaiFont)); // Shorter date
 }

 document.Add(table);

 // Summary
 document.Add(new Paragraph("\n").SetFontSize(5));

 var summaryTitle = new Paragraph("สรุป")
 .SetFontSize(12)
 .SetFont(thaiFont);
 document.Add(summaryTitle);

 var summaryTable2 = new Table(2);
 summaryTable2.SetWidth(UnitValue.CreatePercentValue(50));
 summaryTable2.SetFont(thaiFont);
 summaryTable2.SetFontSize(10);

 summaryTable2.AddCell(new Cell().Add(new Paragraph("จำนวนรายการ:")).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
 summaryTable2.AddCell(new Cell().Add(new Paragraph($"{result.TotalCount:N0} รายการ")).SetBorder(iText.Layout.Borders.Border.NO_BORDER));

 summaryTable2.AddCell(new Cell().Add(new Paragraph("ยอดรวม:" )).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
 summaryTable2.AddCell(new Cell().Add(new Paragraph($"{result.Items.Sum(x => x.CouponDefinitionPrice):N2} บาท")).SetBorder(iText.Layout.Borders.Border.NO_BORDER));

 document.Add(summaryTable2);
 }

 document.Close();

 var fileName = $"รายงานคูปองประจำวัน_{DateTime.Now:dd/MM/yyyy_HH-mm-ss}.pdf";
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

