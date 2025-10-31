using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace CouponManagement.Web.Controllers
{
    /// <summary>
    /// Controller for handling authentication-related actions such as login and logout.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly CouponContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public AuthController(CouponContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Attempts to authenticate a user using the provided username and password.
        /// </summary>
        /// <param name="username">The username submitted by the client.</param>
        /// <param name="password">The password submitted by the client.</param>
        /// <returns>An <see cref="IActionResult"/> containing 200 OK with user info when successful, 400 for bad request, or 401 for invalid credentials.</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return BadRequest("username and password are required");

            var user = await _context.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.Username == username && u.Password == password && u.IsActive);
            if (user == null) return Unauthorized("Invalid credentials");

            // Simple session cookie (no claims) - set a cookie to indicate logged in
            Response.Cookies.Append("pos_user", user.Username, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, IsEssential = true });
            return Ok(new { username = user.Username, displayName = user.DisplayName });
        }

        /// <summary>
        /// Logs out the current user by deleting the authentication cookie.
        /// </summary>
        /// <returns>An <see cref="IActionResult"/> containing 200 OK.</returns>
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("pos_user");
            return Ok();
        }
    }
}