﻿using System;
using System.IO;
using System.Reflection;
using CouponManagement.Shared;
using CouponManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// DbContext - read connection string from appsettings.json
builder.Services.AddDbContext<CouponContext>(options =>
 options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register application services
builder.Services.AddScoped<GeneratedCouponService>();

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
 bool isProtectedApi = path.StartsWith("/api/CouponRedemption", StringComparison.OrdinalIgnoreCase);

 if (isProtectedStatic || isProtectedApi)
 {
 var hasCookie = context.Request.Cookies.ContainsKey("pos_user");
 if (!hasCookie)
 {
 if (isProtectedApi)
 {
 context.Response.StatusCode =401; // Unauthorized for API
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

// Serve static files from wwwroot (redeem.html)
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();