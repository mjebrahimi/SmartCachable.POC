using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCachable
{
    public class AppDbContext : DbContext
    {
        public DbSet<CategoryEntity> Categories { get; set; }
        public DbSet<ProductEntity> Products { get; set; }
        public DbSet<IdentityTokenEntity> IdentityTokens { get; set; }
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<BusinessEntity> Businesses { get; set; }


        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            var keys = this.BeforeSaveChanges();
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            this.AfterSaveChanges(keys);
            return result;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            var keys = this.BeforeSaveChanges();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            this.AfterSaveChanges(keys);
            return result;
        }

        #region Configs
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var cascadeFKs = modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetForeignKeys())
                .Where(foreignKey => !foreignKey.IsOwnership && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
            foreach (var foreignKey in cascadeFKs)
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
        #endregion
    }

    public class CategoryEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int BusinessId { get; set; }

        public BusinessEntity Business { get; set; }
        public ICollection<ProductEntity> Products { get; set; }
    }

    public class ProductEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int CategoryId { get; set; }
        public int BusinessId { get; set; }

        public BusinessEntity Business { get; set; }
        public CategoryEntity Category { get; set; }
    }

    public class IdentityTokenEntity
    {
        public int Id { get; set; }
        public string Token { get; set; }
        public int UserId { get; set; }
        public int BusinessId { get; set; }

        public BusinessEntity Business { get; set; }
        public UserEntity User { get; set; }
    }

    public class UserEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int BusinessId { get; set; }

        public BusinessEntity Business { get; set; }
        public ICollection<IdentityTokenEntity> IdentityTokens { get; set; }
    }

    public class BusinessEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<CategoryEntity> Categories { get; set; }
        public ICollection<ProductEntity> Products { get; set; }
        public ICollection<UserEntity> Users { get; set; }
        public ICollection<IdentityTokenEntity> IdentityTokens { get; set; }
    }

    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder()
                .UseInMemoryDatabase("SmartCachableDb")
                //.UseSqlServer("Data Source=.;Initial Catalog=SmartCachableDb;Integrated Security=true")
                .Options;
            return new AppDbContext(options);
        }
    }
}
