using JobTrackerApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace JobTrackerApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Job> Jobs { get; set; }
        public DbSet<Application> Applications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Job entity configuration
            builder.Entity<Job>(entity =>
            {
                entity.HasKey(j => j.Id);

                entity.Property(j => j.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(j => j.Company)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(j => j.Salary)
                    .HasPrecision(18, 2);

                entity.Property(j => j.Status)
                    .HasConversion<int>();

                entity.HasIndex(j => j.Company);
                entity.HasIndex(j => j.Status);
                entity.HasIndex(j => j.DatePosted);

                // Configure relationship with Applications
                entity.HasMany(j => j.Applications)
                    .WithOne(a => a.Job)
                    .HasForeignKey(a => a.JobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Application entity configuration
            builder.Entity<Application>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.Property(a => a.ApplicantName)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(a => a.Email)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(a => a.Status)
                    .HasConversion<int>();

                entity.HasIndex(a => a.Status);
                entity.HasIndex(a => a.AppliedDate);
                entity.HasIndex(a => a.Email);

                // Configure relationship with User
                entity.HasOne(a => a.User)
                    .WithMany(u => u.Applications)
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure relationship with Job
                entity.HasOne(a => a.Job)
                    .WithMany(j => j.Applications)
                    .HasForeignKey(a => a.JobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ApplicationUser entity configuration
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(u => u.FirstName)
                    .HasMaxLength(100);

                entity.Property(u => u.LastName)
                    .HasMaxLength(100);

                entity.Property(u => u.Role)
                    .HasConversion<int>();

                entity.HasIndex(u => u.Role);
                entity.HasIndex(u => u.IsActive);
            });

            // Seed initial data
            SeedData(builder);
        }

        private void SeedData(ModelBuilder builder)
        {
            // Seed job statuses and application statuses are handled by enums

            // You can add seed data for roles, default users, or sample jobs here
            builder.Entity<Job>().HasData(
                new Job
                {
                    Id = 1,
                    Title = "Software Developer",
                    Company = "Tech Corp",
                    Description = "Looking for a skilled software developer",
                    Location = "Remote",
                    Salary = 75000,
                    Status = JobStatus.Open,
                    DatePosted = DateTime.UtcNow.AddDays(-5),
                    JobType = "Full-time",
                    ExperienceLevel = "Mid-level",
                    IsRemote = true,
                    Requirements = "C#, .NET, SQL Server experience required"
                },
                new Job
                {
                    Id = 2,
                    Title = "Frontend Developer",
                    Company = "Web Solutions Inc",
                    Description = "React and TypeScript developer needed",
                    Location = "New York, NY",
                    Salary = 80000,
                    Status = JobStatus.Open,
                    DatePosted = DateTime.UtcNow.AddDays(-3),
                    JobType = "Full-time",
                    ExperienceLevel = "Senior",
                    IsRemote = false,
                    Requirements = "React, TypeScript, HTML, CSS experience required"
                }
            );
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is Job || e.Entity is Application)
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity is Job job)
                    {
                        job.CreatedAt = DateTime.UtcNow;
                        job.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (entry.Entity is Application app)
                    {
                        app.CreatedAt = DateTime.UtcNow;
                        app.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (entry.Entity is Job job)
                    {
                        job.UpdatedAt = DateTime.UtcNow;
                    }
                    else if (entry.Entity is Application app)
                    {
                        app.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
        }
    }
}