using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CouponManagement.Shared;

namespace CouponManagement.Web.Controllers
{
    /// <summary>
    /// Controller for managing branch-related operations.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BranchesController : ControllerBase
    {
        /// <summary>
        /// Retrieves all branches, ordered by name.
        /// </summary>
        /// <returns>A list of branches with their IDs and names.</returns>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            using var context = new CouponContext();
            var branches = await context.Branches
                .OrderBy(b => b.Name)
                .Select(b => new { id = b.Id, name = b.Name })
                .ToListAsync();
            return Ok(branches);
        }
    }
}
