using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CouponManagement.Shared;

namespace CouponManagement.Web.Controllers
{
    /// <summary>
    /// Controller for managing sale event-related operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SaleEventsController : ControllerBase
    {
        /// <summary>
        /// Retrieves all sale events, ordered by name.
        /// </summary>
        /// <returns>A list of sale events with their IDs and names.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            using var context = new CouponContext();
            var events = await context.SaleEvents
                .OrderBy(e => e.Name)
                .Select(e => new { id = e.Id, name = e.Name })
                .ToListAsync();
            return Ok(events);
        }
    }
}
