using ITSystem.Models;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Habilitar el servicio de almacenamiento en memoria para sesiones
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60); // Tiempo que durará la sesión inactiva
    options.Cookie.HttpOnly = true;                // Seguridad contra ataques XSS
    options.Cookie.IsEssential = true;              // Requerido para que funcione sin aceptar cookies
});

builder.Services.AddHttpContextAccessor(); // Permite acceder a la sesión desde las vistas

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseStaticFiles();

app.UseRouting();

// AGREGA ESTA LÍNEA AQUÍ (Obligatorio después de Routing y antes de Authorization/Endpoints)
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Tickets2}/{action=Index}/{id?}");

app.Run();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Tickets2}/{action=Index}/{id?}");

app.Run();
