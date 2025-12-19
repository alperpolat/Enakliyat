using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Enakliyat.Web.Controllers;

[AllowAnonymous]
public class TestController : Controller
{
    private readonly EnakliyatDbContext _context;

    public TestController(EnakliyatDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> SeedTestOffersForLastRequest(int? moveRequestId, int count = 5)
    {
        var query = _context.MoveRequests.AsQueryable();

        MoveRequest? targetRequest;

        if (moveRequestId.HasValue)
        {
            targetRequest = await query.FirstOrDefaultAsync(r => r.Id == moveRequestId.Value);
        }
        else
        {
            targetRequest = await query
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();
        }

        if (targetRequest == null)
        {
            return BadRequest("Uygun taşınma talebi bulunamadı.");
        }

        var existingCarrierIds = await _context.Offers
            .Where(o => o.MoveRequestId == targetRequest.Id)
            .Select(o => o.CarrierId)
            .ToListAsync();

        var carriersQuery = _context.Carriers
            .Where(c => c.IsApproved && !c.IsRejected && !c.IsSuspended);

        if (existingCarrierIds.Any())
        {
            carriersQuery = carriersQuery.Where(c => !existingCarrierIds.Contains(c.Id));
        }

        var carriers = await carriersQuery
            .OrderBy(c => Guid.NewGuid())
            .Take(count)
            .ToListAsync();

        if (!carriers.Any())
        {
            return BadRequest("Uygun onaylı nakliyeci bulunamadı veya hepsi bu ilana zaten teklif vermiş.");
        }

        var random = new Random();

        foreach (var carrier in carriers)
        {
            var price = random.Next(8000, 20001);

            var offer = new Offer
            {
                MoveRequestId = targetRequest.Id,
                CarrierId = carrier.Id,
                Price = price,
                Note = "Otomatik test teklifi",
                Status = "Beklemede"
            };

            await _context.Offers.AddAsync(offer);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            MoveRequestId = targetRequest.Id,
            CreatedOffers = carriers.Select(c => new { c.Id, c.Name }).ToList()
        });
    }
}
