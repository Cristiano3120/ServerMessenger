using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Server_Messenger.PersonalDataDb
{
    internal sealed class PersonalDataDbContext : DbContext, IDesignTimeDbContextFactory<PersonalDataDbContext>
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Relationships> Relationships { get; set; }

        public PersonalDataDbContext() { }

        public PersonalDataDbContext(DbContextOptions<PersonalDataDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Relationships>()
                .HasKey(x => new { x.SenderId, x.ReceiverId });
        }

        public PersonalDataDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PersonalDataDbContext>();

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(Server.GetDynamicPath("Settings/appsettings.json"))
                .Build();

            optionsBuilder.UseNpgsql(configuration.GetConnectionString("PersonalDataDatabase"));

            return new PersonalDataDbContext(optionsBuilder.Options);
        }
    }
}
