using Enakliyat.Infrastructure;
using Enakliyat.Web;
using Enakliyat.Web.Models;
using Enakliyat.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("EnakliyatDb");

builder.Services.AddDbContext<EnakliyatDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IReservationNotificationService, SmtpReservationNotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    })
    .AddCookie("CarrierAuth", options =>
    {
        options.LoginPath = "/CarrierAccount/Login";
        options.Cookie.Name = "CarrierAuthCookie";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    })
    .AddGoogle("Google", options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Status code pages
app.UseStatusCodePagesWithReExecute("/Error/{0}");

// Global exception handling
app.UseMiddleware<Enakliyat.Web.Middleware.GlobalExceptionHandlerMiddleware>();

       // Seed default admin user and apply migrations
       using (var scope = app.Services.CreateScope())
       {
           var context = scope.ServiceProvider.GetRequiredService<EnakliyatDbContext>();
           context.Database.Migrate();
           var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
           await DataSeeder.SeedAdminUserAsync(context);
           await DataSeeder.SeedAddressesAsync(context, env);
           await DataSeeder.SeedSystemSettingsAsync(context);
           await DataSeeder.SeedNotificationTemplatesAsync(context);
           await DataSeeder.SeedFakeDataAsync(context);
       }

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

//app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
    //.WithStaticAssets();


app.Run();
