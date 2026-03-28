using System.Text.Json;
using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Enakliyat.Web;

public static class DataSeeder
{
    public static async Task SeedAdminUserAsync(EnakliyatDbContext context)
    {
        await context.Database.MigrateAsync();

        if (!context.Users.Any(u => u.IsAdmin))
        {
            var admin = new User
            {
                Email = "admin@ibo.com",
                Password = PasswordHasher.Hash("Admin123!"),
                IsAdmin = true
            };

            context.Users.Add(admin);
            await context.SaveChangesAsync();
        }
        #region test

        //if (!context.Users.Any(u => !u.IsAdmin))
        //{
        //    var user = new User
        //    {
        //        Email = "user@enakliyat.local",
        //        Password = PasswordHasher.Hash("User123!"),
        //        IsAdmin = false
        //    };

        //    context.Users.Add(user);
        //    await context.SaveChangesAsync();
        //}

        //if (!context.Carriers.Any())
        //{
        //    // İstanbul'dan bir district al (varsa)
        //    var istanbulDistrict = await context.Districts
        //        .Include(d => d.City)
        //        .Where(d => d.City.Name == "İstanbul")
        //        .FirstOrDefaultAsync();

        //    var carrier = new Carrier
        //    {
        //        Name = "Demo Nakliyeci",
        //        CompanyName = "Demo Nakliyat",
        //        PhoneNumber = "+90 555 000 0000",
        //        LandlinePhone = "+90 212 000 0000",
        //        Email = "carrier@enakliyat.local",
        //        Website = "https://demo-nakliyat.com",
        //        DistrictId = istanbulDistrict?.Id,
        //        LicenseNumber = "YETKI-DEMO-001",
        //        VehicleInfo = "3+1 kamyon, 2 personel",
        //        ServiceAreas = "İstanbul, Ankara",
        //        Description = "Demo amaçlı onaylı nakliyeci.",
        //        TaxOffice = "Beşiktaş Vergi Dairesi",
        //        TaxNumber = "1234567890",
        //        InvoiceAddress = "İstanbul, Beşiktaş, Demo Mahallesi, Demo Sokak No:1",
        //        IsApproved = true,
        //        IsRejected = false
        //    };

        //    await context.Carriers.AddAsync(carrier);
        //    await context.SaveChangesAsync();

        //    var carrierUser = new CarrierUser
        //    {
        //        Email = "carrier@enakliyat.local",
        //        Password = PasswordHasher.Hash("Carrier123!"),
        //        CarrierId = carrier.Id
        //    };

        //    await context.CarrierUsers.AddAsync(carrierUser);
        //    await context.SaveChangesAsync();
        //}
        #endregion
    }
    #region fakedata
    //public static async Task SeedFakeDataAsync(EnakliyatDbContext context)
    //{
    //    // Kullanıcılar (normal)
    //    if (!context.Users.Any(u => !u.IsAdmin))
    //    {
    //        var fakeUsers = new List<User>();
    //        for (int i = 1; i <= 60; i++)
    //        {
    //            fakeUsers.Add(new User
    //            {
    //                Name = $"Kullanıcı {i}",
    //                PhoneNumber = $"+90 555 000 {i:0000}",
    //                Email = $"user{i}@roadofhome.test",
    //                Password = PasswordHasher.Hash("User123!"),
    //                IsAdmin = false
    //            });
    //        }

    //        await context.Users.AddRangeAsync(fakeUsers);
    //        await context.SaveChangesAsync();
    //    }

    //    // 50 nakliyeci
    //    if (context.Carriers.Count() < 50)
    //    {
    //        var existingCarrierCount = context.Carriers.Count();
    //        var carriersToCreate = 50 - existingCarrierCount;

    //        // Rastgele district'ler al
    //        var districts = await context.Districts
    //            .Include(d => d.City)
    //            .Where(d => d.City.Name == "İstanbul" || d.City.Name == "Ankara" || d.City.Name == "İzmir")
    //            .ToListAsync();
    //        var random = new Random();

    //        var newCarriers = new List<Carrier>();
    //        for (int i = 1; i <= carriersToCreate; i++)
    //        {
    //            int index = existingCarrierCount + i;
    //            var district = districts.Any() ? districts[random.Next(districts.Count)] : null;

