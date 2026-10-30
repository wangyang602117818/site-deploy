using Microsoft.EntityFrameworkCore;

namespace SiteDeploy.Document.DBContext
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<LsOrder> LsOrders { get; set; }
    }
}
