using Microsoft.EntityFrameworkCore;
using test.Models;

namespace test.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Group> Groups { get; set; }

        public DbSet<Teacher> Teachers { get; set; }

        public DbSet<CuratedGroup> CuratedGroups { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CuratedGroup>()
                .HasOne(c => c.Teacher)
                .WithMany(t => t.CuratedGroups)
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CuratedGroup>()
                .HasOne(c => c.Group)
                .WithMany(g => g.CuratedGroups)
                .HasForeignKey(c => c.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}