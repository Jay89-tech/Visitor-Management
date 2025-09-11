using Job_Tracker.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobTrackerApp.Models
{
    public class Application
    {
        public int Id { get; set; }

        [Required]
        public int JobId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string ApplicantName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(1000)]
        public string CoverLetter { get; set; } = string.Empty;

        [StringLength(200)]
        public string ResumeFileName { get; set; } = string.Empty;

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

        public DateTime AppliedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedDate { get; set; }

        public DateTime? InterviewDate { get; set; }

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        [StringLength(100)]
        public string ReviewedBy { get; set; } = string.Empty;

        public int Priority { get; set; } = 1; // 1-5 priority scale

        [StringLength(200)]
        public string Source { get; set; } = string.Empty; // LinkedIn, Indeed, etc.

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("JobId")]
        public virtual Job Job { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        // Computed properties
        public int DaysSinceApplied => (DateTime.UtcNow - AppliedDate).Days;

        public bool IsRecentApplication => DaysSinceApplied <= 7;

        public string StatusColor => Status switch
        {
            ApplicationStatus.Submitted => "primary",
            ApplicationStatus.UnderReview => "warning",
            ApplicationStatus.Interview => "info",
            ApplicationStatus.Accepted => "success",
            ApplicationStatus.Rejected => "danger",
            ApplicationStatus.Withdrawn => "secondary",
            _ => "light"
        };
    }

    public enum ApplicationStatus
    {
        Submitted = 1,
        UnderReview = 2,
        Interview = 3,
        Accepted = 4,
        Rejected = 5,
        Withdrawn = 6
    }
}