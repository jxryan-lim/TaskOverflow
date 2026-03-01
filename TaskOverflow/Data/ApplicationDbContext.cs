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
        public DbSet<SubTask> SubTasks { get; set; }

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
                    .HasConversion<int>();

                entity.Property(t => t.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(t => t.SortOrder)
                    .HasDefaultValue(0);

                // Soft delete
                entity.Property(t => t.IsDeleted)
                    .HasDefaultValue(false);

                entity.HasQueryFilter(t => !t.IsDeleted);

                // Relationship with user
                entity.HasOne(t => t.User)
                    .WithMany(u => u.Tasks)
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Subtasks relationship
                entity.HasMany(t => t.SubTasks)
                    .WithOne(st => st.TaskItem)
                    .HasForeignKey(st => st.TaskItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                entity.HasIndex(t => t.UserId);
                entity.HasIndex(t => t.IsCompleted);
                entity.HasIndex(t => t.DueDate);
                entity.HasIndex(t => t.SortOrder);
                entity.HasIndex(t => t.IsDeleted);
            });

            // Configure SubTask entity
            builder.Entity<SubTask>(entity =>
            {
                entity.HasKey(st => st.Id);

                entity.Property(st => st.Title)
                    .IsRequired()
                    .HasMaxLength(300);

                entity.Property(st => st.CreatedAt)
                    .HasDefaultValueSql("GETDATE()");

                entity.Property(st => st.SortOrder)
                    .HasDefaultValue(0);

                // Soft delete
                entity.Property(st => st.IsDeleted)
                    .HasDefaultValue(false);
                entity.HasQueryFilter(st => !st.IsDeleted);
                entity.HasIndex(st => st.IsDeleted);

                // Self-referencing relationship for nested subtasks
                entity.HasOne(st => st.ParentSubTask)
                    .WithMany(st => st.Children)
                    .HasForeignKey(st => st.ParentSubTaskId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(st => st.TaskItemId);
                entity.HasIndex(st => st.ParentSubTaskId);
                entity.HasIndex(st => st.IsCompleted);
                entity.HasIndex(st => st.SortOrder);
            });
        }
    }
}