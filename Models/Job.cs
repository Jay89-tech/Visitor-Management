using System.ComponentModel.DataAnnotations;

namespace JobTrackerApp.Models
{
    public class Job
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Company { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(100)]
        public string Location { get; set; } = string.Empty;

        [Range(0, double.MaxValue)]
        public decimal? Salary { get; set; }

        public JobStatus Status { get; set; } = JobStatus.Open;

        public DateTime DatePosted { get; set; } = DateTime.UtcNow;

        public DateTime? ApplicationDeadline { get; set; }

        [StringLength(500)]
        public string Requirements { get; set; } = string.Empty;

        [StringLength(100)]
        public string JobType { get; set; } = string.Empty; // Full-time, Part-time, Contract

        [StringLength(100)]
        public string ExperienceLevel { get; set; } = string.Empty; // Entry, Mid, Senior

        public bool IsRemote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Application> Applications { get; set; } = new List<Application>();

        // Computed properties
        public int ApplicationCount => Applications?.Count ?? 0;

        public bool IsDeadlinePassed => ApplicationDeadline.HasValue && ApplicationDeadline < DateTime.UtcNow;
    }

    public enum JobStatus
    {
        Open = 1,
        Closed = 2,
        OnHold = 3,
        Filled = 4
    }
}