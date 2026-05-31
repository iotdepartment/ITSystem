using Microsoft.EntityFrameworkCore;
namespace ITSystem.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<Categorias> Categorias { get; set; }

        public DbSet<Areas> Areas { get; set; }

        public DbSet<Usuarios> Usuarios { get; set; }

        public DbSet<Prioridades> Prioridades { get; set; }

        public DbSet<Subcategorias> Subcategorias { get; set; }

        public DbSet<Tickets> Tickets { get; set; }
    }

}
