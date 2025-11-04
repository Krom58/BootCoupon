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

            var result = await _couponService.RedeemCouponAsync(req.Code.Trim(), req.RedeemedBy ?? "system", req.ReceiptItemId);

_logger.LogInformation("Redeem outcome for {Code}: {Outcome} - {Message}", req.Code, result.Result, result.Message);

            var response = new
            {
         result = result.Result.ToString(),
           message = result.Message,
    couponId = result.CouponId,
                usedBy = result.UsedBy,
     usedDate = result.UsedDate
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
        /// <returns>Excel file containing sold coupons report.</returns>
    [HttpGet("export/excel")]
        public async Task<IActionResult> ExportToExcel(
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
            try
          {
           var result = await _couponService.GetSoldCouponsAsync(
     1, int.MaxValue, couponDefinitionId, searchCode, receiptItemId,
     receiptId, soldBy, couponDefinitionCode, couponDefinitionName,
         createdFrom, createdTo);

        using var workbook = new XLWorkbook();
    var worksheet = workbook.Worksheets.Add("รายงานคูปอง");

         worksheet.Cell(1, 1).Value = "รหัสคูปอง";
     worksheet.Cell(1, 2).Value = "รหัสคำนิยาม";
     worksheet.Cell(1, 3).Value = "ชื่อคำนิยาม";
   worksheet.Cell(1, 4).Value = "ราคา";
      worksheet.Cell(1, 5).Value = "สถานะ";
          worksheet.Cell(1, 6).Value = "การใช้งาน";
              worksheet.Cell(1, 7).Value = "ผู้ใช้";
  worksheet.Cell(1, 8).Value = "วันที่ใช้";
    worksheet.Cell(1, 9).Value = "สร้างเมื่อ";
 worksheet.Cell(1, 10).Value = "วันหมดอายุ";

         var headerRange = worksheet.Range(1, 1, 1, 10);
        headerRange.Style.Font.Bold = true;
    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

       int row = 2;
    foreach (var item in result.Items)
     {
worksheet.Cell(row, 1).Value = item.GeneratedCode;
 worksheet.Cell(row, 2).Value = item.CouponDefinitionCode;
         worksheet.Cell(row, 3).Value = item.CouponDefinitionName;
      worksheet.Cell(row, 4).Value = item.CouponDefinitionPrice;
         worksheet.Cell(row, 5).Value = item.StatusText;
   worksheet.Cell(row, 6).Value = item.UsageText;
      worksheet.Cell(row, 7).Value = item.UsedBy ?? "";
         worksheet.Cell(row, 8).Value = item.UsedDate?.ToString("dd/MM/yyyy HH:mm") ?? "";
   worksheet.Cell(row, 9).Value = item.CreatedAt.ToString("dd/MM/yyyy HH:mm");
  worksheet.Cell(row, 10).Value = item.ExpiresAt?.ToString("dd/MM/yyyy") ?? "";
     row++;
         }

       worksheet.Columns().AdjustToContents();

         worksheet.Cell(row + 1, 1).Value = "สรุป";
                worksheet.Cell(row + 1, 1).Style.Font.Bold = true;
 worksheet.Cell(row + 2, 1).Value = $"จำนวนรายการ: {result.TotalCount}";
          worksheet.Cell(row + 3, 1).Value = $"ยอดรวม: {result.Items.Sum(x => x.CouponDefinitionPrice):N2} บาท";

   using var stream = new MemoryStream();
           workbook.SaveAs(stream);
    stream.Position = 0;

                var fileName = $"รายงานคูปอง_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
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
            [FromQuery] DateTime? createdFrom = null,
            [FromQuery] DateTime? createdTo = null)
        {
     try
   {
   var result = await _couponService.GetSoldCouponsAsync(
       1, int.MaxValue, couponDefinitionId, searchCode, receiptItemId,
          receiptId, soldBy, couponDefinitionCode, couponDefinitionName,
 createdFrom, createdTo);

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
 
   // Option 1: TH Sarabun New (Best for Thai)
fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "THSarabunNew.ttf");
      if (System.IO.File.Exists(fontPath))
      {
     _logger.LogInformation("Using font: THSarabunNew.ttf");
    thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
            }
       // Option 2: Angsana New
      else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "angsa.ttf")))
     {
        _logger.LogInformation("Using font: angsa.ttf");
   thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
     }
 // Option 3: Cordia New
          else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "cordia.ttf")))
 {
      _logger.LogInformation("Using font: cordia.ttf");
    thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
   }
      // Option 4: Browallia New
 else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "browa.ttf")))
  {
   _logger.LogInformation("Using font: browa.ttf");
      thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
          }
   // Option 5: Try Tahoma (supports Thai)
      else if (System.IO.File.Exists(fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "tahoma.ttf")))
{
_logger.LogInformation("Using font: tahoma.ttf");
  thaiFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.FORCE_EMBEDDED);
  }
     // Option 6: Try Arial Unicode MS (supports Thai)
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
     document.SetMargins(20, 20, 20, 20);

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

        if (createdFrom.HasValue || createdTo.HasValue)
     {
     var dateRange = $"ช่วงวันที่: {createdFrom?.ToString("dd/MM/yyyy") ?? "N/A"} - {createdTo?.ToString("dd/MM/yyyy") ?? "N/A"}";
     document.Add(new Paragraph(dateRange)
 .SetTextAlignment(TextAlignment.CENTER)
    .SetFontSize(10)
         .SetFont(thaiFont));
         }

     document.Add(new Paragraph("\n").SetFontSize(3));

       // Create table - adjusted for A4 portrait
  var table = new Table(new float[] { 10, 10, 15, 8, 8, 12, 10, 10, 10 }); // 9 columns (removed one date column)
       table.SetWidth(UnitValue.CreatePercentValue(100));
   table.SetFont(thaiFont);
    table.SetFontSize(8);

  // Header
    var headerBgColor = new iText.Kernel.Colors.DeviceRgb(200, 200, 200);
       AddHeaderCell(table, "รหัสคูปอง", headerBgColor, thaiFont);
      AddHeaderCell(table, "รหัสคำนิยาม", headerBgColor, thaiFont);
    AddHeaderCell(table, "ชื่อคำนิยาม", headerBgColor, thaiFont);
