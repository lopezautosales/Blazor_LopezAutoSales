using Duende.IdentityServer.EntityFramework.Extensions;
using Duende.IdentityServer.EntityFramework.Options;
using LopezAutoSales.Server.Models;
using LopezAutoSales.Shared;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.ApiAuthorization.IdentityServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LopezAutoSales.Server
{
    public class ApplicationDbContext : ApiAuthorizationDbContext<ApplicationUser>
    {
        public DbSet<Sale> Sales { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Car> Cars { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<Picture> Pictures { get; set; }
        public DbSet<Lienholder> Lienholders { get; set; }
        private OperationalStoreOptions OperationalStoreOptions { get; }

        public ApplicationDbContext(
            DbContextOptions options,
            IOptions<OperationalStoreOptions> operationalStoreOptions) : base(options, operationalStoreOptions)
        {
            OperationalStoreOptions = operationalStoreOptions.Value;
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<IdentityRole>().HasData(new IdentityRole
            {
                Id = "2301D884-221A-4E7D-B509-0113DCC043E1",
                Name = "Admin",
                NormalizedName = "ADMIN"
            });

            Dealership.Address.Id = 1;
            Lienholder dealership = new Lienholder
            {
                Name = Dealership.Name,
                AddressId = 1,
                NormalizedName = Dealership.Name.ToUpper()
            };
            builder.Entity<Car>().HasIndex(x => x.IsListed);
            builder.Entity<UserAccount>().HasKey(x => new { x.UserId, x.AccountId });
            builder.Entity<Address>().HasData(Dealership.Address);
            builder.Entity<Lienholder>().HasData(dealership);
            builder.ConfigurePersistedGrantContext(OperationalStoreOptions);
            base.OnModelCreating(builder);
        }
    }
}
