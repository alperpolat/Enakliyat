using System.Diagnostics;
using System.Globalization;
using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Helpers;
using Enakliyat.Web.Models;
using Enakliyat.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Enakliyat.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly EnakliyatDbContext _context;
    private readonly IReservationNotificationService _notificationService;
    private readonly INotificationService _generalNotificationService;
    private readonly IWebHostEnvironment _env;
    private readonly ISmsService _smsService;
    private readonly IOptions<SmsSettings> _smsSettings;
    private readonly IPublicSiteContact _publicSiteContact;

    public HomeController(
        ILogger<HomeController> logger,
        EnakliyatDbContext context,
        IReservationNotificationService notificationService,
        INotificationService generalNotificationService,
        IWebHostEnvironment env,
        ISmsService smsService,
        IOptions<SmsSettings> smsSettings,
        IPublicSiteContact publicSiteContact)
    {
        _logger = logger;
        _context = context;
        _notificationService = notificationService;
        _generalNotificationService = generalNotificationService;
        _env = env;
        _smsService = smsService;
        _smsSettings = smsSettings;
        _publicSiteContact = publicSiteContact;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var cities = _context.Cities
            .OrderBy(c => c.Name)
            .ToList();

        ViewBag.Cities = cities;

        ViewBag.QuoteCallTelHref = _publicSiteContact.QuoteCallTelHref;
        ViewBag.QuoteCallPhoneLabel = _publicSiteContact.PhoneDisplay;

        return View(new MoveRequestViewModel());
    }

    [HttpGet]
    public IActionResult Offers(int? id, string? moveType = null)
    {
        LoadOfferFormViewBag();

        var normalizedMoveType = NormalizeOfferMoveType(moveType);
        int? userId = TryGetCustomerUserId();

        if (!id.HasValue || id.Value <= 0)
        {
            var emptyVm = new OfferDetailsViewModel
            {
                MoveRequestId = 0,
                MoveType = normalizedMoveType,
                MoveDate = DateTime.Today.AddDays(1)
            };

            if (userId.HasValue)
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == userId.Value);
                if (user != null)
                {
                    emptyVm.CustomerName = user.Name ?? string.Empty;
                    emptyVm.PhoneNumber = user.PhoneNumber ?? string.Empty;
                    emptyVm.Email = user.Email;
                }
            }

            return View(emptyVm);
        }

        var request = _context.MoveRequests.FirstOrDefault(x => x.Id == id.Value);
        if (request == null)
        {
            return NotFound();
        }

        if (userId.HasValue)
        {
            if (request.UserId != userId.Value)
            {
                return NotFound();
            }
        }
        else
        {
            if (request.UserId != null)
            {
                return Challenge();
            }

            var sessionOfferId = HttpContext.Session.GetInt32(AnonymousOfferSessionKey);
            if (!sessionOfferId.HasValue || sessionOfferId.Value != request.Id)
            {
                return NotFound();
            }
        }

        var vm = new OfferDetailsViewModel
        {
            MoveRequestId = request.Id,
            FromCityId = request.FromCityId,
            FromDistrictId = request.FromDistrictId,
            FromNeighborhoodId = request.FromNeighborhoodId,
            ToCityId = request.ToCityId,
            ToDistrictId = request.ToDistrictId,
            ToNeighborhoodId = request.ToNeighborhoodId,
            FromAddress = request.FromAddress,
            ToAddress = request.ToAddress,
            MoveType = request.MoveType,
            CustomerName = request.CustomerName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            MoveDate = request.MoveDate == default ? DateTime.Today.AddDays(1) : request.MoveDate,
            MoveDateEnd = request.MoveDateEnd,
            RoomType = request.RoomType,
            FromFloor = request.FromFloor,
            FromHasElevator = request.FromHasElevator,
            ToFloor = request.ToFloor,
            ToHasElevator = request.ToHasElevator,
            Notes = request.Notes
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Offers(OfferDetailsViewModel model)
    {
        LoadOfferFormViewBag();

        try
        {
            const int minFreeAddressChars = 10;
            var vehicleOnly = string.Equals(model.MoveType, "VehicleOnly", StringComparison.OrdinalIgnoreCase);
            var fromFree = (model.FromAddress ?? string.Empty).Trim();
            var toFree = (model.ToAddress ?? string.Empty).Trim();

            if (vehicleOnly)
            {
                var toOk = model.ToCityId.HasValue || toFree.Length >= minFreeAddressChars;
                if (!toOk)
                {
                    ModelState.AddModelError(nameof(model.ToAddress),
                        "Aracın geleceği yer için listeden seçim yapın veya en az 10 karakter adres yazın.");
                }
            }
            else
            {
                var fromOk = model.FromCityId.HasValue || fromFree.Length >= minFreeAddressChars;
                var toOk = model.ToCityId.HasValue || toFree.Length >= minFreeAddressChars;
                if (!fromOk)
                {
                    ModelState.AddModelError(nameof(model.FromAddress),
                        "Çıkış adresi için listeden seçim yapın veya en az 10 karakter yazın.");
                }

                if (!toOk)
                {
                    ModelState.AddModelError(nameof(model.ToAddress),
                        "Varış adresi için listeden seçim yapın veya en az 10 karakter yazın.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var claimsUserId = TryGetCustomerUserId();
            int effectiveUserId;
            if (claimsUserId.HasValue)
            {
                effectiveUserId = claimsUserId.Value;
            }
            else
            {
                var guestUserId = await EnsurePhoneGuestUserAsync(model.PhoneNumber, model.CustomerName, HttpContext.RequestAborted);
                if (!guestUserId.HasValue)
                {
                    ModelState.AddModelError(string.Empty, "Lütfen geçerli bir cep telefonu girin (örn. 05xx xxx xx xx).");
                    return View(model);
                }

                effectiveUserId = guestUserId.Value;
            }

            string fromAddress;
            string toAddress;
            if (vehicleOnly)
            {
                fromAddress = "Sadece araç — çıkış adresi belirtilmedi";
                toAddress = model.ToCityId.HasValue
                    ? BuildAddress(model.ToCityId, model.ToDistrictId, model.ToNeighborhoodId)
                    : toFree;
            }
            else
            {
                fromAddress = model.FromCityId.HasValue
                    ? BuildAddress(model.FromCityId, model.FromDistrictId, model.FromNeighborhoodId)
                    : fromFree;
                toAddress = model.ToCityId.HasValue
                    ? BuildAddress(model.ToCityId, model.ToDistrictId, model.ToNeighborhoodId)
                    : toFree;
            }

            MoveRequest request;
            var isNew = model.MoveRequestId <= 0;

            if (isNew)
            {
                request = new MoveRequest { UserId = effectiveUserId };
                await _context.MoveRequests.AddAsync(request);
            }
            else
            {
                var loaded = await _context.MoveRequests.FirstOrDefaultAsync(x => x.Id == model.MoveRequestId);
                if (loaded == null)
                {
                    TempData["Error"] = "Talep bulunamadı.";
                    return RedirectToAction(nameof(Offers), new { moveType = model.MoveType });
                }

                request = loaded;

                if (claimsUserId.HasValue)
                {
                    if (request.UserId != claimsUserId.Value)
                    {
                        TempData["Error"] = "Bu talebe erişim yetkiniz yok.";
                        return RedirectToAction(nameof(Requests));
                    }
                }
                else
                {
                    var sessionOfferId = HttpContext.Session.GetInt32(AnonymousOfferSessionKey);
                    if (!sessionOfferId.HasValue || sessionOfferId.Value != request.Id)
                    {
                        TempData["Error"] = "Talep bulunamadı.";
                        return RedirectToAction(nameof(Index));
                    }

                    if (request.UserId != null && request.UserId.Value != effectiveUserId)
                    {
                        TempData["Error"] = "Bu talebi düzenlemek için giriş yapın.";
                        return RedirectToAction("Login", "Account");
                    }
                }
            }

            request.FromAddress = fromAddress;
            request.ToAddress = toAddress;
            if (vehicleOnly)
            {
                request.FromCityId = null;
                request.FromDistrictId = null;
                request.FromNeighborhoodId = null;
                if (model.ToCityId.HasValue)
                {
                    request.ToCityId = model.ToCityId;
                    request.ToDistrictId = model.ToDistrictId;
                    request.ToNeighborhoodId = model.ToNeighborhoodId;
                }
                else
                {
                    request.ToCityId = null;
                    request.ToDistrictId = null;
                    request.ToNeighborhoodId = null;
                }
            }
            else
            {
                if (model.FromCityId.HasValue)
                {
                    request.FromCityId = model.FromCityId;
                    request.FromDistrictId = model.FromDistrictId;
                    request.FromNeighborhoodId = model.FromNeighborhoodId;
                }
                else
                {
                    request.FromCityId = null;
                    request.FromDistrictId = null;
                    request.FromNeighborhoodId = null;
                }

                if (model.ToCityId.HasValue)
                {
                    request.ToCityId = model.ToCityId;
                    request.ToDistrictId = model.ToDistrictId;
                    request.ToNeighborhoodId = model.ToNeighborhoodId;
                }
                else
                {
                    request.ToCityId = null;
                    request.ToDistrictId = null;
                    request.ToNeighborhoodId = null;
                }
            }
            request.CustomerName = model.CustomerName;
            request.PhoneNumber = model.PhoneNumber;
            request.Email = model.Email;
            var moveStart = model.MoveDate.Date;
            var moveEnd = model.MoveDateEnd?.Date;
            request.MoveDate = moveStart;
            request.MoveDateEnd = moveEnd.HasValue && moveEnd.Value > moveStart ? moveEnd : null;
            request.MoveType = string.IsNullOrWhiteSpace(model.MoveType) ? "Home" : model.MoveType;
            if (vehicleOnly)
            {
                request.RoomType = null;
                request.FromFloor = null;
                request.ToFloor = null;
                request.FromHasElevator = false;
                request.ToHasElevator = false;
            }
            else
            {
                request.RoomType = model.RoomType;
                request.FromFloor = model.FromFloor;
                request.FromHasElevator = model.FromHasElevator;
                request.ToFloor = model.ToFloor;
                request.ToHasElevator = model.ToHasElevator;
            }
            request.Notes = model.Notes;
            request.Status = "Teklif Bekliyor";
            request.UserId = effectiveUserId;

            EnsureTrackingToken(request);

            await _context.SaveChangesAsync();

            if (isNew && !claimsUserId.HasValue)
            {
                HttpContext.Session.SetInt32(AnonymousOfferSessionKey, request.Id);
            }

            var selectedIds = HttpContext.Request.Form["SelectedAddOnIds"].ToList();
            var intIds = selectedIds
                .Select(x => int.TryParse(x, out var addonId) ? addonId : (int?)null)
                .Where(addonId => addonId.HasValue)
                .Select(addonId => addonId!.Value)
                .ToList();

            var existingAddOnIds = await _context.AddOnServices
                .Where(a => intIds.Contains(a.Id))
                .Select(a => a.Id)
                .ToListAsync();

            var existingAddOns = _context.MoveRequestAddOns
                .Where(a => a.MoveRequestId == request.Id)
                .ToList();

            _context.MoveRequestAddOns.RemoveRange(existingAddOns);

            foreach (var addOnId in existingAddOnIds.Distinct())
            {
                var addOn = new MoveRequestAddOn
                {
                    MoveRequestId = request.Id,
                    AddOnServiceId = addOnId
                };
                await _context.MoveRequestAddOns.AddAsync(addOn);
            }

            await _context.SaveChangesAsync();

            if (model.Photos != null && model.Photos.Length > 0)
            {
                var uploadRoot = Path.Combine(_env.WebRootPath, "uploads", "requests", request.Id.ToString());
                Directory.CreateDirectory(uploadRoot);

                foreach (var file in model.Photos)
                {
                    if (file == null || file.Length == 0) continue;

                    var validationError = Enakliyat.Web.Helpers.FileUploadHelper.GetFileValidationError(file, isImage: true);
                    if (!string.IsNullOrEmpty(validationError))
                    {
                        _logger.LogWarning("Invalid file upload attempt: {Error}", validationError);
                        continue;
                    }

                    var uniqueName = Enakliyat.Web.Helpers.FileUploadHelper.GenerateSafeFileName(file.FileName);
                    var physicalPath = Path.Combine(uploadRoot, uniqueName);

                    await using (var stream = new FileStream(physicalPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var relativePath = $"/uploads/requests/{request.Id}/{uniqueName}";
                    var photo = new MoveRequestPhoto
                    {
                        MoveRequestId = request.Id,
                        FilePath = relativePath
                    };
                    await _context.MoveRequestPhotos.AddAsync(photo);
                }

                await _context.SaveChangesAsync();
            }

            var listeUrl = BuildPublicTalepTakipUserUrl(effectiveUserId);
            var smsGonderildi = false;
            if (!string.IsNullOrEmpty(listeUrl))
            {
                if (_smsSettings.Value.Enabled)
                {
                    var smsText = $"Road of Home: Talepleriniz: {listeUrl} Son talep No:{request.Id}";
                    var smsResult = await _smsService.SendAsync(model.PhoneNumber, smsText);
                    smsGonderildi = smsResult.Ok;
                    if (!smsResult.Ok)
                    {
                        _logger.LogWarning(
                            "Takip SMS gonderilemedi. Talep={Id}, Detay={Detail}. Kontrol: Sms:Enabled, IletimX ayarlari, kredi, 5xxxxxxxxx format, log.",
                            request.Id,
                            smsResult.Detail);
                    }
                }
                else
                {
                    _logger.LogInformation("Takip SMS atlandı: Sms:Enabled=false (appsettings'te true yapın).");
                }
            }
            else
            {
                _logger.LogWarning("Talep listesi URL üretilemedi (Sms:PublicBaseUrl veya Host).");
            }

            if (claimsUserId.HasValue && !isNew)
            {
                if (smsGonderildi)
                {
                    TempData["Success"] =
                        $"Talebiniz güncellendi (#{request.Id}). Liste linki SMS ile gönderildi. {listeUrl}";
                }
                else if (!string.IsNullOrEmpty(listeUrl))
                {
                    TempData["Success"] =
                        $"Talebiniz güncellendi (#{request.Id}). Talepleriniz: {listeUrl}";
                }
                else
                {
                    TempData["Success"] = "Talebiniz başarıyla güncellendi.";
                }

                return RedirectToAction(nameof(Requests));
            }

            var linkFragment = string.IsNullOrEmpty(listeUrl)
                ? string.Empty
                : $" Taleplerim bağlantısı: {listeUrl}";

            var hesapNotu = claimsUserId.HasValue
                ? " Giriş yaptığınız hesaptan da tüm taleplerinize ulaşabilirsiniz."
                : string.Empty;

            if (smsGonderildi)
            {
                TempData["Success"] =
                    "Talebiniz kaydedildi (No #" + request.Id + "). Liste linki cep telefonunuza SMS ile de gönderildi." + linkFragment +
                    " Aşağıda tüm taleplerinizi görebilirsiniz." + hesapNotu;
            }
            else if (_smsSettings.Value.Enabled)
            {
                TempData["Warning"] =
                    "Talebiniz kaydedildi (No #" + request.Id + "). SMS gönderilemedi (SMS ayarları veya numara)." + linkFragment +
                    hesapNotu;
            }
            else
            {
                TempData["Info"] =
                    "Talebiniz kaydedildi (No #" + request.Id + "). SMS şu an kapalı (Sms:Enabled)." + linkFragment +
                    hesapNotu;
            }

            return RedirectToAction(nameof(TalepTakip), new { t = effectiveUserId.ToString(CultureInfo.InvariantCulture) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving offer details for request {RequestId}", model.MoveRequestId);
            TempData["Error"] = "Talebiniz kaydedilirken bir hata oluştu. Lütfen tekrar deneyin.";
            LoadOfferFormViewBag();
            return View(model);
        }
    }

    /// <summary><paramref name="t"/> — kullanıcı Id (tüm talepler) veya eski gizli takip token'ı (tek talep).</summary>
    [HttpGet]
    public async Task<IActionResult> TalepTakip(string? t, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(t))
        {
            return NotFound();
        }

        var trimmed = t.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kullaniciId) &&
            kullaniciId > 0)
        {
            var musteri = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == kullaniciId && !u.IsAdmin, cancellationToken);
            if (musteri == null)
            {
                return NotFound();
            }

            var talepler = await _context.MoveRequests.AsNoTracking()
                .Where(m => m.UserId == kullaniciId)
                .OrderByDescending(m => m.Id)
                .Select(m => new MisafirTalepOzetItem
                {
                    Id = m.Id,
                    Status = m.Status,
                    FromAddress = m.FromAddress,
                    ToAddress = m.ToAddress,
                    MoveType = m.MoveType,
                    MoveDate = m.MoveDate,
                    MoveDateEnd = m.MoveDateEnd,
                    TeklifSayisi = _context.Offers.Count(o => o.MoveRequestId == m.Id)
                })
                .ToListAsync(cancellationToken);

            var listeVm = new MisafirTaleplerimViewModel
            {
                KullaniciId = kullaniciId,
                MusteriAdi = musteri.Name,
                PublicListeUrl = BuildPublicTalepTakipUserUrl(kullaniciId),
                Talepler = talepler
            };
            return View("MisafirTaleplerim", listeVm);
        }

        var req = await _context.MoveRequests.AsNoTracking()
            .FirstOrDefaultAsync(m => m.TrackingToken == trimmed, cancellationToken);
        if (req == null)
        {
            return NotFound();
        }

        if (req.UserId.HasValue)
        {
            return RedirectToAction(nameof(TalepTakip),
                new { t = req.UserId.Value.ToString(CultureInfo.InvariantCulture) });
        }

        var teklifSayisi = await _context.Offers.CountAsync(o => o.MoveRequestId == req.Id, cancellationToken);
        var trackingUrlForVm = BuildPublicTrackingUrl(trimmed);
        if (string.IsNullOrEmpty(trackingUrlForVm))
        {
            var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value! : string.Empty;
            trackingUrlForVm =
                $"{Request.Scheme}://{Request.Host.Value}{pathBase}/Home/TalepTakip?t={Uri.EscapeDataString(trimmed)}";
        }

        var vm = new TalepTakipViewModel
        {
            Id = req.Id,
            PublicTrackingUrl = trackingUrlForVm,
            MisafirKullaniciId = null,
            Status = req.Status,
            FromAddress = req.FromAddress,
            ToAddress = req.ToAddress,
            MoveType = req.MoveType,
            MoveDate = req.MoveDate,
            MoveDateEnd = req.MoveDateEnd,
            TeklifSayisi = teklifSayisi
        };
        return View(vm);
    }

    [HttpGet]
    public IActionResult MisafirTalepDetay(int id, int t) =>
        RedirectToAction(nameof(Details), new { id, t });

    private void EnsureTrackingToken(MoveRequest request)
    {
        if (!string.IsNullOrEmpty(request.TrackingToken))
        {
            return;
        }

        for (var attempt = 0; attempt < 12; attempt++)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            if (!_context.MoveRequests.Any(m => m.TrackingToken == token))
            {
                request.TrackingToken = token;
                return;
            }
        }

        throw new InvalidOperationException("Takip kodu üretilemedi.");
    }

    /// <summary>
    /// SMS / paylaşım için kök URL. Önce <c>Sms:PublicBaseUrl</c> (canlı alan adı sabitlemek veya reverse proxy için); boşsa mevcut isteğin hostu.
    /// </summary>
    private string? GetPublicSiteBaseUrl()
    {
        var configured = (_smsSettings.Value.PublicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        if (Request.Host.HasValue)
        {
            return $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
        }

        return null;
    }

    /// <summary>Takip sayfasının canlıda doğru görünmesi için önce <c>Sms:PublicBaseUrl</c>; yoksa isteğin şeması ve hostu.</summary>
    private string BuildPublicTrackingUrl(string trackingToken)
    {
        var configured = (_smsSettings.Value.PublicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
        {
            var path = Url.Action(nameof(TalepTakip), "Home", new { t = trackingToken });
            if (string.IsNullOrEmpty(path))
            {
                return configured;
            }

            return $"{configured}{path}";
        }

        var absolute = Url.Action(nameof(TalepTakip), "Home", new { t = trackingToken }, Request.Scheme, Request.Host.Value);
        if (!string.IsNullOrEmpty(absolute))
        {
            return absolute;
        }

        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value! : string.Empty;
        return $"{Request.Scheme}://{Request.Host.Value}{pathBase}/Home/TalepTakip?t={Uri.EscapeDataString(trackingToken)}";
    }

    /// <summary>Misafir talep listesi — <c>t</c> sorgu parametresi kullanıcı Id.</summary>
    private string BuildPublicTalepTakipUserUrl(int kullaniciId)
    {
        var t = kullaniciId.ToString(CultureInfo.InvariantCulture);
        var configured = (_smsSettings.Value.PublicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
        {
            var path = Url.Action(nameof(TalepTakip), "Home", new { t });
            if (string.IsNullOrEmpty(path))
            {
                return configured;
            }

            return $"{configured}{path}";
        }

        var absolute = Url.Action(nameof(TalepTakip), "Home", new { t }, Request.Scheme, Request.Host.Value);
        if (!string.IsNullOrEmpty(absolute))
        {
            return absolute;
        }

        var pathBase = Request.PathBase.HasValue ? Request.PathBase.Value! : string.Empty;
        return $"{Request.Scheme}://{Request.Host.Value}{pathBase}/Home/TalepTakip?t={Uri.EscapeDataString(t)}";
    }

    private async Task<int?> EnsurePhoneGuestUserAsync(string? phone, string? name, CancellationToken cancellationToken = default)
    {
        var normalized = PhoneNumberHelper.NormalizeTurkishMobile(phone);
        if (normalized == null)
        {
            return null;
        }

        var candidates = await _context.Users.AsNoTracking()
            .Where(u => !u.IsAdmin)
            .Select(u => new { u.Id, u.PhoneNumber })
            .ToListAsync(cancellationToken);

        foreach (var c in candidates)
        {
            if (PhoneNumberHelper.NormalizeTurkishMobile(c.PhoneNumber) == normalized)
            {
                return c.Id;
            }
        }

        var guestEmail = $"guest-{normalized}@guest.roadofhome.local";
        var byEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == guestEmail, cancellationToken);
        if (byEmail != null)
        {
            return byEmail.Id;
        }

        var guest = new User
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Misafir" : name.Trim(),
            PhoneNumber = normalized,
            Email = guestEmail,
            Password = PasswordHasher.Hash(Guid.NewGuid().ToString("N")),
            IsAdmin = false
        };
        await _context.Users.AddAsync(guest, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return guest.Id;
    }

    private void LoadOfferFormViewBag()
    {
        const string sadeceAracName = "Sadece Araç";
        var active = _context.AddOnServices
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToList();
        var sadeceArac = active.FirstOrDefault(s => s.Name == sadeceAracName);
        ViewBag.AddOnServices = sadeceArac == null
            ? active
            : active.Where(s => s.Id != sadeceArac.Id).ToList();
        ViewBag.SadeceAracAddOn = sadeceArac;
    }

    [Authorize]
    public IActionResult Requests()
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var requests = _context.MoveRequests
            .Where(x => x.UserId == userId && x.Status != "Taslak")
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        ViewBag.PendingOfferCount = requests.Count(r => r.Status == "Teklif Bekliyor");
        ViewBag.AcceptedReservationCount = requests.Count(r => r.Status == "Teklif Kabul Edildi");

        return View(requests);
    }

    [AllowAnonymous]
    public IActionResult Details(int id)
    {
        if (!TryResolveMoveRequestOwnerUserIdForRead(out var userId))
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Details), new { id, t = Request.Query["t"].ToString() }) });
        }

        var request = _context.MoveRequests.FirstOrDefault(x => x.Id == id && x.UserId == userId);
        if (request == null)
        {
            return NotFound();
        }

        var offers = _context.Offers
            .Include(o => o.Carrier)
            .Where(o => o.MoveRequestId == id)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        var photos = _context.MoveRequestPhotos
            .Where(p => p.MoveRequestId == id)
            .OrderBy(p => p.CreatedAt)
            .ToList();

        var secilenEkHizmetler = _context.MoveRequestAddOns
            .Where(m => m.MoveRequestId == id)
            .Join(_context.AddOnServices.AsNoTracking(), m => m.AddOnServiceId, s => s.Id, (m, s) => s.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        int? acceptedCarrierId = null;
        Review? existingReview = null;
        Payment? payment = null;

        if (request.AcceptedOfferId.HasValue)
        {
            var acceptedOffer = offers.FirstOrDefault(o => o.Id == request.AcceptedOfferId.Value);
            if (acceptedOffer != null)
            {
                acceptedCarrierId = acceptedOffer.CarrierId;
                existingReview = _context.Reviews
                    .FirstOrDefault(r => r.MoveRequestId == id && r.CarrierId == acceptedOffer.CarrierId && r.UserId == userId);

                var contract = _context.Contracts
                    .FirstOrDefault(c => c.MoveRequestId == request.Id && c.OfferId == acceptedOffer.Id);
                if (contract != null)
                {
                    payment = _context.Payments.FirstOrDefault(p => p.ContractId == contract.Id);
                }
            }
        }

        var canReview = request.Status == "Taşınma Tamamlandı" && acceptedCarrierId.HasValue && existingReview == null;
        var misafirId = TryGetCustomerUserId().HasValue ? null : (int?)userId;

        var vm = new UserRequestDetailsViewModel
        {
            MisafirKullaniciId = misafirId,
            SecilenEkHizmetler = secilenEkHizmetler,
            Request = request,
            Offers = offers,
            Photos = photos,
            CanReview = canReview,
            AcceptedCarrierId = acceptedCarrierId,
            ReviewRating = existingReview?.Rating,
            ReviewComment = existingReview?.Comment,
            Payment = payment
        };

        return View(vm);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(int id, string status)
    {
        if (!TryResolveMoveRequestOwnerUserIdForWrite(out var userId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (request == null)
        {
            return NotFound();
        }

        request.Status = status;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Durum güncellendi.";
        return TryGetCustomerUserId().HasValue
            ? RedirectToAction(nameof(Details), new { id })
            : RedirectToAction(nameof(Details), new { id, t = userId });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptOffer(int offerId, int moveRequestId)
    {
        if (!TryResolveMoveRequestOwnerUserIdForWrite(out var userId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests
            .FirstOrDefaultAsync(r => r.Id == moveRequestId && r.UserId == userId);
        if (request == null)
        {
            return NotFound();
        }

        var offers = await _context.Offers
            .Where(o => o.MoveRequestId == moveRequestId)
            .ToListAsync();

        var selected = offers.FirstOrDefault(o => o.Id == offerId);
        if (selected == null)
        {
            return NotFound();
        }

        foreach (var offer in offers)
        {
            if (offer.Id == offerId)
            {
                offer.Status = "Kabul Edildi";
            }
            else if (offer.Status == "Beklemede")
            {
                offer.Status = "Reddedildi";
            }
        }

        request.Status = "Teklif Kabul Edildi";
        request.AcceptedOfferId = offerId;
        await _context.SaveChangesAsync();

        // Create contract if not exists
        var existingContract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.MoveRequestId == request.Id && c.OfferId == offerId);

        if (existingContract == null)
        {
            var contractNumber = $"ET-{DateTime.UtcNow:yyyy}-{request.Id}";

            var contract = new Contract
            {
                MoveRequestId = request.Id,
                OfferId = offerId,
                ContractNumber = contractNumber,
                IsInsuranceIncluded = false
            };

            await _context.Contracts.AddAsync(contract);
            await _context.SaveChangesAsync();
        }

        // Bildirim gönder
        var acceptedOffer = await _context.Offers
            .Include(o => o.Carrier)
            .FirstOrDefaultAsync(o => o.Id == offerId);
        
        if (acceptedOffer != null)
        {
            await _generalNotificationService.NotifyOfferAcceptedToCarrierAsync(acceptedOffer);
        }

        TempData["Success"] = "Seçtiğiniz teklif kabul edildi.";
        return TryGetCustomerUserId().HasValue
            ? RedirectToAction(nameof(Reservation), new { id = moveRequestId })
            : RedirectToAction(nameof(Reservation), new { id = moveRequestId, t = userId });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Reservation(int id)
    {
        if (!TryResolveMoveRequestOwnerUserIdForRead(out var userId))
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Reservation), new { id, t = Request.Query["t"].ToString() }) });
        }

        var request = await _context.MoveRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
        if (request == null)
        {
            return NotFound();
        }

        Offer? acceptedOffer = null;
        Contract? contract = null;
        Payment? payment = null;
        if (request.AcceptedOfferId.HasValue)
        {
            acceptedOffer = await _context.Offers
                .Include(o => o.Carrier)
                .FirstOrDefaultAsync(o => o.Id == request.AcceptedOfferId.Value);

            if (acceptedOffer != null)
            {
                contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.MoveRequestId == request.Id && c.OfferId == acceptedOffer.Id);
                if (contract != null)
                {
                    payment = await _context.Payments.FirstOrDefaultAsync(p => p.ContractId == contract.Id);
                }
            }
        }

        var vm = new ReservationViewModel
        {
            MisafirKullaniciId = TryGetCustomerUserId().HasValue ? null : userId,
            Request = request,
            AcceptedOffer = acceptedOffer,
            Contract = contract,
            Payment = payment
        };

        // E-posta bildirimi (konfigürasyon varsa çalışacak).
        _ = _notificationService.SendReservationConfirmationAsync(request, acceptedOffer);

        return View(vm);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int moveRequestId, string cardHolder, string cardNumber, string expiry, string cvv)
    {
        if (!TryResolveMoveRequestOwnerUserIdForWrite(out var userId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests
            .FirstOrDefaultAsync(r => r.Id == moveRequestId && r.UserId == userId);
        if (request == null || !request.AcceptedOfferId.HasValue)
        {
            return NotFound();
        }

        var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == request.AcceptedOfferId.Value);
        if (offer == null)
        {
            return NotFound();
        }

        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.MoveRequestId == request.Id && c.OfferId == offer.Id);
        if (contract == null)
        {
            return NotFound();
        }

        var existingPayment = await _context.Payments.FirstOrDefaultAsync(p => p.ContractId == contract.Id);
        if (existingPayment != null && existingPayment.Status == PaymentStatus.Paid)
        {
            TempData["Success"] = "Ödeme zaten tamamlanmış.";
            return TryGetCustomerUserId().HasValue
                ? RedirectToAction(nameof(Reservation), new { id = moveRequestId })
                : RedirectToAction(nameof(Reservation), new { id = moveRequestId, t = userId });
        }

        // Fake validation; gerçek sistemde kart doğrulaması yapılmalı.
        var depositAmount = Math.Round(offer.Price * 0.10m, 2);

        var payment = existingPayment ?? new Payment
        {
            ContractId = contract.Id,
            Amount = depositAmount,
            Currency = "TRY",
            Method = PaymentMethod.Card,
        };

        payment.Status = PaymentStatus.Paid;
        payment.ExternalReference = $"FAKE-{DateTime.UtcNow:yyyyMMddHHmmss}";

        if (existingPayment == null)
        {
            await _context.Payments.AddAsync(payment);
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Ödeme başarıyla alındı (test).";
        return TryGetCustomerUserId().HasValue
            ? RedirectToAction(nameof(Reservation), new { id = moveRequestId })
            : RedirectToAction(nameof(Reservation), new { id = moveRequestId, t = userId });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Payments()
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var items = await _context.Payments
            .Include(p => p.Contract)
            .ThenInclude(c => c.Offer)
            .ThenInclude(o => o.MoveRequest)
            .Where(p => p.Contract.Offer.MoveRequest.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(items);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> PaymentReceipt(int id)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var payment = await _context.Payments
            .Include(p => p.Contract)
                .ThenInclude(c => c.Offer)
                    .ThenInclude(o => o.MoveRequest)
            .FirstOrDefaultAsync(p => p.Id == id && p.Contract.Offer.MoveRequest.UserId == userId);

        if (payment == null)
        {
            return NotFound();
        }

        return View(payment);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet]
    public async Task<IActionResult> CarrierProfile(int id)
    {
        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == id);
        if (carrier == null)
        {
            return NotFound();
        }

        var reviews = await _context.Reviews
            .Include(r => r.User)
            .Where(r => r.CarrierId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var vm = new HomeCarrierProfileViewModel
        {
            Carrier = carrier,
            Reviews = reviews
        };

        return View(vm);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(ReviewViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return TryGetCustomerUserId().HasValue
                ? RedirectToAction(nameof(Details), new { id = model.MoveRequestId })
                : RedirectToAction(nameof(Details), new { id = model.MoveRequestId, t = Request.Form["t"].ToString() });
        }

        if (!TryResolveMoveRequestOwnerUserIdForWrite(out var userId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests.FirstOrDefaultAsync(r => r.Id == model.MoveRequestId && r.UserId == userId);
        if (request == null)
        {
            return NotFound();
        }

        if (request.Status != "Taşınma Tamamlandı" || !request.AcceptedOfferId.HasValue)
        {
            return BadRequest();
        }

        var acceptedOffer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == request.AcceptedOfferId.Value);
        if (acceptedOffer == null || acceptedOffer.CarrierId != model.CarrierId)
        {
            return BadRequest();
        }

        var existing = await _context.Reviews
            .FirstOrDefaultAsync(r => r.MoveRequestId == model.MoveRequestId && r.CarrierId == model.CarrierId && r.UserId == userId);

        if (existing == null)
        {
            var review = new Review
            {
                MoveRequestId = model.MoveRequestId,
                CarrierId = model.CarrierId,
                UserId = userId,
                Rating = model.Rating,
                Comment = model.Comment
            };
            await _context.Reviews.AddAsync(review);
        }
        else
        {
            existing.Rating = model.Rating;
            existing.Comment = model.Comment;
        }

        await _context.SaveChangesAsync();

        // Update carrier rating
        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == model.CarrierId);
        if (carrier != null)
        {
            var reviews = await _context.Reviews
                .Where(r => r.CarrierId == model.CarrierId)
                .ToListAsync();

            carrier.ReviewCount = reviews.Count;
            carrier.AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0;

            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Değerlendirmeniz kaydedildi.";
        return TryGetCustomerUserId().HasValue
            ? RedirectToAction(nameof(Details), new { id = model.MoveRequestId })
            : RedirectToAction(nameof(Details), new { id = model.MoveRequestId, t = userId });
    }

    private const string AnonymousOfferSessionKey = "AnonymousOfferId";

    /// <summary>Girişli müşteri claim'i veya <c>?t=kullanıcıId</c> (talep sahibi).</summary>
    private bool TryResolveMoveRequestOwnerUserIdForRead(out int userId)
    {
        var claim = TryGetCustomerUserId();
        if (claim.HasValue)
        {
            userId = claim.Value;
            return true;
        }

        var t = Request.Query["t"].ToString();
        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) && uid > 0)
        {
            userId = uid;
            return true;
        }

        userId = 0;
        return false;
    }

    private bool TryResolveMoveRequestOwnerUserIdForWrite(out int userId)
    {
        var claim = TryGetCustomerUserId();
        if (claim.HasValue)
        {
            userId = claim.Value;
            return true;
        }

        var t = Request.Form["t"].ToString();
        if (string.IsNullOrEmpty(t))
        {
            t = Request.Query["t"].ToString();
        }

        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid) && uid > 0)
        {
            userId = uid;
            return true;
        }

        userId = 0;
        return false;
    }

    private int? TryGetCustomerUserId()
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var uid))
        {
            return uid;
        }

        return null;
    }

    private static string NormalizeOfferMoveType(string? moveType)
    {
        if (string.IsNullOrWhiteSpace(moveType))
        {
            return "Home";
        }

        return moveType.Trim().ToLowerInvariant() switch
        {
            "office" => "Office",
            "vehicleonly" or "vehicle" => "VehicleOnly",
            "storage" => "Storage",
            "partial" => "Partial",
            "international" => "International",
            _ => "Home"
        };
    }

    private string BuildAddress(int? cityId, int? districtId, int? neighborhoodId)
    {
        if (cityId == null)
            return string.Empty;

        var parts = new List<string>();

        if (neighborhoodId.HasValue)
        {
            var n = _context.Neighborhoods.FirstOrDefault(x => x.Id == neighborhoodId.Value);
            if (n != null)
            {
                parts.Add(n.Name);
            }
        }

        if (districtId.HasValue)
        {
            var d = _context.Districts.FirstOrDefault(x => x.Id == districtId.Value);
            if (d != null)
            {
                parts.Add(d.Name);
            }
        }

        var city = _context.Cities.FirstOrDefault(x => x.Id == cityId.Value);
        if (city != null)
        {
            parts.Add(city.Name);
        }

        return string.Join(" / ", parts);
    }

    [HttpGet]
    public IActionResult GetDistricts(int cityId)
    {
        var districts = _context.Districts
            .Where(d => d.CityId == cityId)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name })
            .ToList();

        return Json(districts);
    }

    [HttpGet]
    public IActionResult GetNeighborhoods(int districtId)
    {
        var neighborhoods = _context.Neighborhoods
            .Where(n => n.DistrictId == districtId)
            .OrderBy(n => n.Name)
            .Select(n => new { n.Id, n.Name })
            .ToList();

        return Json(neighborhoods);
    }

    // ===== SERVICE PAGES =====
    [HttpGet]
    public IActionResult EvTasima()
    {
        return View();
    }

    [HttpGet]
    public IActionResult OfisTasima()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Depolama()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ParcaEsya()
    {
        return View();
    }

    [HttpGet]
    public IActionResult SehirlerArasi()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Hakkimizda()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Iletisim()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Iletisim(string name, string email, string? phone, string subject, string message)
    {
        // In a real app, you would send an email or save to database
        TempData["ContactSuccess"] = "Mesajınız başarıyla gönderildi. En kısa sürede size dönüş yapacağız.";
        return RedirectToAction(nameof(Iletisim));
    }

    [HttpGet]
    public IActionResult SearchLocation(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Json(Enumerable.Empty<object>());
        }

        term = term.Trim();

        var query = from n in _context.Neighborhoods
                    join d in _context.Districts on n.DistrictId equals d.Id
                    join c in _context.Cities on d.CityId equals c.Id
                    where n.Name.Contains(term) || d.Name.Contains(term) || c.Name.Contains(term)
                    orderby c.Name, d.Name, n.Name
                    select new
                    {
                        CityId = c.Id,
                        DistrictId = d.Id,
                        NeighborhoodId = n.Id,
                        Display = n.Name + ", " + d.Name + "/" + c.Name + ", Türkiye"
                    };

        var results = query.Take(10).ToList();
        return Json(results);
    }

    // Kullanıcı Mesajlaşma
    [Authorize]
    public async Task<IActionResult> UserMessages(int moveRequestId)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests
            .FirstOrDefaultAsync(r => r.Id == moveRequestId && r.UserId == userId);
        
        if (request?.AcceptedOfferId.HasValue == true)
        {
            var offer = await _context.Offers
                .Include(o => o.Carrier)
                .FirstOrDefaultAsync(o => o.Id == request.AcceptedOfferId.Value);
            ViewBag.AcceptedOffer = offer;
        }

        if (request == null) return NotFound();

        var messages = await _context.Messages
            .Include(m => m.FromUser)
            .Include(m => m.FromCarrier)
            .Where(m => m.MoveRequestId == moveRequestId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // Okunmamış mesajları okundu olarak işaretle
        var unreadMessages = messages.Where(m => !m.IsRead && m.FromCarrierId.HasValue).ToList();
        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
        }
        await _context.SaveChangesAsync();

        ViewBag.Request = request;
        return View(messages);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendUserMessage(int moveRequestId, string content, IFormFile? attachment)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests.FirstOrDefaultAsync(r => r.Id == moveRequestId && r.UserId == userId);
        if (request == null) return NotFound();

        string? attachmentPath = null;
        if (attachment != null && attachment.Length > 0)
        {
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "messages");
            Directory.CreateDirectory(uploadsPath);
            var fileName = $"{Guid.NewGuid()}_{attachment.FileName}";
            var filePath = Path.Combine(uploadsPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await attachment.CopyToAsync(stream);
            }
            attachmentPath = $"messages/{fileName}";
        }

        var message = new Message
        {
            MoveRequestId = moveRequestId,
            FromUserId = userId,
            Content = content,
            AttachmentPath = attachmentPath
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(UserMessages), new { moveRequestId });
    }

    // Gelişmiş Teklif Karşılaştırma
    [Authorize]
    public async Task<IActionResult> CompareOffers(int moveRequestId)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests
            .FirstOrDefaultAsync(r => r.Id == moveRequestId && r.UserId == userId);
        if (request == null) return NotFound();

        var offers = await _context.Offers
            .Include(o => o.Carrier)
            .Where(o => o.MoveRequestId == moveRequestId)
            .OrderBy(o => o.Price)
            .ToListAsync();

        ViewBag.Request = request;
        return View(offers);
    }

    // Favori Firmalar
    [Authorize]
    public async Task<IActionResult> FavoriteCarriers()
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        // Favori firmalar için ayrı bir entity eklenebilir, şimdilik kullanıcının önceki iş yaptığı firmaları gösteriyoruz
        var favoriteCarrierIds = await _context.MoveRequests
            .Where(r => r.UserId == userId && r.AcceptedOfferId != null)
            .Join(_context.Offers,
                r => r.AcceptedOfferId,
                o => o.Id,
                (r, o) => o.CarrierId)
            .Distinct()
            .ToListAsync();

        var carriers = await _context.Carriers
            .Where(c => favoriteCarrierIds.Contains(c.Id))
            .ToListAsync();

        return View(carriers);
    }

    // Ödeme Geçmişi
    [Authorize]
    public async Task<IActionResult> PaymentHistory()
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var payments = await _context.Payments
            .Include(p => p.Contract)
            .ThenInclude(c => c.Offer)
            .ThenInclude(o => o.Carrier)
            .Include(p => p.Contract)
            .ThenInclude(c => c.MoveRequest)
            .Where(p => p.Contract.MoveRequest.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(payments);
    }

    // Fatura/İrsaliye PDF
    [Authorize]
    public async Task<IActionResult> DownloadInvoice(int paymentId)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var payment = await _context.Payments
            .Include(p => p.Contract)
            .ThenInclude(c => c.MoveRequest)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Contract.MoveRequest.UserId == userId);

        if (payment == null) return NotFound();

        // Basit PDF oluşturma (gerçekte bir PDF kütüphanesi kullanılmalı)
        var invoiceHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Fatura</title>
    <style>
        body {{ font-family: Arial; padding: 20px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .info {{ margin-bottom: 20px; }}
        table {{ width: 100%; border-collapse: collapse; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>FATURA</h1>
        <p>Fatura No: INV-{payment.Id}-{DateTime.UtcNow:yyyyMMdd}</p>
    </div>
    <div class='info'>
        <p><strong>Müşteri:</strong> {payment.Contract.MoveRequest.CustomerName}</p>
        <p><strong>Tarih:</strong> {payment.CreatedAt:dd.MM.yyyy}</p>
    </div>
    <table>
        <tr>
            <th>Açıklama</th>
            <th>Tutar</th>
        </tr>
        <tr>
            <td>Taşınma Hizmeti - Talep #{payment.Contract.MoveRequest.Id}</td>
            <td>{payment.Amount:N2} {payment.Currency}</td>
        </tr>
        <tr>
            <td><strong>Toplam</strong></td>
            <td><strong>{payment.Amount:N2} {payment.Currency}</strong></td>
        </tr>
    </table>
</body>
</html>";

        var bytes = System.Text.Encoding.UTF8.GetBytes(invoiceHtml);
        return File(bytes, "text/html", $"Fatura_{payment.Id}.html");
    }
}