    //            newCarriers.Add(new Carrier
    //            {
    //                Name = $"Nakliyeci {index}",
    //                CompanyName = $"Road of Home Taşımacılık #{index}",
    //                PhoneNumber = $"+90 532 000 {index:0000}",
    //                LandlinePhone = $"+90 212 {random.Next(200, 999)} {random.Next(1000, 9999)}",
    //                Email = $"carrier{index}@roadofhome.test",
    //                Website = index % 3 == 0 ? $"https://nakliyeci{index}.com" : null,
    //                DistrictId = district?.Id,
    //                LicenseNumber = $"YETKI-{index:0000}",
    //                VehicleInfo = "1 kamyon, 3 personel",
    //                ServiceAreas = "İstanbul, Ankara, İzmir",
    //                Description = "Test amaçlı eklenen demo nakliyeci.",
    //                TaxOffice = district != null ? $"{district.Name} Vergi Dairesi" : "Beşiktaş Vergi Dairesi",
    //                TaxNumber = $"{random.Next(100000000, 999999999)}",
    //                InvoiceAddress = district != null 
    //                    ? $"{district.City.Name}, {district.Name}, Demo Mahallesi, Demo Sokak No:{index}"
    //                    : "İstanbul, Beşiktaş, Demo Mahallesi, Demo Sokak No:1",
    //                IsApproved = true,
    //                IsRejected = false,
    //                IsSuspended = false,
    //                AverageRating = 4.2,
    //                ReviewCount = 0
    //            });
    //        }

    //        await context.Carriers.AddRangeAsync(newCarriers);
    //        await context.SaveChangesAsync();

    //        // Her carrier için bir CarrierUser
    //        var carrierUsers = new List<CarrierUser>();
    //        foreach (var carrier in newCarriers)
    //        {
    //            carrierUsers.Add(new CarrierUser
    //            {
    //                Email = carrier.Email ?? $"carrier{carrier.Id}@roadofhome.test",
    //                Password = PasswordHasher.Hash("Carrier123!"),
    //                CarrierId = carrier.Id
    //            });
    //        }

    //        await context.CarrierUsers.AddRangeAsync(carrierUsers);
    //        await context.SaveChangesAsync();
    //    }

    //    // İşler (MoveRequest + Offer)
    //    if (!context.MoveRequests.Any())
    //    {
    //        var moveTypes = new[] { "Ev", "Parça", "Ofis", "Depolama" };
    //        var statuses = new[] { "Yeni", "Teklif Bekliyor", "Planlandı", "Tamamlandı", "İptal Edildi" };
    //        var offerStatuses = new[] { "Beklemede", "Kabul Edildi", "Reddedildi" };

    //        var users = context.Users.Where(u => !u.IsAdmin).ToList();
    //        var carriers = context.Carriers.Take(50).ToList();

    //        if (users.Count == 0 || carriers.Count == 0)
    //        {
    //            return;
    //        }

    //        var random = new Random();
    //        var moveRequests = new List<MoveRequest>();
    //        var offers = new List<Offer>();
    //        var contracts = new List<Contract>();
    //        var payments = new List<Payment>();
    //        var reviews = new List<Review>();

    //        foreach (var carrier in carriers)
    //        {
    //            for (int i = 0; i < 10; i++)
    //            {
    //                var user = users[random.Next(users.Count)];
    //                var moveType = moveTypes[(i + carrier.Id) % moveTypes.Length];
    //                // Statüleri dengeli dağıtmak için index'e göre cycyle
    //                var status = statuses[(i + carrier.Id) % statuses.Length];
    //                var daysOffset = random.Next(-30, 30);

