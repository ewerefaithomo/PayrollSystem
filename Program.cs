using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PayrollSystem.Data;
using Rotativa.AspNetCore;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSession();

// Add DB connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=PayrollDB;Trusted_Connection=True;TrustServerCertificate=True;"));

var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ROTATIVA CONFIGURATION
RotativaConfiguration.Setup(
    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
    "Rotativa"
);

app.Run();