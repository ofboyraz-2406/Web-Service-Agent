using Microsoft.EntityFrameworkCore;

namespace WebService.Data;

public class DataDbContext : DbContext
{
    public DataDbContext(DbContextOptions<DataDbContext> options) : base(options)
    {
    }
}