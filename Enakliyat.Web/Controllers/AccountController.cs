using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Models;
using Enakliyat.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Enakliyat.Web.Controllers;

public class AccountController : Controller
{
    private readonly EnakliyatDbContext _context;

    public AccountController(EnakliyatDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return RedirectToAction("Login");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        var vm = new UserProfileViewModel
        {
            Name = user.Name,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(UserProfileViewModel model)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return RedirectToAction("Login");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        user.Name = model.Name ?? string.Empty;
        user.PhoneNumber = model.PhoneNumber ?? string.Empty;
        user.Email = model.Email ?? user.Email;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Profiliniz güncellendi.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !PasswordHasher.Verify(password, user.Password))
        {
            ModelState.AddModelError(string.Empty, "Geçersiz e-posta veya şifre.");
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim("UserId", user.Id.ToString()),
            new Claim("IsAdmin", user.IsAdmin.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(string email, string password)
    {
        var exists = await _context.Users.AnyAsync(u => u.Email == email);
        if (exists)
        {
            ModelState.AddModelError(string.Empty, "Bu e-posta ile kayıtlı kullanıcı zaten var.");
            return View();
        }

        var user = new User
        {
            Email = email,
            Password = PasswordHasher.Hash(password)
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult GoogleLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action("GoogleCallback", "Account", new { returnUrl });
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return Challenge(properties, "Google");
    }

    [HttpGet]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = null)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Google hesabınızdan e-posta bilgisi alınamadı.";
            return RedirectToAction("Login");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User
            {
                Email = email,
                Password = PasswordHasher.Hash(Guid.NewGuid().ToString())
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim("UserId", user.Id.ToString()),
            new Claim("IsAdmin", user.IsAdmin.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
