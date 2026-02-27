using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskOverflow.Models;

namespace TaskOverflow.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<TaskItem> Tasks { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure TaskItem entity
            builder.Entity<TaskItem>(entity =>
            {
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Description)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(t => t.Category)
                    .HasConversion<int>(); // Store enum as int

                entity.Property(t => t.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");

                // Default sort order for drag-and-drop
                entity.Property(t => t.SortOrder)
                    .HasDefaultValue(0);

                // Relationship with user
                entity.HasOne(t => t.User)
                    .WithMany(u => u.Tasks)
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes for better query performance
                entity.HasIndex(t => t.UserId);
                entity.HasIndex(t => t.IsCompleted);
                entity.HasIndex(t => t.DueDate);
                entity.HasIndex(t => t.SortOrder);
            });
        }
    }
}