    //                var request = new MoveRequest
    //                {
    //                    CustomerName = user.Name != string.Empty ? user.Name : $"Müşteri {user.Id}",
    //                    PhoneNumber = string.IsNullOrWhiteSpace(user.PhoneNumber) ? "+90 555 111 1111" : user.PhoneNumber,
    //                    Email = user.Email,
    //                    FromAddress = "İstanbul, Beşiktaş",
    //                    ToAddress = "Ankara, Çankaya",
    //                    MoveDate = DateTime.UtcNow.AddDays(daysOffset),
    //                    Notes = "Demo veri - otomatik oluşturulmuş taşıma isteği.",
    //                    MoveType = moveType,
    //                    Status = status,
    //                    FromFloor = random.Next(1, 6),
    //                    FromHasElevator = random.NextDouble() > 0.5,
    //                    ToFloor = random.Next(1, 6),
    //                    ToHasElevator = random.NextDouble() > 0.5,
    //                    RoomType = moveType == "Ev" ? "3+1" : null,
    //                    UserId = user.Id
    //                };

    //                moveRequests.Add(request);

    //                var priceBase = moveType switch
    //                {
    //                    "Ev" => 6000,
    //                    "Parça" => 1500,
    //                    "Ofis" => 8000,
    //                    "Depolama" => 2500,
    //                    _ => 5000
    //                };

    //                var offer = new Offer
    //                {
    //                    CarrierId = carrier.Id,
    //                    MoveRequest = request,
    //                    Price = priceBase + random.Next(0, 3000),
    //                    Note = "Demo teklif - sistem testi için hazırlanmıştır.",
    //                    Status = offerStatuses[random.Next(offerStatuses.Length)]
    //                };

    //                offers.Add(offer);

    //                // Tamamlanan işlere sözleşme ve %10 kapora ödemesi ekle
    //                if (status == "Tamamlandı")
    //                {
    //                    var contract = new Contract
    //                    {
    //                        MoveRequest = request,
    //                        Offer = offer,
    //                        ContractNumber = $"CNT-{request.Id}-{carrier.Id}",
    //                        IsInsuranceIncluded = random.NextDouble() > 0.3,
    //                        InsuranceCompany = "Demo Sigorta",
    //                        PolicyNumber = $"POL-{random.Next(100000, 999999)}",
    //                        CoverageDescription = "Demo taşınma sigortası",
    //                        CoverageAmount = offer.Price * 2
    //                    };
    //                    contracts.Add(contract);

    //                    var payment = new Payment
    //                    {
    //                        Contract = contract,
    //                        Amount = Math.Round(offer.Price * 0.10m, 2),
    //                        Currency = "TRY",
    //                        Status = PaymentStatus.Paid,
    //                        Method = PaymentMethod.Card,
    //                        ExternalReference = $"DEMO-{Guid.NewGuid():N}".Substring(0, 20)
    //                    };
    //                    payments.Add(payment);
    //                }

    //                // Bazı tamamlanan işler için kullanıcı yorumu oluşturalım
    //                if (status == "Tamamlandı" && random.NextDouble() > 0.3)
    //                {
    //                    var rating = random.Next(4, 6); // 4-5 arası
    //                    var commentOptions = new[]
    //                    {
    //                        "Ekip zamanında geldi, eşyalar sorunsuz taşındı.",
    //                        "İletişim ve paketleme çok iyiydi, tavsiye ederim.",
    //                        "Fiyat / performans olarak oldukça memnun kaldım.",
    //                        "Ufak tefek aksaklıklar dışında genel olarak iyiydi.",
    //                        "Beklentimi karşıladı, tekrar taşınsam yine çalışırım."
    //                    };
    //                    var review = new Review
    //                    {
    //                        MoveRequest = request,
    //                        CarrierId = carrier.Id,
    //                        UserId = user.Id,
    //                        Rating = rating,
    //                        Comment = commentOptions[random.Next(commentOptions.Length)]
    //                    };
    //                    reviews.Add(review);
    //                }
    //            }
    //        }

    //        await context.MoveRequests.AddRangeAsync(moveRequests);
    //        await context.Offers.AddRangeAsync(offers);
    //        if (contracts.Any())
    //        {
    //            await context.Contracts.AddRangeAsync(contracts);
    //        }
    //        if (payments.Any())
    //        {
    //            await context.Payments.AddRangeAsync(payments);
    //        }
    //        if (reviews.Any())
    //        {
    //            await context.Reviews.AddRangeAsync(reviews);
    //        }
    //        await context.SaveChangesAsync();

