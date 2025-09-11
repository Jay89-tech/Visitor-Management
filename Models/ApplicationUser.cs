using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace JobTrackerApp.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(200)]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(100)]
        public string State { get; set; } = string.Empty;

        [StringLength(20)]
        public string ZipCode { get; set; } = string.Empty;

        public DateTime DateOfBirth { get; set; }

        [StringLength(200)]
        public string ProfilePictureUrl { get; set; } = string.Empty;

        [StringLength(500)]
        public string Bio { get; set; } = string.Empty;

        [StringLength(200)]
        public string LinkedInProfile { get; set; } = string.Empty;

        [StringLength(200)]
        public string GitHubProfile { get; set; } = string.Empty;

        [StringLength(200)]
        public string PortfolioUrl { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastLoginDate { get; set; } = DateTime.UtcNow;

        // User role/type
        public UserRole Role { get; set; } = UserRole.JobSeeker;

        // Navigation properties
        public virtual ICollection<Application> Applications { get; set; } = new List<Application>();

        // Computed properties
        public string FullName => $"{FirstName} {LastName}".Trim();

        public int TotalApplications => Applications?.Count ?? 0;

        public int PendingApplications => Applications?.Count(a =>
            a.Status == ApplicationStatus.Submitted ||
            a.Status == ApplicationStatus.UnderReview) ?? 0;

        public int AcceptedApplications => Applications?.Count(a =>
            a.Status == ApplicationStatus.Accepted) ?? 0;

        public double SuccessRate => TotalApplications > 0 ?
            (double)AcceptedApplications / TotalApplications * 100 : 0;
    }

    public enum UserRole
    {
        JobSeeker = 1,
        Recruiter = 2,
        Admin = 3
    }
}