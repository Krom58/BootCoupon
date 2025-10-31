using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CouponManagement.Shared.Services;
using System;
using Microsoft.Extensions.Logging;

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
    }
}
