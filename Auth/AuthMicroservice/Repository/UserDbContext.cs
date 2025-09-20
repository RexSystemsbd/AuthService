using AuthMicroservice.Model;
using Microsoft.EntityFrameworkCore;

namespace AuthMicroservice.Repository
{
    public class UserDbContext : DbContext
    {
        private readonly IConfiguration _configuration;

        //public UserDbContext(IConfiguration configuration)
        //{
        //    _configuration = configuration;
        //}
        public UserDbContext(DbContextOptions<UserDbContext> options, IConfiguration configuration)
       : base(options)
        {
            _configuration = configuration;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<UserRole>userRoles { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Subscriber> Subscribers { get; set; }
        public DbSet<EmailHistory> EmailHistories { get; set; }
        public DbSet<SmtpConfig> SmtpConfigs { get; set; }


        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    var connectionString = _configuration.GetConnectionString("DefaultConnection");
        //    optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)));
        //}
        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    base.OnModelCreating(modelBuilder);
        //}
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Ensure OnConfiguring is not used if options are passed in constructor
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entity-specific settings here, if needed
            // For example, configuring table names or relationships
        }
        public override int SaveChanges()
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedDate = DateTime.UtcNow;
                }
            }

            return base.SaveChanges();
        }
    }
}
