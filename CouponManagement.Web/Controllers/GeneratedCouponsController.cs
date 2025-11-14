using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CouponManagement.Shared.Services;
using Microsoft.Extensions.Logging;
using System;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace CouponManagement.Web.Controllers
{
    /// <summary>
    /// Controller for generated coupons operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class GeneratedCouponsController : ControllerBase
    {
        private readonly GeneratedCouponService _generatedCouponService;
        private readonly ILogger<GeneratedCouponsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratedCouponsController"/> class.
        /// </summary>
        /// <param name="generatedCouponService">The service for generated coupon operations.</param>
        /// <param name="logger">The logger instance.</param>
        public GeneratedCouponsController(
            GeneratedCouponService generatedCouponService,
            ILogger<GeneratedCouponsController> logger)
        {
            _generatedCouponService = generatedCouponService;
            _logger = logger;
        }

        /// <summary>
        /// Get generated coupon by code.
        /// </summary>
        /// <param name="code">Generated coupon code</param>
        /// <returns>Generated coupon details</returns>
      [HttpGet("bycode/{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            try
       {
                if (string.IsNullOrWhiteSpace(code))
       {
    return BadRequest(new { message = "Code is required" });
       }

       var coupon = await _generatedCouponService.GetByCodeAsync(code.Trim());
     
      if (coupon == null)
                {
                return NotFound(new { message = "Coupon not found" });
      }

    // Return model with isUsed field for redemption checking
         var response = new
             {
      id = coupon.Id,
        generatedCode = coupon.GeneratedCode,
  couponDefinitionId = coupon.CouponDefinitionId,
     batchNumber = coupon.BatchNumber,
 isUsed = coupon.IsUsed,
    usedDate = coupon.UsedDate,
    usedBy = coupon.UsedBy,
     createdAt = coupon.CreatedAt,
createdBy = coupon.CreatedBy,
        expiresAt = coupon.ExpiresAt,
        receiptItemId = coupon.ReceiptItemId,
    isComplimentary = coupon.IsComplimentary
       };

             return Ok(response);
       }
 catch (Exception ex)
  {
     _logger.LogError(ex, "Error getting generated coupon by code: {Code}", code);
    return StatusCode(500, new { message = "Internal server error", error = ex.Message });
 }
        }

        /// <summary>
      /// Mark a generated coupon as complimentary.
        /// </summary>
        /// <param name="code">Generated coupon code</param>
     [HttpPost("mark-complimentary/{code}")]
        public async Task<IActionResult> MarkComplimentary(string code)
      {
    try
        {
    if (string.IsNullOrWhiteSpace(code))
 return BadRequest(new { message = "Code is required" });

 var ok = await _generatedCouponService.MarkComplimentaryAsync(code.Trim());
 if (!ok)
 return NotFound(new { message = "Coupon not found" });

 return Ok(new { result = "Success" });
}
catch (Exception ex)
{
     _logger.LogError(ex, "Error marking complimentary for code: {Code}", code);
   return StatusCode(500, new { message = "Internal server error", error = ex.Message });
    }
   }

        /// <summary>
      /// Get generated coupon by ID.
        /// </summary>
        /// <param name="id">Generated coupon ID</param>
   /// <returns>Generated coupon details</returns>
     [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
      {
    try
        {
    var coupon = await _generatedCouponService.GetByIdAsync(id);
          
         if (coupon == null)
       {
         return NotFound(new { message = "Coupon not found" });
      }

    // Return model with isUsed field for redemption checking
    var response = new
   {
     id = coupon.Id,
   generatedCode = coupon.GeneratedCode,
        couponDefinitionId = coupon.CouponDefinitionId,
 batchNumber = coupon.BatchNumber,
      isUsed = coupon.IsUsed,
     usedDate = coupon.UsedDate,
  usedBy = coupon.UsedBy,
   createdAt = coupon.CreatedAt,
     createdBy = coupon.CreatedBy,
   expiresAt = coupon.ExpiresAt,
   receiptItemId = coupon.ReceiptItemId,
     isComplimentary = coupon.IsComplimentary
        };

  return Ok(response);
    }
  catch (Exception ex)
{
     _logger.LogError(ex, "Error getting generated coupon by ID: {Id}", id);
   return StatusCode(500, new { message = "Internal server error", error = ex.Message });
    }
   }

     /// <summary>
 /// Request model for resetting a generated coupon.
 /// Provide either the database Id or the generated Code and optionally the user who cancelled.
 /// </summary>
 public class ResetRequest
 {
 /// <summary>
 /// The database identifier of the generated coupon (optional).
 /// </summary>
 public int? Id { get; set; }

 /// <summary>
 /// The generated coupon code (optional if Id is provided).
 /// </summary>
 public string? Code { get; set; }

 /// <summary>
 /// The username or identifier of the user who cancelled the redemption (optional).
 /// </summary>
 public string? CancelledBy { get; set; }
 }

 /// <summary>
 /// Request model for setting complimentary flag
 /// </summary>
 public class SetComplimentaryRequest
 {
 /// <summary>
 /// Generated coupon code
 /// </summary>
 public string? Code { get; set; }
 /// <summary>
 /// Whether to mark as complimentary
 /// </summary>
 public bool IsComplimentary { get; set; }
 }

    /// <summary>
    /// Reset a generated coupon.
    /// </summary>
    /// <param name="req">Reset request containing Id or Code of the coupon</param>
    /// <returns>Result of the reset operation</returns>
    [HttpPost("reset")]
    public async Task<IActionResult> ResetGeneratedCoupon([FromBody] ResetRequest req)
    {
        try
        {
            if (req == null || (req.Id == null && string.IsNullOrWhiteSpace(req.Code)))
                return BadRequest(new { message = "Id or Code is required" });

            var ok = await _generatedCouponService.ResetGeneratedCouponAsync(req.Id, req.Code, req.CancelledBy);
            if (!ok) return NotFound(new { message = "Coupon not found" });

            return Ok(new { result = "Success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting generated coupon: {@Request}", req);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }

    /// <summary>
    /// Set or unset complimentary flag for a generated coupon.
    /// </summary>
    [HttpPost("set-complimentary")]
    public async Task<IActionResult> SetComplimentary([FromBody] SetComplimentaryRequest req)
    {
        try
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Code))
                return BadRequest(new { message = "Code is required" });

            var ok = await _generatedCouponService.SetComplimentaryAsync(req.Code.Trim(), req.IsComplimentary);
            if (!ok) return NotFound(new { message = "Coupon not found" });
            return Ok(new { result = "Success" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting complimentary flag for {@Request}", req);
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }
    }
}