    //        // Carrier ortalama puan ve review sayısını güncelle
    //        if (reviews.Any())
    //        {
    //            var ratingsByCarrier = reviews
    //                .GroupBy(r => r.CarrierId)
    //                .Select(g => new { CarrierId = g.Key, Avg = g.Average(r => r.Rating), Count = g.Count() })
    //                .ToList();

    //            var affectedCarrierIds = ratingsByCarrier.Select(x => x.CarrierId).ToList();
    //            var affectedCarriers = context.Carriers.Where(c => affectedCarrierIds.Contains(c.Id)).ToList();

    //            foreach (var c in affectedCarriers)
    //            {
    //                var stats = ratingsByCarrier.First(x => x.CarrierId == c.Id);
    //                c.AverageRating = stats.Avg;
    //                c.ReviewCount = stats.Count;
    //            }

    //            await context.SaveChangesAsync();
    //        }
    //    }
    //}
    #endregion
    public static async Task SeedSystemSettingsAsync(EnakliyatDbContext context)
    {
        if (context.SystemSettings.Any())
        {
            return; // Zaten yüklenmiş
        }

        var settings = new List<SystemSetting>
        {
            new SystemSetting
            {
                Key = "DefaultCommissionRate",
                Value = "10",
                Description = "Varsayılan komisyon oranı (%)",
                Category = "Financial"
            },
            new SystemSetting
            {
                Key = "PlatformName",
                Value = "Road of Home",
                Description = "Platform adı",
                Category = "General"
            },
            new SystemSetting
            {
                Key = "SupportEmail",
                Value = "destek@roadofhome.com",
                Description = "Destek e-posta adresi",
                Category = "General"
            },
            new SystemSetting
            {
                Key = "SupportPhone",
                Value = "+90 532 123 45 67",
                Description = "Destek telefon numarası",
                Category = "General"
            },
            new SystemSetting
            {
                Key = "MaxFileUploadSize",
                Value = "10485760",
                Description = "Maksimum dosya yükleme boyutu (byte)",
                Category = "System"
            },
            new SystemSetting
            {
                Key = "MinPasswordLength",
                Value = "6",
                Description = "Minimum şifre uzunluğu",
                Category = "Security"
            }
        };

        await context.SystemSettings.AddRangeAsync(settings);
        await context.SaveChangesAsync();
    }

    /// <summary>Mevcut kurulumlara ana sayfa teklif hattı ayarını bir kez ekler.</summary>
    public static async Task EnsureQuoteCallHotlineSettingAsync(EnakliyatDbContext context)
    {
        if (await context.SystemSettings.AnyAsync(s => s.Key == "QuoteCallHotline"))
            return;

        await context.SystemSettings.AddAsync(new SystemSetting
        {
            Key = "QuoteCallHotline",
            Value = "+90 532 123 45 67",
            Description = "Ana sayfa teklif hattı: «Hemen Ara», WhatsApp (sağ alt) ve footer; yoksa SupportPhone kullanılır.",
            Category = "General"
        });
        await context.SaveChangesAsync();
    }

    public static async Task SeedNotificationTemplatesAsync(EnakliyatDbContext context)
    {
        if (context.NotificationTemplates.Any())
        {
            return; // Zaten yüklenmiş
        }

        var templates = new List<NotificationTemplate>
        {
            new NotificationTemplate
            {
                Name = "Yeni Teklif E-posta",
                Type = "Email",
                EventType = "NewOffer",
                Subject = "Yeni Teklifiniz Var - Talep No: {MoveRequestId}",
                Body = "<h2>Merhaba {CustomerName},</h2><p>Talep numaranız {MoveRequestId} için yeni bir teklif aldınız.</p><p>Teklif detaylarını görmek için <a href='{OfferLink}'>buraya tıklayın</a>.</p>",
                IsActive = true,
                Variables = "MoveRequestId,CustomerName,OfferLink"
            },
            new NotificationTemplate
            {
                Name = "Teklif Kabul Edildi E-posta",
                Type = "Email",
                EventType = "OfferAccepted",
                Subject = "Teklifiniz Kabul Edildi - Talep No: {MoveRequestId}",
                Body = "<h2>Merhaba {CarrierName},</h2><p>{MoveRequestId} numaralı talep için sunduğunuz teklif kabul edildi!</p><p>Rezervasyon detaylarını görmek için <a href='{ReservationLink}'>buraya tıklayın</a>.</p>",
                IsActive = true,
                Variables = "MoveRequestId,CarrierName,ReservationLink"
            },
            new NotificationTemplate
            {
                Name = "Rezervasyon Onayı E-posta",
                Type = "Email",
                EventType = "ReservationConfirmed",
                Subject = "Rezervasyonunuz Onaylandı - Talep No: {MoveRequestId}",
                Body = "<h2>Merhaba {CustomerName},</h2><p>Rezervasyonunuz onaylandı. Taşınma tarihiniz: {MoveDate}</p><p>Detayları görmek için <a href='{ReservationLink}'>buraya tıklayın</a>.</p>",
                IsActive = true,
                Variables = "MoveRequestId,CustomerName,MoveDate,ReservationLink"
            }
        };

        await context.NotificationTemplates.AddRangeAsync(templates);
        await context.SaveChangesAsync();
    }

