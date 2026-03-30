using System;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using CouponManagement.Shared;
using CouponManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
      // รองรับภาษาไทยและภาษาอื่นๆ ในการ encode JSON
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        options.JsonSerializerOptions.WriteIndented = false;
    });

// เพิ่ม CORS สำหรับ development
builder.Services.AddCors(options =>
{
 options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
      .AllowAnyMethod()
    .AllowAnyHeader();
    });
});

// DbContext - read connection string from appsettings.json
builder.Services.AddDbContext<CouponContext>(options =>
 options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register application services
builder.Services.AddScoped<GeneratedCouponService>();

// add forwarded headers options
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
 options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
 // optionally limit known networks/proxies here
});

// Swagger + include XML comments
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
 var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
 var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
 if (File.Exists(xmlPath))
 {
 c.IncludeXmlComments(xmlPath);
 }
});

var app = builder.Build();

// Simple auth middleware: require cookie 'pos_user' for redeem.html and CouponRedemption APIs
app.Use(async (context, next) =>
{
 var path = context.Request.Path.Value ?? string.Empty;

 bool isProtectedStatic = path.Equals("/redeem.html", StringComparison.OrdinalIgnoreCase);
 // Protect CouponRedemption APIs except export endpoints (allow exports without pos_user cookie)
 bool isProtectedApi = path.StartsWith("/api/CouponRedemption", StringComparison.OrdinalIgnoreCase)
 && !path.StartsWith("/api/CouponRedemption/export", StringComparison.OrdinalIgnoreCase);

 if (isProtectedStatic || isProtectedApi)
 {
 var hasCookie = context.Request.Cookies.ContainsKey("pos_user");
 if (!hasCookie)
 {
 if (isProtectedApi)
 {
 context.Response.StatusCode =401; // Unauthorized for API
 context.Response.ContentType = "text/plain; charset=utf-8";
 await context.Response.WriteAsync("Unauthorized");
 return;
 }
 else
 {
 context.Response.Redirect("/login.html");
 return;
 }
 }
 }

 await next();
});

// Middleware
if (app.Environment.IsDevelopment())
{
 app.UseSwagger();
 app.UseSwaggerUI();
}

// Use forwarded headers (when behind a proxy)
app.UseForwardedHeaders();

// Use CORS
app.UseCors();

// Serve static files from wwwroot (redeem.html)
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();