AddHeaderCell(table, "ราคา", headerBgColor, thaiFont);
 AddHeaderCell(table, "สถานะ", headerBgColor, thaiFont);
  AddHeaderCell(table, "การใช้งาน", headerBgColor, thaiFont);
      AddHeaderCell(table, "ผู้ใช้", headerBgColor, thaiFont);
     AddHeaderCell(table, "วันที่ใช้", headerBgColor, thaiFont);
     AddHeaderCell(table, "หมดอายุ", headerBgColor, thaiFont);

            // Data rows - compact for A4 portrait
       foreach (var item in result.Items)
       {
           table.AddCell(CreateCell(TruncateText(item.GeneratedCode, 12), thaiFont));
         table.AddCell(CreateCell(TruncateText(item.CouponDefinitionCode, 10), thaiFont));
     table.AddCell(CreateCell(TruncateText(item.CouponDefinitionName, 15), thaiFont));
 table.AddCell(CreateCell(item.CouponDefinitionPrice.ToString("N0"), thaiFont)); // No decimals to save space
       table.AddCell(CreateCell(item.StatusText, thaiFont));
      table.AddCell(CreateCell(TruncateText(item.UsageText, 15), thaiFont));
    table.AddCell(CreateCell(TruncateText(item.UsedBy ?? "-", 10), thaiFont));
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

 var summaryTable = new Table(2);
         summaryTable.SetWidth(UnitValue.CreatePercentValue(50));
      summaryTable.SetFont(thaiFont);
           summaryTable.SetFontSize(10);

      summaryTable.AddCell(new Cell().Add(new Paragraph("จำนวนรายการ:")).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
   summaryTable.AddCell(new Cell().Add(new Paragraph($"{result.TotalCount:N0} รายการ")).SetBorder(iText.Layout.Borders.Border.NO_BORDER));

     summaryTable.AddCell(new Cell().Add(new Paragraph("ยอดรวม:")).SetBorder(iText.Layout.Borders.Border.NO_BORDER));
   summaryTable.AddCell(new Cell().Add(new Paragraph($"{result.Items.Sum(x => x.CouponDefinitionPrice):N2} บาท")).SetBorder(iText.Layout.Borders.Border.NO_BORDER));

   document.Add(summaryTable);

       document.Close();

     var fileName = $"รายงานคูปองประจำวัน_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
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

