using Enakliyat.Infrastructure;
using Enakliyat.Web;
using Enakliyat.Web.Models;
using Enakliyat.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Serilog yapılandırması
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/startup-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("=== Uygulama başlatılıyor ===");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog'u builder'a ekle
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/app-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"));

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("EnakliyatDb");

builder.Services.AddDbContext<EnakliyatDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<SmsSettings>(builder.Configuration.GetSection("Sms"));
builder.Services.AddHttpClient<ISmsService, IletimXSmsService>();
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

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

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
       Log.Information("Database migration ve seed işlemleri başlatılıyor...");
       using (var scope = app.Services.CreateScope())
       {
           var context = scope.ServiceProvider.GetRequiredService<EnakliyatDbContext>();
           var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
           var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

           try
           {
               Log.Information("Database migration çalıştırılıyor...");
               context.Database.Migrate();
               Log.Information("Database migration tamamlandı.");
           }
           catch (Exception ex)
           {
               Log.Error(ex, "Database migration hatası!");
               logger.LogError(ex, "Database migration hatası!");
               throw;
           }

           try
           {
               Log.Information("Admin user seed işlemi başlatılıyor...");
               await DataSeeder.SeedAdminUserAsync(context);
               Log.Information("Admin user seed tamamlandı.");
           }
           catch (Exception ex)
           {
               Log.Error(ex, "Admin user seed hatası!");
               logger.LogError(ex, "Admin user seed hatası!");
           }

           try
           {
               Log.Information("Address seed işlemi başlatılıyor...");
               await DataSeeder.SeedAddressesAsync(context, env);
               Log.Information("Address seed tamamlandı.");
           }
           catch (Exception ex)
           {
               Log.Error(ex, "Address seed hatası!");
               logger.LogError(ex, "Address seed hatası!");
           }

           try
           {
               Log.Information("System settings seed işlemi başlatılıyor...");
               await DataSeeder.SeedSystemSettingsAsync(context);
               Log.Information("System settings seed tamamlandı.");
           }
           catch (Exception ex)
           {
               Log.Error(ex, "System settings seed hatası!");
               logger.LogError(ex, "System settings seed hatası!");
           }

           try
           {
               Log.Information("Varsayılan ek hizmetler seed işlemi başlatılıyor...");
               await DataSeeder.SeedDefaultAddOnServicesAsync(context);
               Log.Information("Varsayılan ek hizmetler seed tamamlandı.");
           }
           catch (Exception ex)
           {
               Log.Error(ex, "Varsayılan ek hizmetler seed hatası!");
               logger.LogError(ex, "Varsayılan ek hizmetler seed hatası!");
           }

           try
           {
               Log.Information("Notification templates seed işlemi başlatılıyor...");
               await DataSeeder.SeedNotificationTemplatesAsync(context);
               Log.Information("Notification templates seed tamamlandı.");
           }
           catch (Exception ex)
           {
               Log.Error(ex, "Notification templates seed hatası!");
               logger.LogError(ex, "Notification templates seed hatası!");
           }

           try
           {
               Log.Information("Fake data seed işlemi başlatılıyor...");
               await DataSeeder.SeedFakeDataAsync(context);
               Log.Information("Fake data seed tamamlandı.");
           }
           catch (Exception ex)
           {
               Log.Error(ex, "Fake data seed hatası!");
               logger.LogError(ex, "Fake data seed hatası!");
           }
       }
       Log.Information("Tüm seed işlemleri tamamlandı.");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

//app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
    //.WithStaticAssets();

Log.Information("=== Uygulama başarıyla başlatıldı ===");

app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "=== Uygulama başlatılamadı! ===");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
