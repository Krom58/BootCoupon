using Microsoft.AspNetCore.Mvc;
using CouponManagement.Shared.Services;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using CouponManagement.Shared;

namespace CouponManagement.Web.Controllers
{
    /// <summary>
    /// Controller for coupon definitions operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CouponDefinitionsController : ControllerBase
    {
        /// <summary>
        /// Get all coupon definitions (for dropdown population).
        /// Returns a simplified list with Code and Name only.
        /// </summary>
        /// <returns>List of coupon definitions.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var service = new CouponDefinitionService();
            
            try
            {
                var definitions = await service.GetAllAsync();
     
                // Return simplified model for dropdown
                var result = definitions.Select(d => new
                {
                    id = d.Id,
                    code = d.Code,
                    name = d.Name
                }).ToList();
     
                return Ok(result);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving coupon definitions", error = ex.Message });
            }
        }

        /// <summary>
        /// Get coupon definition details by code.
        /// Returns full details including price, description, and validity dates.
        /// </summary>
        /// <param name="code">Coupon definition code.</param>
        /// <returns>Detailed coupon definition information.</returns>
        [HttpGet("bycode/{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            try
            {
                using var context = new CouponContext();
   
                var definition = await context.CouponDefinitions
                    .Include(d => d.CouponType)
                    .Include(d => d.CodeGenerator)
                    .FirstOrDefaultAsync(d => d.Code == code);

                if (definition == null)
                {
                    return NotFound(new { message = $"Coupon definition with code '{code}' not found" });
                }

                // Parse parameters to get description
                string description = "";
                int expiryDays = 0;
     
                try
                {
                    if (!string.IsNullOrEmpty(definition.Params))
                    {
                        var paramsObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(definition.Params);
            
                        if (paramsObj.TryGetProperty("description", out var descProp))
                        {
                            description = descProp.GetString() ?? "";
                        }
   
                        if (paramsObj.TryGetProperty("expiryDays", out var expiryProp))
                        {
                            expiryDays = expiryProp.GetInt32();
                        }
                    }
                }
                catch { }

                var result = new
                {
                    id = definition.Id,
                    code = definition.Code,
                    name = definition.Name,
                    price = definition.Price,
                    description = description,
                    couponType = definition.CouponType?.Name ?? "",
                    validFrom = definition.ValidFrom,
                    validTo = definition.ValidTo,
                    expiryDays = expiryDays,
                    isActive = definition.IsActive,
                    isLimited = definition.IsLimited,
                    createdAt = definition.CreatedAt,
                    createdBy = definition.CreatedBy,
                    statusText = definition.StatusText,
                    isExpired = definition.IsExpired,
                    isCurrentlyValid = definition.IsCurrentlyValid
                };

                return Ok(result);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving coupon definition", error = ex.Message });
            }
        }
    }
}
