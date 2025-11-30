using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Claims;

namespace Enakliyat.Web.Controllers;

public class CarrierAccountController : Controller
{
    private readonly EnakliyatDbContext _context;
    private readonly IWebHostEnvironment _env;

    public CarrierAccountController(EnakliyatDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "E-posta ve şifre zorunludur.");
            return View();
        }

        var user = _context.CarrierUsers.FirstOrDefault(u => u.Email == email);
        if (user == null || !PasswordHasher.Verify(password, user.Password))
        {
            ModelState.AddModelError(string.Empty, "Geçersiz bilgiler.");
            return View();
        }

        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == user.CarrierId);
        if (carrier == null || !carrier.IsApproved || carrier.IsRejected || carrier.IsSuspended)
        {
            ModelState.AddModelError(string.Empty, "Başvurunuz henüz onaylanmamış, reddedilmiş veya hesabınız askıya alınmıştır.");
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim("CarrierUserId", user.Id.ToString()),
            new Claim("CarrierId", user.CarrierId.ToString()),
            new Claim("IsCarrier", "True")
        };

        var identity = new ClaimsIdentity(claims, "CarrierAuth");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("CarrierAuth", principal, new AuthenticationProperties
        {
            IsPersistent = true
        });

        return RedirectToAction("Offers", "Carrier");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string companyName, string name, string phoneNumber, string email, string password, string? licenseNumber, string? vehicleInfo, string? serviceAreas, string? description, IFormFile[]? documents)
    {
        if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "Tüm zorunlu alanları doldurun.");
            return View();
        }

        var emailExists = await _context.CarrierUsers.AnyAsync(u => u.Email == email);
        if (emailExists)
        {
            ModelState.AddModelError(string.Empty, "Bu e-posta ile kayıtlı nakliyeci hesabı zaten var.");
            return View();
        }

        var carrier = new Carrier
        {
            Name = name,
            CompanyName = companyName,
            PhoneNumber = phoneNumber,
            Email = email,
            LicenseNumber = licenseNumber,
            VehicleInfo = vehicleInfo,
            ServiceAreas = serviceAreas,
            Description = description,
            IsApproved = false,
            IsRejected = false,
            IsSuspended = false
        };

        await _context.Carriers.AddAsync(carrier);
        await _context.SaveChangesAsync();

        var carrierUser = new CarrierUser
        {
            Email = email,
            Password = PasswordHasher.Hash(password),
            CarrierId = carrier.Id
        };

        await _context.CarrierUsers.AddAsync(carrierUser);
        await _context.SaveChangesAsync();

        if (documents != null && documents.Length > 0)
        {
            var uploadRoot = Path.Combine(_env.WebRootPath, "uploads", "carriers", carrier.Id.ToString());
            Directory.CreateDirectory(uploadRoot);

            foreach (var file in documents)
            {
                if (file == null || file.Length == 0) continue;

                var safeName = Path.GetFileNameWithoutExtension(file.FileName);
                var ext = Path.GetExtension(file.FileName);
                var uniqueName = $"{safeName}_{Guid.NewGuid():N}{ext}";
                var physicalPath = Path.Combine(uploadRoot, uniqueName);

                using (var stream = new FileStream(physicalPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = $"/uploads/carriers/{carrier.Id}/{uniqueName}";
                var doc = new CarrierDocument
                {
                    CarrierId = carrier.Id,
                    FilePath = relativePath,
                    DocumentType = "Genel Belge",
                    IsApproved = false
                };

                await _context.CarrierDocuments.AddAsync(doc);
            }

            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Başvurunuz alınmıştır. Onaylandıktan sonra giriş yapabilirsiniz.";
        return RedirectToAction("Login");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("CarrierAuth");
        return RedirectToAction("Login");
    }
}
