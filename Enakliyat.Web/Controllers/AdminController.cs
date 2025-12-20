using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Enakliyat.Web.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly EnakliyatDbContext _context;

    public AdminController(EnakliyatDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Dashboard()
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var now = DateTime.UtcNow;
        var sinceWeek = now.AddDays(-7);
        var today = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        ViewBag.TotalUsers = await _context.Users.CountAsync(u => !u.IsAdmin);
        ViewBag.TotalCarriers = await _context.Carriers.CountAsync();
        ViewBag.TotalRequests = await _context.MoveRequests.CountAsync();
        ViewBag.CompletedRequests = await _context.MoveRequests.CountAsync(r => r.Status.Contains("Tamamlandı"));
        ViewBag.CancelledRequests = await _context.MoveRequests.CountAsync(r => r.Status.Contains("İptal"));

        var total = (int)ViewBag.TotalRequests;
        var cancelled = (int)ViewBag.CancelledRequests;
        ViewBag.CancelRate = total > 0 ? (double)cancelled / total : 0d;

        ViewBag.NewRequestsWeek = await _context.MoveRequests.CountAsync(r => r.CreatedAt >= sinceWeek);
        ViewBag.NewUsersWeek = await _context.Users.CountAsync(u => !u.IsAdmin && u.CreatedAt >= sinceWeek);

        ViewBag.OpenOffers = await _context.Offers.CountAsync(o => o.Status == "Beklemede");

        ViewBag.ActiveReservations = await _context.MoveRequests
            .CountAsync(r => r.AcceptedOfferId != null && !r.Status.Contains("Tamamlandı") && !r.Status.Contains("İptal"));

        ViewBag.PendingCarrierApprovals = await _context.Carriers
            .CountAsync(c => !c.IsApproved && !c.IsRejected);

        var ratedCarriers = await _context.Carriers
            .Where(c => c.ReviewCount > 0)
            .ToListAsync();
        ViewBag.AverageCarrierRating = ratedCarriers.Any()
            ? ratedCarriers.Average(c => c.AverageRating)
            : 0d;

        var paidPaymentsWeek = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Paid && p.CreatedAt >= sinceWeek)
            .ToListAsync();
        ViewBag.WeeklyDepositCount = paidPaymentsWeek.Count;
        ViewBag.WeeklyDepositAmount = paidPaymentsWeek.Sum(p => p.Amount);

        // Son 30 günlük trend verileri (grafik için)
        var last30Days = Enumerable.Range(0, 30)
            .Select(i => today.AddDays(-29 + i))
            .ToList();

        var requestsByDay = await _context.MoveRequests
            .Where(r => r.CreatedAt >= last30Days.First() && r.CreatedAt < today.AddDays(1))
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var paymentsByDay = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Paid && p.CreatedAt >= last30Days.First() && p.CreatedAt < today.AddDays(1))
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.Date, x => x.Amount);

        ViewBag.ChartLabels = last30Days.Select(d => d.ToString("dd.MM")).ToList();
        ViewBag.RequestsChartData = last30Days.Select(d => requestsByDay.ContainsKey(d) ? requestsByDay[d] : 0).ToList();
        ViewBag.PaymentsChartData = last30Days.Select(d => paymentsByDay.ContainsKey(d) ? (double)paymentsByDay[d] : 0).ToList();

        // Durum dağılımı (pie chart için)
        var statusDistribution = await _context.MoveRequests
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        ViewBag.StatusDistribution = statusDistribution;

        var paidToday = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Paid && p.CreatedAt >= today && p.CreatedAt < today.AddDays(1))
            .ToListAsync();
        ViewBag.TodayRevenue = paidToday.Sum(p => p.Amount);

        var paidMonth = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Paid && p.CreatedAt >= monthStart && p.CreatedAt < monthStart.AddMonths(1))
            .ToListAsync();
        ViewBag.MonthRevenue = paidMonth.Sum(p => p.Amount);

        return View();
    }

    private bool IsCurrentUserAdmin()
    {
        return User.HasClaim("IsAdmin", "True");
    }

    public async Task<IActionResult> Requests(int? cityId, int? districtId, string? moveType, string? status, string? search, int page = 1, int pageSize = 20)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var cities = await _context.Cities
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (page < 1) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 20;

        ViewBag.Cities = cities;
        ViewBag.SelectedCityId = cityId;
        ViewBag.SelectedDistrictId = districtId;
        ViewBag.SelectedMoveType = moveType;
        ViewBag.SelectedStatus = status;
        ViewBag.Search = search;
        ViewBag.Search = search;

        var districts = Enumerable.Empty<District>();
        if (cityId.HasValue)
        {
            districts = await _context.Districts
                .Where(d => d.CityId == cityId.Value)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }
        ViewBag.Districts = districts;

        var query = _context.MoveRequests
            .Include(x => x.User)
            .AsQueryable();

        // Arama özelliği
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(r => 
                r.CustomerName.Contains(search) ||
                r.PhoneNumber.Contains(search) ||
                (r.Email != null && r.Email.Contains(search)) ||
                r.FromAddress.Contains(search) ||
                r.ToAddress.Contains(search) ||
                r.Id.ToString().Contains(search));
        }

        if (cityId.HasValue)
        {
            query = query.Where(r => r.FromCityId == cityId.Value || r.ToCityId == cityId.Value);
        }

        if (districtId.HasValue)
        {
            query = query.Where(r => r.FromDistrictId == districtId.Value || r.ToDistrictId == districtId.Value);
        }

        if (!string.IsNullOrWhiteSpace(moveType) && moveType != "All")
        {
            query = query.Where(r => r.MoveType == moveType);
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(r => r.Status == status);
        }

        var totalCount = await query.CountAsync();

        var requests = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var completedPaidIds = await _context.Contracts
            .Where(c => c.MoveRequest.Status == "Taşınma Tamamlandı" || c.MoveRequest.Status == "Tamamlandı")
            .Where(c => _context.Payments.Any(p => p.ContractId == c.Id && p.Status == PaymentStatus.Paid))
            .Select(c => c.MoveRequestId)
            .Distinct()
            .ToListAsync();

        ViewBag.CompletedPaidIds = completedPaidIds;

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var request = await _context.MoveRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request == null)
        {
            return NotFound();
        }

        request.Status = status;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Talep durumu güncellendi.";
        return RedirectToAction(nameof(Requests));
    }

    public async Task<IActionResult> Carriers(string? search, string? status, int page = 1, int pageSize = 20)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        if (page < 1) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 20;

        ViewBag.Search = search;
        ViewBag.Status = status;

        var query = _context.Carriers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(search) ||
                (c.CompanyName != null && c.CompanyName.Contains(search)) ||
                (c.Email != null && c.Email.Contains(search)) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(search)) ||
                (c.ServiceAreas != null && c.ServiceAreas.Contains(search)) ||
                (c.LicenseNumber != null && c.LicenseNumber.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            if (status == "Approved")
            {
                query = query.Where(c => c.IsApproved && !c.IsRejected);
            }
            else if (status == "Pending")
            {
                query = query.Where(c => !c.IsApproved && !c.IsRejected);
            }
            else if (status == "Rejected")
            {
                query = query.Where(c => c.IsRejected);
            }
            else if (status == "Suspended")
            {
                query = query.Where(c => c.IsSuspended);
            }
            else if (status == "Active")
            {
                query = query.Where(c => !c.IsSuspended && c.IsApproved && !c.IsRejected);
            }
        }

        var totalCount = await query.CountAsync();

        var carriers = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.PendingCarrierApplications = await _context.Carriers
            .CountAsync(c => !c.IsApproved && !c.IsRejected);

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return View(carriers);
    }

    public async Task<IActionResult> CarrierDetails(int id)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var carrier = await _context.Carriers
            .FirstOrDefaultAsync(c => c.Id == id);

        if (carrier == null)
        {
            return NotFound();
        }

        var offers = await _context.Offers
            .Include(o => o.MoveRequest)
            .Where(o => o.CarrierId == id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var reviews = await _context.Reviews
            .Include(r => r.MoveRequest)
            .Include(r => r.User)
            .Where(r => r.CarrierId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var vm = new AdminCarrierDetailsViewModel
        {
            Carrier = carrier,
            Offers = offers,
            Reviews = reviews
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCarrier(string name, string phoneNumber, string? companyName, string? email, string? licenseNumber, string? vehicleInfo, string? serviceAreas, string? description)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phoneNumber))
        {
            TempData["Error"] = "İsim ve telefon zorunludur.";
            return RedirectToAction(nameof(Carriers));
        }

        var carrier = new Carrier
        {
            Name = name,
            PhoneNumber = phoneNumber,
            CompanyName = companyName,
            Email = email,
            LicenseNumber = licenseNumber,
            VehicleInfo = vehicleInfo,
            ServiceAreas = serviceAreas,
            Description = description
        };

        await _context.Carriers.AddAsync(carrier);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Nakliyeci eklendi.";
        return RedirectToAction(nameof(Carriers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCarrier(int id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == id);
        if (carrier == null) return NotFound();

        carrier.IsApproved = true;
        carrier.IsRejected = false;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Nakliyeci başvurusu onaylandı.";
        return RedirectToAction(nameof(Carriers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCarrier(int id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == id);
        if (carrier == null) return NotFound();

        carrier.IsApproved = false;
        carrier.IsRejected = true;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Nakliyeci başvurusu reddedildi.";
        return RedirectToAction(nameof(Carriers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendCarrier(int id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == id);
        if (carrier == null) return NotFound();

        carrier.IsSuspended = true;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Nakliyeci hesabı askıya alındı.";
        return RedirectToAction(nameof(Carriers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnsuspendCarrier(int id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == id);
        if (carrier == null) return NotFound();

        carrier.IsSuspended = false;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Nakliyeci hesabı tekrar aktifleştirildi.";
        return RedirectToAction(nameof(Carriers));
    }

    public async Task<IActionResult> AddOnServices()
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var services = await _context.AddOnServices
            .OrderBy(s => s.Name)
            .ToListAsync();

        return View(services);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAddOnService(string name, decimal? defaultPrice, AddOnPricingType pricingType, bool isActive)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Hizmet adı zorunludur.";
            return RedirectToAction(nameof(AddOnServices));
        }

        var service = new AddOnService
        {
            Name = name,
            DefaultPrice = defaultPrice,
            PricingType = pricingType,
            IsActive = isActive
        };

        await _context.AddOnServices.AddAsync(service);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Ek hizmet eklendi.";
        return RedirectToAction(nameof(AddOnServices));
    }

    public async Task<IActionResult> Offers(int moveRequestId)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var request = await _context.MoveRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == moveRequestId);

        if (request == null)
        {
            return NotFound();
        }

        var offers = await _context.Offers
            .Include(o => o.Carrier)
            .Where(o => o.MoveRequestId == moveRequestId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var carriers = await _context.Carriers
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
            .ToListAsync();

        var vm = new AdminOffersViewModel
        {
            Request = request,
            Offers = offers,
            NewOffer = new AdminCreateOfferViewModel { MoveRequestId = moveRequestId },
            CarrierOptions = carriers
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Offers(AdminCreateOfferViewModel model)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return await Offers(model.MoveRequestId);
        }

        var requestExists = await _context.MoveRequests.AnyAsync(r => r.Id == model.MoveRequestId);
        var carrierExists = await _context.Carriers.AnyAsync(c => c.Id == model.CarrierId);

        if (!requestExists || !carrierExists)
        {
            return NotFound();
        }

        var offer = new Offer
        {
            MoveRequestId = model.MoveRequestId,
            CarrierId = model.CarrierId,
            Price = model.Price,
            Note = model.Note,
            Status = "Beklemede"
        };

        await _context.Offers.AddAsync(offer);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Teklif eklendi.";
        return RedirectToAction(nameof(Offers), new { moveRequestId = model.MoveRequestId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOfferStatus(int id, string status, int moveRequestId)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var offer = await _context.Offers.FirstOrDefaultAsync(o => o.Id == id && o.MoveRequestId == moveRequestId);
        if (offer == null)
        {
            return NotFound();
        }

        offer.Status = status;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Teklif durumu güncellendi.";
        return RedirectToAction(nameof(Offers), new { moveRequestId });
    }

    public async Task<IActionResult> Reviews()
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var reviews = await _context.Reviews
            .Include(r => r.Carrier)
            .Include(r => r.MoveRequest)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(reviews);
    }

    public async Task<IActionResult> RequestDetails(int id)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var request = await _context.MoveRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
        {
            return NotFound();
        }

        var offers = await _context.Offers
            .Include(o => o.Carrier)
            .Where(o => o.MoveRequestId == id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var vm = new AdminRequestDetailsViewModel
        {
            Request = request,
            Offers = offers
        };

        return View(vm);
    }

    public async Task<IActionResult> UserRequests(int? userId, string? moveType, string? status)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var users = await _context.Users
            .Where(u => !u.IsAdmin)
            .OrderBy(u => u.Email)
            .ToListAsync();

        ViewBag.Users = users;
        ViewBag.SelectedUserId = userId;
        ViewBag.SelectedMoveType = moveType;
        ViewBag.SelectedStatus = status;

        var query = _context.MoveRequests
            .Include(r => r.User)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(r => r.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(moveType) && moveType != "All")
        {
            query = query.Where(r => r.MoveType == moveType);
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(r => r.Status == status);
        }

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(requests);
    }

    public async Task<IActionResult> Users(string? search, string? status, int page = 1, int pageSize = 20)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        if (page < 1) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 20;

        ViewBag.Search = search;
        ViewBag.Status = status;

        var query = _context.Users
            .Include(u => u.MoveRequests)
            .Where(u => !u.IsAdmin)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(u =>
                u.Email.Contains(search) ||
                u.Name.Contains(search) ||
                u.PhoneNumber.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            if (status == "Banned")
            {
                query = query.Where(u => u.IsBanned);
            }
            else if (status == "Suspended")
            {
                query = query.Where(u => u.IsSuspended && !u.IsBanned);
            }
            else if (status == "Active")
            {
                query = query.Where(u => !u.IsBanned && !u.IsSuspended);
            }
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return View(users);
    }

    public async Task<IActionResult> UserDetails(int id)
    {
        if (!IsCurrentUserAdmin())
        {
            return Forbid();
        }

        var user = await _context.Users
            .Include(u => u.MoveRequests)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsAdmin);

        if (user == null)
        {
            return NotFound();
        }

        var requests = user.MoveRequests
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        var vm = new AdminUserDetailsViewModel
        {
            User = user,
            Requests = requests
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BanUser(int id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsAdmin);
        if (user == null) return NotFound();

        user.IsBanned = true;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Kullanıcı banlandı.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnbanUser(int id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsAdmin);
        if (user == null) return NotFound();

        user.IsBanned = false;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Kullanıcı ban kaldırıldı.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendUser(int id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsAdmin);
        if (user == null) return NotFound();

        user.IsSuspended = true;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Kullanıcı donduruldu.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnsuspendUser(int id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsAdmin);
        if (user == null) return NotFound();

        user.IsSuspended = false;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Kullanıcı tekrar aktifleştirildi.";
        return RedirectToAction(nameof(Users));
    }

    // Bildirim Yönetimi
    public async Task<IActionResult> Notifications()
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var templates = await _context.NotificationTemplates
            .OrderBy(t => t.Type)
            .ThenBy(t => t.EventType)
            .ToListAsync();

        return View(templates);
    }

    public async Task<IActionResult> NotificationTemplate(int? id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        if (id.HasValue)
        {
            var template = await _context.NotificationTemplates.FindAsync(id);
            if (template == null) return NotFound();
            return View(template);
        }

        return View(new NotificationTemplate());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotificationTemplate(NotificationTemplate template)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        if (ModelState.IsValid)
        {
            if (template.Id == 0)
            {
                _context.NotificationTemplates.Add(template);
            }
            else
            {
                _context.NotificationTemplates.Update(template);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Şablon kaydedildi.";
            return RedirectToAction(nameof(Notifications));
        }

        return View(template);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNotificationTemplate(int id)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null) return NotFound();

        _context.NotificationTemplates.Remove(template);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Şablon silindi.";
        return RedirectToAction(nameof(Notifications));
    }

    // Sistem Ayarları
    public async Task<IActionResult> Settings()
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var settings = await _context.SystemSettings
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync();

        ViewBag.Categories = settings.Select(s => s.Category).Distinct().ToList();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSetting(int id, string value)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var setting = await _context.SystemSettings.FindAsync(id);
        if (setting == null) return NotFound();

        setting.Value = value;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Ayar güncellendi.";
        return RedirectToAction(nameof(Settings));
    }

    public IActionResult CreateSetting()
    {
        if (!IsCurrentUserAdmin()) return Forbid();
        return View(new SystemSetting());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSetting(SystemSetting setting)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        if (ModelState.IsValid)
        {
            _context.SystemSettings.Add(setting);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Ayar oluşturuldu.";
            return RedirectToAction(nameof(Settings));
        }

        return View(setting);
    }

    // Komisyon Yönetimi
    public async Task<IActionResult> Commissions()
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var carriers = await _context.Carriers
            .Where(c => c.IsApproved)
            .OrderBy(c => c.Name)
            .ToListAsync();

        // Her firma için komisyon oranını ayarlardan al (varsayılan %10)
        var defaultCommissionRate = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "DefaultCommissionRate");
        var defaultRate = defaultCommissionRate != null && decimal.TryParse(defaultCommissionRate.Value, out var rate) ? rate : 10m;

        ViewBag.DefaultCommissionRate = defaultRate;
        return View(carriers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCarrierCommission(int carrierId, decimal commissionRate)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var setting = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == $"CarrierCommission_{carrierId}");

        if (setting == null)
        {
            setting = new SystemSetting
            {
                Key = $"CarrierCommission_{carrierId}",
                Category = "Commission",
                Description = $"Firma #{carrierId} komisyon oranı"
            };
            _context.SystemSettings.Add(setting);
        }

        setting.Value = commissionRate.ToString("F2");
        await _context.SaveChangesAsync();

        TempData["Success"] = "Komisyon oranı güncellendi.";
        return RedirectToAction(nameof(Commissions));
    }

    // Finansal Raporlar
    public async Task<IActionResult> FinancialReports(DateTime? startDate, DateTime? endDate)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var end = endDate ?? DateTime.UtcNow;

        var payments = await _context.Payments
            .Include(p => p.Contract)
            .ThenInclude(c => c.Offer)
            .ThenInclude(o => o.Carrier)
            .Where(p => p.CreatedAt >= start && p.CreatedAt <= end && p.Status == PaymentStatus.Paid)
            .ToListAsync();

        var totalRevenue = payments.Sum(p => p.Amount);
        var defaultCommissionRate = await _context.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "DefaultCommissionRate");
        var defaultRate = defaultCommissionRate != null && decimal.TryParse(defaultCommissionRate.Value, out var rate) ? rate : 10m;

        var totalCommission = totalRevenue * (defaultRate / 100m);

        ViewBag.StartDate = start;
        ViewBag.EndDate = end;
        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.TotalCommission = totalCommission;
        ViewBag.Payments = payments;

        return View();
    }

    // Toplu İşlemler
    public async Task<IActionResult> BulkOperations()
    {
        if (!IsCurrentUserAdmin()) return Forbid();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApproveCarriers(int[] carrierIds)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var carriers = await _context.Carriers
            .Where(c => carrierIds.Contains(c.Id))
            .ToListAsync();

        foreach (var carrier in carriers)
        {
            carrier.IsApproved = true;
            carrier.IsRejected = false;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = $"{carriers.Count} firma onaylandı.";
        return RedirectToAction(nameof(Carriers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSuspendCarriers(int[] carrierIds)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var carriers = await _context.Carriers
            .Where(c => carrierIds.Contains(c.Id))
            .ToListAsync();

        foreach (var carrier in carriers)
        {
            carrier.IsSuspended = true;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = $"{carriers.Count} firma askıya alındı.";
        return RedirectToAction(nameof(Carriers));
    }

    // Firma Belge Onay Sistemi
    public async Task<IActionResult> CarrierDocuments(int? carrierId)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        IQueryable<CarrierDocument> query = _context.CarrierDocuments.Include(d => d.Carrier);

        if (carrierId.HasValue)
        {
            query = query.Where(d => d.CarrierId == carrierId.Value);
        }

        var documents = await query
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        ViewBag.Carriers = await _context.Carriers
            .Where(c => c.IsApproved || !c.IsRejected)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(documents);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCarrierDocument(int documentId)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var document = await _context.CarrierDocuments
            .Include(d => d.Carrier)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return NotFound();

        // Belge onaylandı olarak işaretle (CarrierDocument'a IsApproved eklenebilir)
        // Şimdilik sadece carrier'ı onayla
        if (!document.Carrier.IsApproved)
        {
            document.Carrier.IsApproved = true;
            document.Carrier.IsRejected = false;
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Belge onaylandı.";
        return RedirectToAction(nameof(CarrierDocuments));
    }

    // Raporlama/Export
    public async Task<IActionResult> ExportRequests(DateTime? startDate, DateTime? endDate)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var end = endDate ?? DateTime.UtcNow;

        var requests = await _context.MoveRequests
            .Include(r => r.User)
            .Where(r => r.CreatedAt >= start && r.CreatedAt <= end)
            .ToListAsync();

        // AcceptedOffer'ları ayrı çek
        var acceptedOfferIds = requests.Where(r => r.AcceptedOfferId.HasValue).Select(r => r.AcceptedOfferId!.Value).ToList();
        var acceptedOffers = await _context.Offers
            .Include(o => o.Carrier)
            .Where(o => acceptedOfferIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id);

        // CSV formatında export (Excel için)
        var csv = "ID,Tarih,Müşteri,Telefon,Email,Nereden,Nereye,Tip,Durum,Firma,Fiyat\n";
        foreach (var req in requests)
        {
            var offer = req.AcceptedOfferId.HasValue && acceptedOffers.ContainsKey(req.AcceptedOfferId.Value) 
                ? acceptedOffers[req.AcceptedOfferId.Value] 
                : null;
            csv += $"{req.Id},{req.CreatedAt:dd.MM.yyyy},{req.CustomerName},{req.PhoneNumber},{req.Email ?? ""},";
            csv += $"{req.FromAddress},{req.ToAddress},{req.MoveType},{req.Status},";
            csv += $"{offer?.Carrier?.Name ?? ""},{offer?.Price ?? 0}\n";
        }

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"Talepler_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
    }

    // Log/Audit Trail (Basit versiyon - SystemSetting ile log tutulabilir)
    public async Task<IActionResult> ActivityLogs(DateTime? startDate, DateTime? endDate)
    {
        if (!IsCurrentUserAdmin()) return Forbid();

        var start = startDate ?? DateTime.UtcNow.AddDays(-7);
        var end = endDate ?? DateTime.UtcNow;

        // Şimdilik SystemSetting'lerde Category="Log" olanları göster
        // İleride ayrı bir ActivityLog entity'si eklenebilir
        var logs = await _context.SystemSettings
            .Where(s => s.Category == "Log" && s.CreatedAt >= start && s.CreatedAt <= end)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        ViewBag.StartDate = start;
        ViewBag.EndDate = end;

        return View(logs);
    }
}
