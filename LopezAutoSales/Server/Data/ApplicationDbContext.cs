using IdentityServer4.EntityFramework.Options;
using LopezAutoSales.Server.Models;
using LopezAutoSales.Shared;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LopezAutoSales.Server.Data
{
    public class ApplicationDbContext : ApiAuthorizationDbContext<ApplicationUser>
    {
        public DbSet<Sale> Sales { get; set; }
        public DbSet<Car> Cars { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<UserSale> UserSales { get; set; }
        public DbSet<Image> Images { get; set; }

        public ApplicationDbContext(
            DbContextOptions options,
            IOptions<OperationalStoreOptions> operationalStoreOptions) : base(options, operationalStoreOptions)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            Dealership.Address.Id = 1;
            Lienholder dealership = new Lienholder
            {
                Name = Dealership.Name,
                AddressId = 1,
                Id = 1,
            };
            builder.Entity<Car>().HasKey(x => new { x.VIN, x.Date });
            builder.Entity<Car>().HasIndex(x => x.IsListed);
            builder.Entity<UserSale>().HasKey(x => new { x.UserId, x.SaleId });
            builder.Entity<Address>().HasData(Dealership.Address);
            builder.Entity<Lienholder>().HasData(dealership);
            base.OnModelCreating(builder);
        }
    }
}
