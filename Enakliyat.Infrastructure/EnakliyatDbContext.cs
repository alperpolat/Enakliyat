using Enakliyat.Domain;
using Microsoft.EntityFrameworkCore;

namespace Enakliyat.Infrastructure;

public class EnakliyatDbContext : DbContext
{
    public EnakliyatDbContext(DbContextOptions<EnakliyatDbContext> options) : base(options)
    {
    }

    public DbSet<MoveRequest> MoveRequests => Set<MoveRequest>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AddOnService> AddOnServices => Set<AddOnService>();
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<ServicePackageItem> ServicePackageItems => Set<ServicePackageItem>();
    public DbSet<MoveRequestAddOn> MoveRequestAddOns => Set<MoveRequestAddOn>();
    public DbSet<MoveRequestPhoto> MoveRequestPhotos => Set<MoveRequestPhoto>();
    public DbSet<CarrierDocument> CarrierDocuments => Set<CarrierDocument>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();
    public DbSet<CarrierUser> CarrierUsers => Set<CarrierUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MoveRequest>(entity =>
        {
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.FromAddress).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ToAddress).HasMaxLength(500).IsRequired();
            entity.Property(x => x.MoveDate).IsRequired();
            entity.Property(x => x.MoveType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RoomType).HasMaxLength(100);
            entity.Property(x => x.FromFloor);
            entity.Property(x => x.FromHasElevator);
            entity.Property(x => x.ToFloor);
            entity.Property(x => x.ToHasElevator);
            entity.Property(x => x.AssignedTeam).HasMaxLength(200);
            entity.Property(x => x.EstimatedArrivalTime);
            entity.Property(x => x.CompletedAt);
            entity.Property(x => x.AcceptedOfferId);
            entity.HasOne(x => x.User)
                  .WithMany(u => u.MoveRequests)
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SelectedPackage)
                  .WithMany()
                  .HasForeignKey(x => x.SelectedPackageId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<City>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<District>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasOne(d => d.City)
                  .WithMany(c => c.Districts)
                  .HasForeignKey(d => d.CityId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Neighborhood>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasOne(n => n.District)
                  .WithMany(d => d.Neighborhoods)
                  .HasForeignKey(n => n.DistrictId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CarrierUser>(entity =>
        {
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Password).HasMaxLength(200).IsRequired();

            entity.HasOne(cu => cu.Carrier)
                  .WithMany(c => c.Users)
                  .HasForeignKey(cu => cu.CarrierId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Carrier>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.LicenseNumber).HasMaxLength(100);
            entity.Property(x => x.VehicleInfo).HasMaxLength(200);
            entity.Property(x => x.ServiceAreas).HasMaxLength(500);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.IsApproved);
            entity.Property(x => x.IsRejected);
            entity.Property(x => x.IsSuspended);
            entity.Property(x => x.AverageRating);
            entity.Property(x => x.ReviewCount);
        });

        modelBuilder.Entity<CarrierDocument>(entity =>
        {
            entity.Property(x => x.DocumentType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.FilePath).HasMaxLength(500).IsRequired();

            entity.HasOne(d => d.Carrier)
                  .WithMany(c => c.Documents)
                  .HasForeignKey(d => d.CarrierId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.Property(x => x.Rating).IsRequired();
            entity.Property(x => x.Comment).HasMaxLength(1000);

            entity.HasOne(r => r.MoveRequest)
                  .WithMany()
                  .HasForeignKey(r => r.MoveRequestId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Carrier)
                  .WithMany()
                  .HasForeignKey(r => r.CarrierId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.User)
                  .WithMany()
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.Property(x => x.ContractNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.CoverageAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(c => c.MoveRequest)
                  .WithMany()
                  .HasForeignKey(c => c.MoveRequestId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Offer)
                  .WithMany()
                  .HasForeignKey(c => c.OfferId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.Property(x => x.ExternalReference).HasMaxLength(200);

            entity.HasOne(p => p.Contract)
                  .WithMany()
                  .HasForeignKey(p => p.ContractId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AddOnService>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.DefaultPrice).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ServicePackage>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.ApplicableMoveTypes).HasMaxLength(200);
            entity.Property(x => x.BasePrice).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ServicePackageItem>(entity =>
        {
            entity.Property(x => x.ExtraPrice).HasColumnType("decimal(18,2)");

            entity.HasOne(i => i.ServicePackage)
                  .WithMany(p => p.Items)
                  .HasForeignKey(i => i.ServicePackageId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.AddOnService)
                  .WithMany(a => a.PackageItems)
                  .HasForeignKey(i => i.AddOnServiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MoveRequestAddOn>(entity =>
        {
            entity.HasOne(m => m.MoveRequest)
                  .WithMany(r => r.AddOns)
                  .HasForeignKey(m => m.MoveRequestId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.AddOnService)
                  .WithMany()
                  .HasForeignKey(m => m.AddOnServiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MoveRequestPhoto>(entity =>
        {
            entity.Property(x => x.FilePath).HasMaxLength(500).IsRequired();

            entity.HasOne(p => p.MoveRequest)
                  .WithMany(r => r.Photos)
                  .HasForeignKey(p => p.MoveRequestId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Offer>(entity =>
        {
            entity.Property(x => x.Price).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();

            entity.HasOne(o => o.MoveRequest)
                  .WithMany() // optional: later you can add ICollection<Offer> to MoveRequest
                  .HasForeignKey(o => o.MoveRequestId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(o => o.Carrier)
                  .WithMany(c => c.Offers)
                  .HasForeignKey(o => o.CarrierId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Password).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IsAdmin).HasDefaultValue(false);
        });
    }
}
