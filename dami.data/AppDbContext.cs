using Dami.Data.Models;
using Microsoft.EntityFrameworkCore;


namespace Dami.Data;


public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Definition> Definitions => Set<Definition>();
    public DbSet<Operation> Operations => Set<Operation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>().ToTable("CUSTOMERS");
        modelBuilder.Entity<Product>().ToTable("PRODUCTS");

        modelBuilder.Entity<Customer>().HasData(
            new Customer { Id = 1, FullName = "customer1"},
            new Customer { Id = 2, FullName = "custmer2"},
            new Customer { Id = 3, FullName = "customer3"},
            new Customer { Id = 4, FullName = "customer4"},
            new Customer { Id = 5, FullName = "customer5"}
        );

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Product1"},
            new Product { Id = 2, Name = "Product2" },
            new Product { Id = 3, Name = "Product3" },
            new Product { Id = 4, Name = "Product4" },
            new Product { Id = 5, Name = "Product5" }
        );
        modelBuilder.Entity<Definition>().HasData(
    new Definition
    {
        Id = 1,
        Name = "Products SOAP - Top5",
        ServiceUrl = "https://localhost:44366/ProductsService.asmx",
        MethodName = "GetTop5Products",
        ServiceType = "SOAP",
        IsActive = true
    }
);
        modelBuilder.Entity<Operation>().ToTable("OPERATIONS");

        modelBuilder.Entity<Operation>()
            .HasOne(x => x.Definition)
            .WithMany()
            .HasForeignKey(x => x.DefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Definition>().Property(x => x.Name).IsRequired();
        modelBuilder.Entity<Definition>().Property(x => x.ServiceUrl).IsRequired();
        modelBuilder.Entity<Definition>().Property(x => x.MethodName).IsRequired();
        modelBuilder.Entity<Definition>().Property(x => x.ServiceType).IsRequired();
    }

}