using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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

    public async Task<IActionResult> Requests(int? cityId, int? districtId, string? moveType, string? status, int page = 1, int pageSize = 20)
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
}
