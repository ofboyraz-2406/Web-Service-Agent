using Microsoft.EntityFrameworkCore;
using WebService.Models;

namespace WebService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Definition> Definitions => Set<Definition>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<ScheduleManager> ScheduleManagers => Set<ScheduleManager>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Definition>().ToTable("DEFINITIONS");
        modelBuilder.Entity<Operation>().ToTable("OPERATIONS");
        modelBuilder.Entity<ScheduleManager>().ToTable("SCHEDULEMANAGER");

        modelBuilder.Entity<Definition>().Property(x => x.ColumnMap).HasColumnName("ColumnMap");
        modelBuilder.Entity<Definition>().Property(x => x.ParseType).HasColumnName("ParseType");
    }
}