    public static async Task SeedAddressesAsync(EnakliyatDbContext context, IWebHostEnvironment env)
    {
        if (context.Cities.Any())
        {
            return; // Zaten yüklenmiş
        }

        var root = Path.Combine(env.WebRootPath, "turkiye-adresler-json-main");
        if (!Directory.Exists(root))
        {
            return;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Şehirler
        var citiesJson = await File.ReadAllTextAsync(Path.Combine(root, "sehirler.json"));
        var cityDtos = JsonSerializer.Deserialize<List<CityDto>>(citiesJson, jsonOptions) ?? new();

        var cityMap = new Dictionary<string, City>();
        foreach (var dto in cityDtos)
        {
            var city = new City
            {
                Name = dto.sehir_adi
            };
            context.Cities.Add(city);
            cityMap[dto.sehir_id] = city;
        }

        await context.SaveChangesAsync();

        // İlçeler
        var districtsJson = await File.ReadAllTextAsync(Path.Combine(root, "ilceler.json"));
        var districtDtos = JsonSerializer.Deserialize<List<DistrictDto>>(districtsJson, jsonOptions) ?? new();

        var districtMap = new Dictionary<string, District>();
        foreach (var dto in districtDtos)
        {
            if (!cityMap.TryGetValue(dto.sehir_id, out var city))
            {
                continue;
            }

            var district = new District
            {
                Name = dto.ilce_adi,
                CityId = city.Id
            };
            context.Districts.Add(district);
            districtMap[dto.ilce_id] = district;
        }

        await context.SaveChangesAsync();

        // Mahalleler (parçalı dosyalar)
        var neighborhoodFiles = Directory.GetFiles(root, "mahalleler-*.json");
        foreach (var file in neighborhoodFiles)
        {
            var text = await File.ReadAllTextAsync(file);
            var mahalleDtos = JsonSerializer.Deserialize<List<NeighborhoodDto>>(text, jsonOptions) ?? new();

            foreach (var dto in mahalleDtos)
            {
                if (!districtMap.TryGetValue(dto.ilce_id, out var district))
                {
                    continue;
                }

                var neighborhood = new Neighborhood
                {
                    Name = dto.mahalle_adi,
                    DistrictId = district.Id
                };
                context.Neighborhoods.Add(neighborhood);
            }

            await context.SaveChangesAsync();
        }
    }

    public static async Task SeedDefaultAddOnServicesAsync(EnakliyatDbContext context)
    {
        const string sadeceAracName = "Sadece Araç";
        if (await context.AddOnServices.AnyAsync(a => a.Name == sadeceAracName))
        {
            return;
        }

        await context.AddOnServices.AddAsync(new AddOnService
        {
            Name = sadeceAracName,
            Description = "Yükleme-boşaltma ve ek personel olmadan yalnızca araç temini.",
            IsActive = true,
            PricingType = AddOnPricingType.Included,
            DefaultPrice = null
        });
        await context.SaveChangesAsync();
    }

    private record CityDto(string sehir_id, string sehir_adi);
    private record DistrictDto(string ilce_id, string ilce_adi, string sehir_id, string sehir_adi);
    private record NeighborhoodDto(string mahalle_id, string mahalle_adi, string ilce_id, string ilce_adi, string sehir_id, string sehir_adi);
}
