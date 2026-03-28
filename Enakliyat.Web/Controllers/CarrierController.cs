using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Models;
using Enakliyat.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Enakliyat.Web.Controllers;

[Authorize(AuthenticationSchemes = "CarrierAuth")]
public class CarrierController : Controller
{
    private readonly EnakliyatDbContext _context;
    private readonly INotificationService _notificationService;

    public CarrierController(EnakliyatDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == carrierId);
        if (carrier == null)
        {
            return Unauthorized();
        }

        var documents = await _context.CarrierDocuments
            .Where(d => d.CarrierId == carrierId)
            .OrderBy(d => d.DocumentType)
            .ToListAsync();

        var vm = new CarrierProfileViewModel
        {
            Carrier = carrier,
            Documents = documents
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(CarrierProfileViewModel model)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == carrierId);
        if (carrier == null)
        {
            return Unauthorized();
        }

        carrier.Name = model.Carrier.Name;
        carrier.CompanyName = model.Carrier.CompanyName;
        carrier.PhoneNumber = model.Carrier.PhoneNumber;
        carrier.Email = model.Carrier.Email;
        carrier.LicenseNumber = model.Carrier.LicenseNumber;
        carrier.VehicleInfo = model.Carrier.VehicleInfo;
        carrier.ServiceAreas = model.Carrier.ServiceAreas;
        carrier.Description = model.Carrier.Description;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Profiliniz güncellendi.";
        return RedirectToAction(nameof(Profile));
    }

    public async Task<IActionResult> Leads(int? cityId, DateTime? fromDate, DateTime? toDate, string? offerFilter)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        ViewBag.CityId = cityId;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.OfferFilter = offerFilter;

        var cities = await _context.Cities
            .OrderBy(c => c.Name)
            .ToListAsync();
        ViewBag.Cities = cities;

        var query = _context.MoveRequests
            .Include(r => r.User)
            .AsQueryable();

        // Sadece iptal edilmemiş talepler
        query = query.Where(r => !r.Status.Contains("İptal"));

        if (cityId.HasValue)
        {
            query = query.Where(r => r.FromCityId == cityId.Value || r.ToCityId == cityId.Value);
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(r => r.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date.AddDays(1);
            query = query.Where(r => r.CreatedAt < to);
        }

        // Teklif filtreleri
        var allRequests = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var moveRequestIds = allRequests.Select(r => r.Id).ToList();
        var offersForCarrier = await _context.Offers
            .Where(o => o.CarrierId == carrierId && moveRequestIds.Contains(o.MoveRequestId))
            .ToListAsync();

        var hasOfferIds = offersForCarrier.Select(o => o.MoveRequestId).Distinct().ToHashSet();

        IEnumerable<MoveRequest> result = allRequests;
        if (!string.IsNullOrWhiteSpace(offerFilter) && offerFilter != "All")
        {
            if (offerFilter == "NoOffer")
            {
                result = allRequests.Where(r => !hasOfferIds.Contains(r.Id));
            }
            else if (offerFilter == "HasOffer")
            {
                result = allRequests.Where(r => hasOfferIds.Contains(r.Id));
            }
        }

        return View(result);
    }

    public async Task<IActionResult> Dashboard()
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var now = DateTime.UtcNow;
        var sinceWeek = now.AddDays(-7);

        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == carrierId);
        if (carrier == null)
        {
            return Unauthorized();
        }

        var offersQuery = _context.Offers
            .Include(o => o.MoveRequest)
            .Where(o => o.CarrierId == carrierId);

        var offers = await offersQuery.ToListAsync();

        ViewBag.CarrierName = carrier.Name;
        ViewBag.TotalOffers = offers.Count;
        ViewBag.PendingOffers = offers.Count(o => o.Status == "Beklemede");
        ViewBag.AcceptedOffers = offers.Count(o => o.Status == "Kabul Edildi");

        var completedRequests = await _context.MoveRequests
            .Where(r => r.Status.Contains("Tamamlandı") && r.AcceptedOfferId != null)
            .Join(_context.Offers,
                r => r.AcceptedOfferId,
                o => o.Id,
                (r, o) => new { r, o })
            .Where(ro => ro.o.CarrierId == carrierId)
            .Select(ro => ro.r)
            .ToListAsync();

        ViewBag.CompletedMoves = completedRequests.Count;

        // Kabul edilmiş ama henüz tamamlanmamış aktif rezervasyonlar
        var activeReservations = await _context.MoveRequests
            .Where(r => r.AcceptedOfferId != null && !r.Status.Contains("Tamamlandı") && !r.Status.Contains("İptal"))
            .Join(_context.Offers,
                r => r.AcceptedOfferId,
                o => o.Id,
                (r, o) => new { r, o })
            .Where(ro => ro.o.CarrierId == carrierId)
            .Select(ro => ro.r)
            .ToListAsync();

        ViewBag.AcceptedReservations = activeReservations.Count;

        var paidPayments = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Paid && p.CreatedAt >= sinceWeek)
            .Join(_context.Contracts,
                p => p.ContractId,
                c => c.Id,
                (p, c) => new { p, c })
            .Join(_context.Offers,
                pc => pc.c.OfferId,
                o => o.Id,
                (pc, o) => new { pc.p, o })
            .Where(x => x.o.CarrierId == carrierId)
            .Select(x => x.p)
            .ToListAsync();

        ViewBag.WeeklyDepositCount = paidPayments.Count;
        ViewBag.WeeklyDepositAmount = paidPayments.Sum(p => p.Amount);

        // Son 7 günde bu carrieer için henüz teklif vermediği yeni lead sayısı
        var weekRequests = await _context.MoveRequests
            .Where(r => r.CreatedAt >= sinceWeek && !r.Status.Contains("İptal"))
            .ToListAsync();
        var weekRequestIds = weekRequests.Select(r => r.Id).ToList();
        var weekOffersForCarrier = await _context.Offers
            .Where(o => o.CarrierId == carrierId && weekRequestIds.Contains(o.MoveRequestId))
            .Select(o => o.MoveRequestId)
            .Distinct()
            .ToListAsync();
        var weekHasOfferIds = new HashSet<int>(weekOffersForCarrier);
        ViewBag.NewLeadsWeek = weekRequests.Count(r => !weekHasOfferIds.Contains(r.Id));

        ViewBag.AverageRating = carrier.ReviewCount > 0 ? carrier.AverageRating : (double?)null;
        ViewBag.ReviewCount = carrier.ReviewCount;

        return View();
    }

    public async Task<IActionResult> Offers(string? status)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        ViewBag.SelectedStatus = status;

        var query = _context.Offers
            .Include(o => o.MoveRequest)
            .ThenInclude(r => r.User)
            .Where(o => o.CarrierId == carrierId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(o => o.Status == status);
        }

        var offers = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var since = DateTime.UtcNow.AddDays(-1);
        ViewBag.NewOffersCount = offers
            .Count(o => o.Status == "Beklemede" && o.CreatedAt >= since);

        // Yeni teklif sayısı (bildirim badge için)
        ViewBag.NewOffersCount = offers.Count(o => o.Status == "Beklemede" && o.CreatedAt >= since);
        
        // Bekleyen teklifler (kabul/red bekliyor)
        ViewBag.PendingOffersCount = offers.Count(o => o.Status == "Beklemede");

        return View(offers);
    }

    public async Task<IActionResult> LeadDetails(int id)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return NotFound();
        }

        var existingOffers = await _context.Offers
            .Include(o => o.Carrier)
            .Where(o => o.MoveRequestId == id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        ViewBag.Request = request;
        ViewBag.ExistingOffers = existingOffers;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOffer(int moveRequestId, decimal price, string? note)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var requestExists = await _context.MoveRequests.AnyAsync(r => r.Id == moveRequestId);
        if (!requestExists)
        {
            return NotFound();
        }

        var offer = new Enakliyat.Domain.Offer
        {
            MoveRequestId = moveRequestId,
            CarrierId = carrierId,
            Price = price,
            Note = note,
            Status = "Beklemede"
        };

        await _context.Offers.AddAsync(offer);
        await _context.SaveChangesAsync();

        // Bildirim gönder
        await _notificationService.NotifyNewOfferToUserAsync(offer);

        TempData["Success"] = "Teklifiniz oluşturuldu.";
        return RedirectToAction(nameof(LeadDetails), new { id = moveRequestId });
    }

    public async Task<IActionResult> Reviews()
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var reviews = await _context.Reviews
            .Include(r => r.MoveRequest)
            .Include(r => r.User)
            .Where(r => r.CarrierId == carrierId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(reviews);
    }

    public async Task<IActionResult> Payments()
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var payments = await _context.Payments
            .Include(p => p.Contract)
            .ThenInclude(c => c.Offer)
            .Where(p => p.Contract.Offer.CarrierId == carrierId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(payments);
    }

    public async Task<IActionResult> ReservationsCalendar(int? year, int? month)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var now = DateTime.UtcNow;
        int y = year ?? now.Year;
        int m = month ?? now.Month;
        var firstDay = new DateTime(y, m, 1);
        var lastDay = firstDay.AddMonths(1);

        var reservations = await _context.MoveRequests
            .Where(r => r.AcceptedOfferId != null && r.MoveDate < lastDay &&
                        (r.MoveDateEnd ?? r.MoveDate) >= firstDay)
            .Join(_context.Offers,
                r => r.AcceptedOfferId,
                o => o.Id,
                (r, o) => new { r, o })
            .Where(ro => ro.o.CarrierId == carrierId)
            .Select(ro => ro.r)
            .Include(r => r.User)
            .ToListAsync();

        ViewBag.Year = y;
        ViewBag.Month = m;
        ViewBag.FirstDay = firstDay;
        ViewBag.Reservations = reservations;

        return View();
    }

    public async Task<IActionResult> Reservations()
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var reservations = await _context.MoveRequests
            .Where(r => r.AcceptedOfferId != null)
            .Join(_context.Offers,
                r => r.AcceptedOfferId,
                o => o.Id,
                (r, o) => new { r, o })
            .Where(ro => ro.o.CarrierId == carrierId)
            .Select(ro => ro.r)
            .Include(r => r.User)
            .OrderByDescending(r => r.MoveDate)
            .ToListAsync();

        return View(reservations);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReservationInfo(int id, string? assignedTeam, DateTime? estimatedArrivalTime)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.AcceptedOfferId != null);
        if (request == null)
        {
            return NotFound();
        }

        var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == request.AcceptedOfferId && o.CarrierId == carrierId);
        if (offer == null)
        {
            return Forbid();
        }

        request.AssignedTeam = assignedTeam;
        request.EstimatedArrivalTime = estimatedArrivalTime;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Reservations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteReservation(int id)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.AcceptedOfferId != null);
        if (request == null)
        {
            return NotFound();
        }

        var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == request.AcceptedOfferId && o.CarrierId == carrierId);
        if (offer == null)
        {
            return Forbid();
        }

        request.Status = "Taşınma Tamamlandı";
        request.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Reservations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditOffer(int id, decimal price, string? note, string? statusFilter)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var offer = await _context.Offers
            .Include(o => o.MoveRequest)
            .FirstOrDefaultAsync(o => o.Id == id && o.CarrierId == carrierId);

        if (offer == null)
        {
            return NotFound();
        }

        offer.Price = price;
        offer.Note = note;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Offers), new { status = statusFilter });
    }

    // Mesajlaşma
    public async Task<IActionResult> Messages(int moveRequestId)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == moveRequestId);

        if (request == null) return NotFound();

        var messages = await _context.Messages
            .Include(m => m.FromUser)
            .Include(m => m.FromCarrier)
            .Where(m => m.MoveRequestId == moveRequestId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // Okunmamış mesajları okundu olarak işaretle
        var unreadMessages = messages.Where(m => !m.IsRead && m.FromUserId.HasValue).ToList();
        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
        }
        await _context.SaveChangesAsync();

        ViewBag.Request = request;
        return View(messages);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int moveRequestId, string content, IFormFile? attachment)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests.FirstOrDefaultAsync(r => r.Id == moveRequestId);
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
            FromCarrierId = carrierId,
            Content = content,
            AttachmentPath = attachmentPath
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Messages), new { moveRequestId });
    }

    // Teklif Şablonları
    public async Task<IActionResult> OfferTemplates()
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var templates = await _context.OfferTemplates
            .Where(t => t.CarrierId == carrierId)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync();

        return View(templates);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOfferTemplate(OfferTemplate template)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        template.CarrierId = carrierId;
        
        if (template.IsDefault)
        {
            // Diğer default'ları kaldır
            var otherDefaults = await _context.OfferTemplates
                .Where(t => t.CarrierId == carrierId && t.IsDefault)
                .ToListAsync();
            foreach (var t in otherDefaults)
            {
                t.IsDefault = false;
            }
        }

        _context.OfferTemplates.Add(template);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Şablon oluşturuldu.";
        return RedirectToAction(nameof(OfferTemplates));
    }

    // Fiyat Hesaplayıcı
    [HttpPost]
    public async Task<IActionResult> CalculatePrice(int moveRequestId, int? templateId)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var request = await _context.MoveRequests.FirstOrDefaultAsync(r => r.Id == moveRequestId);
        if (request == null) return NotFound();

        decimal calculatedPrice = 0;

        if (templateId.HasValue)
        {
            var template = await _context.OfferTemplates
                .FirstOrDefaultAsync(t => t.Id == templateId.Value && t.CarrierId == carrierId);
            
            if (template != null)
            {
                calculatedPrice = template.BasePrice ?? 0;
                
                // Mesafe hesaplama (basit - gerçekte API kullanılabilir)
                if (template.PricePerKm.HasValue && request.FromCityId.HasValue && request.ToCityId.HasValue)
                {
                    // Basit mesafe tahmini (şehirler arası ortalama)
                    var estimatedKm = 200; // Varsayılan
                    calculatedPrice += template.PricePerKm.Value * estimatedKm;
                }
                
                // Oda sayısı
                if (template.PricePerRoom.HasValue && !string.IsNullOrEmpty(request.RoomType))
                {
                    var roomCount = request.RoomType.Contains("1+1") ? 1 : 
                                   request.RoomType.Contains("2+1") ? 2 :
                                   request.RoomType.Contains("3+1") ? 3 : 4;
                    calculatedPrice += template.PricePerRoom.Value * roomCount;
                }
                
                // Kat
                if (template.PricePerFloor.HasValue)
                {
                    if (request.FromFloor.HasValue) calculatedPrice += template.PricePerFloor.Value * request.FromFloor.Value;
                    if (request.ToFloor.HasValue) calculatedPrice += template.PricePerFloor.Value * request.ToFloor.Value;
                }
            }
        }

        return Json(new { price = calculatedPrice });
    }

    // Müşteri Geçmişi
    public async Task<IActionResult> CustomerHistory(int userId)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound();

        var requests = await _context.MoveRequests
            .Where(r => r.UserId == userId)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var offers = await _context.Offers
            .Where(o => o.CarrierId == carrierId && requests.Select(r => r.Id).Contains(o.MoveRequestId))
            .ToListAsync();

        ViewBag.User = user;
        ViewBag.Requests = requests;
        ViewBag.Offers = offers;

        return View();
    }

    // Toplu Teklif
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreateOffers(int[] moveRequestIds, decimal price, string? note)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var createdCount = 0;
        foreach (var requestId in moveRequestIds)
        {
            var exists = await _context.Offers
                .AnyAsync(o => o.MoveRequestId == requestId && o.CarrierId == carrierId);
            
            if (!exists)
            {
                var offer = new Offer
                {
                    MoveRequestId = requestId,
                    CarrierId = carrierId,
                    Price = price,
                    Note = note,
                    Status = "Beklemede"
                };
                _context.Offers.Add(offer);
                createdCount++;
            }
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"{createdCount} teklif oluşturuldu.";
        return RedirectToAction(nameof(Leads));
    }

    // Raporlama
    public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate)
    {
        var carrierIdClaim = User.FindFirst("CarrierId");
        if (carrierIdClaim == null || !int.TryParse(carrierIdClaim.Value, out var carrierId))
        {
            return Unauthorized();
        }

        var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var end = endDate ?? DateTime.UtcNow;

        var offers = await _context.Offers
            .Include(o => o.MoveRequest)
            .Where(o => o.CarrierId == carrierId && o.CreatedAt >= start && o.CreatedAt <= end)
            .ToListAsync();

        var acceptedOffers = offers.Where(o => o.Status == "Kabul Edildi").ToList();
        var totalRevenue = acceptedOffers.Sum(o => o.Price);
        var completedMoves = await _context.MoveRequests
            .Where(r => r.Status.Contains("Tamamlandı") && r.AcceptedOfferId != null)
            .Join(_context.Offers.Where(o => o.CarrierId == carrierId),
                r => r.AcceptedOfferId,
                o => o.Id,
                (r, o) => r)
            .Where(r => r.CompletedAt >= start && r.CompletedAt <= end)
            .CountAsync();

        ViewBag.StartDate = start;
        ViewBag.EndDate = end;
        ViewBag.TotalOffers = offers.Count;
        ViewBag.AcceptedOffers = acceptedOffers.Count;
        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.CompletedMoves = completedMoves;
        ViewBag.Offers = offers;

        return View();
    }
}
