using Microsoft.AspNetCore.Mvc;
using CouponManagement.Shared.Services;
using System.Threading.Tasks;
using System.Linq;

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
    }
}
