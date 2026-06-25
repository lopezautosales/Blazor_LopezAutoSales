using LopezAutoSales.Server.Models;
using LopezAutoSales.Shared;
using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LopezAutoSales.Server
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Sale> Sales { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Car> Cars { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<Picture> Pictures { get; set; }
        public DbSet<Lienholder> Lienholders { get; set; }

        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<IdentityRole>().HasData(new IdentityRole
            {
                Id = "2301D884-221A-4E7D-B509-0113DCC043E1",
                Name = "Admin",
                NormalizedName = "ADMIN",
                // Pinned so the seed is deterministic; IdentityRole otherwise generates a
                // random ConcurrencyStamp each model build, which breaks migrations.
                ConcurrencyStamp = "2301d884-221a-4e7d-b509-0113dcc043e1"
            });

            // Seed from a fresh instance instead of mutating the shared static Dealership.Address.
            Address dealershipAddress = new Address
            {
                Id = 1,
                Street = Dealership.Address.Street,
                City = Dealership.Address.City,
                State = Dealership.Address.State,
                ZIP = Dealership.Address.ZIP
            };
            Lienholder dealership = new Lienholder
            {
                Name = Dealership.Name,
                AddressId = 1,
                NormalizedName = Dealership.Name.ToUpper()
            };
            builder.Entity<Car>().HasIndex(x => x.IsListed);
            builder.Entity<Sale>().HasIndex(x => x.Date);
            builder.Entity<UserAccount>().HasKey(x => new { x.UserId, x.AccountId });

            builder.Entity<Address>().HasData(dealershipAddress);
            builder.Entity<Lienholder>().HasData(dealership);
            base.OnModelCreating(builder);
        }
    }
}
