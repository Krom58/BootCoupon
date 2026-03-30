using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CouponManagement.Shared;
using CouponManagement.Shared.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

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
        private readonly ILogger<AuthController> _logger;
        private readonly bool _enableLoginLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="configuration">Configuration to read feature flags/settings.</param>
        public AuthController(CouponContext context, ILogger<AuthController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            // Default to false to disable DB login logging unless explicitly enabled in configuration
            _enableLoginLog = configuration?.GetValue<bool>("App:EnableLoginLog", false) ?? false;
        }

        /// <summary>
        /// Attempts to authenticate a user using the provided username and password.
        /// </summary>
        /// <param name="username">The username submitted by the client.</param>
        /// <param name="password">The password submitted by the client.</param>
        /// <returns>An <see cref="IActionResult"/> containing200 OK with user info when successful,400 for bad request, or401 for invalid credentials.</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return BadRequest("username and password are required");

            var user = await _context.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.Username == username && u.Password == password && u.IsActive);
            if (user == null)
            {
                // Log failed login attempt
                await LogLoginAttempt(username, "FailedLogin");
                return Unauthorized("Invalid credentials");
            }

            // Simple session cookie (no claims) - set a cookie to indicate logged in
            Response.Cookies.Append("pos_user", user.Username, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, IsEssential = true });

            // Log successful login
            await LogLoginAttempt(user.Username, "Login");
            return Ok(new { username = user.Username, displayName = user.DisplayName });
        }

        /// <summary>
        /// Logs out the current user by deleting the authentication cookie.
        /// </summary>
        /// <returns>An <see cref="IActionResult"/> containing200 OK.</returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var username = Request.Cookies.ContainsKey("pos_user") ? Request.Cookies["pos_user"] : "(unknown)";
            Response.Cookies.Delete("pos_user");

            // Log logout
            await LogLoginAttempt(username, "Logout");
            return Ok();
        }

        private string GetClientIp()
        {
            try
            {
                // Prefer X-Forwarded-For if present (may contain comma-separated list)
                if (Request?.Headers != null && Request.Headers.TryGetValue("X-Forwarded-For", out var fwd) && !string.IsNullOrWhiteSpace(fwd))
                {
                    var first = fwd.ToString().Split(',').Select(s => s.Trim()).FirstOrDefault();
                    if (!string.IsNullOrEmpty(first))
                    {
                        if (first == "::1") return "127.0.0.1";
                        return first;
                    }
                }

                var remote = HttpContext?.Connection?.RemoteIpAddress?.ToString();
                if (remote == "::1") return "127.0.0.1";
                return remote ?? "(unknown)";
            }
            catch
            {
                return "(unknown)";
            }
        }

        private async Task LogLoginAttempt(string? username, string action)
        {
            try
            {
                // If DB login logging is disabled via configuration, skip writing to DB.
                if (!_enableLoginLog)
                {
                    _logger.LogInformation("Login logging disabled - would have logged: {User} {Action}", username, action);
                    return;
                }

                var ip = GetClientIp();

                var log = new LoginLog
                {
                    UserName = username ?? string.Empty,
                    Action = action,
                    Location = ip,
                    App = Request?.Headers.ContainsKey("User-Agent") == true ? Request.Headers["User-Agent"].ToString() : "Web"
                };

                _context.LoginLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "LogLoginAttempt failed for user {User} action {Action}", username, action);
            }
        }
    }
}