using System.Diagnostics;
using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Models;
using Enakliyat.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Enakliyat.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly EnakliyatDbContext _context;
    private readonly IReservationNotificationService _notificationService;
    private readonly INotificationService _generalNotificationService;
    private readonly IWebHostEnvironment _env;

    public HomeController(ILogger<HomeController> logger, EnakliyatDbContext context, IReservationNotificationService notificationService, INotificationService generalNotificationService, IWebHostEnvironment env)
    {
        _logger = logger;
        _context = context;
        _notificationService = notificationService;
        _generalNotificationService = generalNotificationService;
        _env = env;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var cities = _context.Cities
            .OrderBy(c => c.Name)
            .ToList();

        ViewBag.Cities = cities;

        return View(new MoveRequestViewModel());
    }

    [HttpGet]
    public IActionResult Offers(int? id, string? moveType = null)
    {
        ViewBag.AddOnServices = _context.AddOnServices
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToList();

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
        ViewBag.AddOnServices = _context.AddOnServices
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToList();

        try
        {
            if (!model.FromCityId.HasValue || !model.ToCityId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Lütfen nereden ve nereye adreslerini listeden seçin.");
            }

            if (!ModelState.IsValid || !model.KvkkAccepted)
            {
                if (!model.KvkkAccepted)
                {
                    ModelState.AddModelError(string.Empty, "Lütfen KVKK ve sözleşmeleri onaylayın.");
                }

                return View(model);
            }

            int? userId = TryGetCustomerUserId();
            var fromAddress = BuildAddress(model.FromCityId, model.FromDistrictId, model.FromNeighborhoodId);
            var toAddress = BuildAddress(model.ToCityId, model.ToDistrictId, model.ToNeighborhoodId);

            MoveRequest request;
            var isNew = model.MoveRequestId <= 0;

            if (isNew)
            {
                request = new MoveRequest { UserId = userId };
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

                if (userId.HasValue)
                {
                    if (request.UserId != userId.Value)
                    {
                        TempData["Error"] = "Bu talebe erişim yetkiniz yok.";
                        return RedirectToAction(nameof(Requests));
                    }
                }
                else
                {
                    if (request.UserId != null)
                    {
                        TempData["Error"] = "Bu talebi düzenlemek için giriş yapın.";
                        return RedirectToAction("Login", "Account");
                    }

                    var sessionOfferId = HttpContext.Session.GetInt32(AnonymousOfferSessionKey);
                    if (!sessionOfferId.HasValue || sessionOfferId.Value != request.Id)
                    {
                        TempData["Error"] = "Talep bulunamadı.";
                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            request.FromAddress = fromAddress;
            request.ToAddress = toAddress;
            request.FromCityId = model.FromCityId;
            request.FromDistrictId = model.FromDistrictId;
            request.FromNeighborhoodId = model.FromNeighborhoodId;
            request.ToCityId = model.ToCityId;
            request.ToDistrictId = model.ToDistrictId;
            request.ToNeighborhoodId = model.ToNeighborhoodId;
            request.CustomerName = model.CustomerName;
            request.PhoneNumber = model.PhoneNumber;
            request.Email = model.Email;
            request.MoveDate = model.MoveDate;
            request.MoveType = string.IsNullOrWhiteSpace(model.MoveType) ? "Home" : model.MoveType;
            request.RoomType = model.RoomType;
            request.FromFloor = model.FromFloor;
            request.FromHasElevator = model.FromHasElevator;
            request.ToFloor = model.ToFloor;
            request.ToHasElevator = model.ToHasElevator;
            request.Notes = model.Notes;
            request.Status = "Teklif Bekliyor";

            await _context.SaveChangesAsync();

            if (isNew && !userId.HasValue)
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

            if (userId.HasValue)
            {
                TempData["Success"] = isNew
                    ? $"Talebiniz oluşturuldu. Talep numaranız: #{request.Id}."
                    : "Talebiniz başarıyla güncellendi.";
                return RedirectToAction(nameof(Requests));
            }

            TempData["Success"] =
                $"Talebiniz alındı. Talep numaranız: #{request.Id}. Taleplerinizi görmek için giriş yapabilirsiniz.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving offer details for request {RequestId}", model.MoveRequestId);
            TempData["Error"] = "Talebiniz kaydedilirken bir hata oluştu. Lütfen tekrar deneyin.";
            return View(model);
        }
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

    [Authorize]
    public IActionResult Details(int id)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
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

        var vm = new UserRequestDetailsViewModel
        {
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
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(int id, string status)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
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
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptOffer(int offerId, int moveRequestId)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
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
        return RedirectToAction(nameof(Reservation), new { id = moveRequestId });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Reservation(int id)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
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
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int moveRequestId, string cardHolder, string cardNumber, string expiry, string cvv)
    {
        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
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
            return RedirectToAction(nameof(Reservation), new { id = moveRequestId });
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
        return RedirectToAction(nameof(Reservation), new { id = moveRequestId });
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
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReview(ReviewViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Details), new { id = model.MoveRequestId });
        }

        var userIdClaim = User.FindFirst("UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
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
        return RedirectToAction(nameof(Details), new { id = model.MoveRequestId });
    }

    private const string AnonymousOfferSessionKey = "AnonymousOfferId";

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
