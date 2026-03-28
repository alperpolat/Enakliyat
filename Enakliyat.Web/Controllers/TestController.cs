using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Models;
using Enakliyat.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Enakliyat.Web.Controllers;

[AllowAnonymous]
public class TestController : Controller
{
    private readonly EnakliyatDbContext _context;
    private readonly ISmsService _smsService;
    private readonly SmsSettings _smsSettings;
    private readonly IWebHostEnvironment _environment;

    public TestController(
        EnakliyatDbContext context,
        ISmsService smsService,
        IOptions<SmsSettings> smsSettings,
        IWebHostEnvironment environment)
    {
        _context = context;
        _smsService = smsService;
        _smsSettings = smsSettings.Value;
        _environment = environment;
    }

    /// <summary>
    /// Yalnızca Development: İletimX ile tek SMS dener. Örnek: <c>/Test/SmsPing?phone=05321234567</c>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SmsPing([FromQuery] string phone, [FromQuery] string? msg = null, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new { error = "phone parametresi gerekli (örn. 05xx ile başlayan cep)." });
        }

        var message = string.IsNullOrWhiteSpace(msg)
            ? "Road of Home SMS test — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            : msg.Trim();

        var result = await _smsService.SendAsync(phone, message, cancellationToken);

        return Ok(new
        {
            environment = _environment.EnvironmentName,
            result = new { ok = result.Ok, detail = result.Detail },
            request = new { phoneInput = phone, messageLength = message.Length },
            config = new
            {
                enabled = _smsSettings.Enabled,
                publicBaseUrl = string.IsNullOrWhiteSpace(_smsSettings.PublicBaseUrl) ? "(boş — istek hostu kullanılır)" : _smsSettings.PublicBaseUrl,
                apiGatewayUrl = _smsSettings.ApiGatewayUrl,
                kullaniciAdiSet = !string.IsNullOrWhiteSpace(_smsSettings.KullaniciAdi),
                sifreSet = !string.IsNullOrWhiteSpace(_smsSettings.Sifre),
                bayiKoduSet = !string.IsNullOrWhiteSpace(_smsSettings.BayiKodu),
                baslik = string.IsNullOrWhiteSpace(_smsSettings.Baslik) ? "(boş)" : _smsSettings.Baslik
            },
            hints = new
            {
                enabledFalse = "Sms:Enabled false ise gönderim yapılmaz; appsettings Development içinde true deneyin.",
                iletimx = "not_configured: ApiGatewayUrl, KullaniciAdi, Sifre, BayiKodu, Baslik eksik.",
                invalid_phone = "10 hane 5xxxxxxxxx olmalı (0 ve 90 olmadan normalize edilir).",
                disabled = "Sms.Enabled kapalı."
            }
        });
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
