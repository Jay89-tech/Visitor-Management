using Job_Tracker.Services;
using JobTrackerApp.Data;
using JobTrackerApp.Hubs;
using JobTrackerApp.Models;
using JobTrackerApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static System.Net.Mime.MediaTypeNames;

namespace JobTrackerApp.Controllers
{
    [Authorize]
    public class ApplicationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ApplicationsController> _logger;

        public ApplicationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService,
            ILogger<ApplicationsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: Applications
        public async Task<IActionResult> Index(string status = "", string sortBy = "date", bool myApplications = false)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                IQueryable<Application> applicationsQuery = _context.Applications
                    .Include(a => a.Job)
                    .Include(a => a.User);

                // Filter by current user if requested or if user is a job seeker
                if (myApplications || currentUser.Role == UserRole.JobSeeker)
                {
                    applicationsQuery = applicationsQuery.Where(a => a.UserId == currentUser.Id);
                }

                // Filter by status
                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ApplicationStatus>(status, out var appStatus))
                {
                    applicationsQuery = applicationsQuery.Where(a => a.Status == appStatus);
                }

                // Apply sorting
                applicationsQuery = sortBy.ToLower() switch
                {
                    "name" => applicationsQuery.OrderBy(a => a.ApplicantName),
                    "job" => applicationsQuery.OrderBy(a => a.Job.Title),
                    "company" => applicationsQuery.OrderBy(a => a.Job.Company),
                    "status" => applicationsQuery.OrderBy(a => a.Status),
                    "priority" => applicationsQuery.OrderByDescending(a => a.Priority),
                    _ => applicationsQuery.OrderByDescending(a => a.AppliedDate)
                };

                var applications = await applicationsQuery.ToListAsync();

                ViewBag.Status = status;
                ViewBag.SortBy = sortBy;
                ViewBag.MyApplications = myApplications;
                ViewBag.ApplicationStatuses = Enum.GetValues<ApplicationStatus>();
                ViewBag.IsRecruiterOrAdmin = currentUser.Role == UserRole.Recruiter || currentUser.Role == UserRole.Admin;

                return View(application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading application for edit: {ApplicationId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the application.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Applications/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Application application)
        {
            if (id != application.Id)
            {
                TempData["ErrorMessage"] = "Invalid application ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var existingApplication = await _context.Applications
                    .Include(a => a.Job)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (existingApplication == null)
                {
                    TempData["ErrorMessage"] = "Application not found.";
                    return RedirectToAction(nameof(Index));
                }

                // Check permissions
                if (currentUser.Role == UserRole.JobSeeker && existingApplication.UserId != currentUser.Id)
                {
                    TempData["ErrorMessage"] = "You don't have permission to edit this application.";
                    return RedirectToAction(nameof(Index));
                }

                if (ModelState.IsValid)
                {
                    var oldStatus = existingApplication.Status;

                    // Update properties
                    existingApplication.ApplicantName = application.ApplicantName;
                    existingApplication.Email = application.Email;
                    existingApplication.Phone = application.Phone;
                    existingApplication.CoverLetter = application.CoverLetter;
                    existingApplication.Notes = application.Notes;
                    existingApplication.Priority = application.Priority;
                    existingApplication.Source = application.Source;
                    existingApplication.UpdatedAt = DateTime.UtcNow;

                    // Only recruiters/admins can change status and review fields
                    if (currentUser.Role == UserRole.Recruiter || currentUser.Role == UserRole.Admin)
                    {
                        existingApplication.Status = application.Status;
                        existingApplication.ReviewedBy = application.ReviewedBy;
                        existingApplication.InterviewDate = application.InterviewDate;

                        if (application.Status != ApplicationStatus.Submitted && existingApplication.ReviewedDate == null)
                        {
                            existingApplication.ReviewedDate = DateTime.UtcNow;
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Send notification if status changed
                    if (oldStatus != existingApplication.Status)
                    {
                        await _notificationService.SendApplicationStatusChangedNotification(existingApplication);
                    }

                    TempData["SuccessMessage"] = "Application updated successfully!";
                    return RedirectToAction(nameof(Details), new { id = application.Id });
                }

                // Reload dropdown data
                ViewBag.Jobs = await _context.Jobs
                    .OrderBy(j => j.Company)
                    .ThenBy(j => j.Title)
                    .Select(j => new { j.Id, DisplayText = $"{j.Title} - {j.Company}" })
                    .ToListAsync();

                ViewBag.ApplicationStatuses = Enum.GetValues<ApplicationStatus>();
                ViewBag.IsRecruiterOrAdmin = currentUser.Role == UserRole.Recruiter || currentUser.Role == UserRole.Admin;

                return View(application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating application: {ApplicationId}", id);
                ModelState.AddModelError("", "An error occurred while updating the application.");

                ViewBag.Jobs = await _context.Jobs
                    .OrderBy(j => j.Company)
                    .ThenBy(j => j.Title)
                    .Select(j => new { j.Id, DisplayText = $"{j.Title} - {j.Company}" })
                    .ToListAsync();

                ViewBag.ApplicationStatuses = Enum.GetValues<ApplicationStatus>();
                ViewBag.IsRecruiterOrAdmin = currentUser?.Role == UserRole.Recruiter || currentUser?.Role == UserRole.Admin;

                return View(application);
            }
        }

        // GET: Applications/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var application = await _context.Applications
                    .Include(a => a.Job)
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (application == null)
                {
                    TempData["ErrorMessage"] = "Application not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading application for delete: {ApplicationId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the application.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Applications/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var application = await _context.Applications.FindAsync(id);
                if (application != null)
                {
                    _context.Applications.Remove(application);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Application deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Application not found.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting application: {ApplicationId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the application.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Applications/Withdraw/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(int id)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var application = await _context.Applications
                    .Include(a => a.Job)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (application == null)
                {
                    return Json(new { success = false, message = "Application not found" });
                }

                // Check if user owns this application
                if (application.UserId != currentUser.Id)
                {
                    return Json(new { success = false, message = "You don't have permission to withdraw this application" });
                }

                // Check if application can be withdrawn
                if (application.Status == ApplicationStatus.Accepted || application.Status == ApplicationStatus.Withdrawn)
                {
                    return Json(new { success = false, message = "This application cannot be withdrawn" });
                }

                application.Status = ApplicationStatus.Withdrawn;
                application.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Send notification
                await _notificationService.SendApplicationStatusChangedNotification(application);

                return Json(new { success = true, message = "Application withdrawn successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error withdrawing application: {ApplicationId}", id);
                return Json(new { success = false, message = "An error occurred while withdrawing the application" });
            }
        }

        // AJAX: Quick status update for recruiters/admins
        [HttpPost]
        [Authorize(Roles = "Recruiter,Admin")]
        public async Task<IActionResult> UpdateStatus(int id, ApplicationStatus status, string notes = "")
        {
            try
            {
                var application = await _context.Applications
                    .Include(a => a.Job)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (application == null)
                {
                    return Json(new { success = false, message = "Application not found" });
                }

                var oldStatus = application.Status;
                application.Status = status;
                application.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    application.Notes = notes;
                }

                if (status != ApplicationStatus.Submitted && application.ReviewedDate == null)
                {
                    application.ReviewedDate = DateTime.UtcNow;
                    application.ReviewedBy = User.Identity?.Name ?? "Unknown";
                }

                await _context.SaveChangesAsync();

                // Send notification if status changed
                if (oldStatus != status)
                {
                    await _notificationService.SendApplicationStatusChangedNotification(application);
                }

                return Json(new
                {
                    success = true,
                    message = $"Application status updated to {status}",
                    newStatus = status.ToString(),
                    statusColor = application.StatusColor
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating application status: ApplicationId={ApplicationId}, Status={Status}", id, status);
                return Json(new { success = false, message = "An error occurred while updating the application status" });
            }
        }

        // GET: Applications/Dashboard - Statistics and overview
        [Authorize(Roles = "Recruiter,Admin")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var statistics = new ApplicationDashboardViewModel
                {
                    TotalApplications = await _context.Applications.CountAsync(),
                    PendingApplications = await _context.Applications.CountAsync(a =>
                        a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.UnderReview),
                    InterviewApplications = await _context.Applications.CountAsync(a => a.Status == ApplicationStatus.Interview),
                    AcceptedApplications = await _context.Applications.CountAsync(a => a.Status == ApplicationStatus.Accepted),
                    RejectedApplications = await _context.Applications.CountAsync(a => a.Status == ApplicationStatus.Rejected),
                    RecentApplications = await _context.Applications
                        .Include(a => a.Job)
                        .Include(a => a.User)
                        .Where(a => a.AppliedDate >= DateTime.UtcNow.AddDays(-7))
                        .OrderByDescending(a => a.AppliedDate)
                        .Take(10)
                        .ToListAsync(),
                    TopJobs = await _context.Applications
                        .Include(a => a.Job)
                        .GroupBy(a => a.Job)
                        .Select(g => new { Job = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(5)
                        .ToDictionaryAsync(x => x.Job.Title + " - " + x.Job.Company, x => x.Count)
                };

                return View(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading application dashboard");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return RedirectToAction(nameof(Index));
            }
        }

        // AJAX: Get applications for a specific job
        [HttpGet]
        public async Task<IActionResult> GetApplicationsByJob(int jobId)
        {
            try
            {
                var applications = await _context.Applications
                    .Include(a => a.User)
                    .Where(a => a.JobId == jobId)
                    .Select(a => new
                    {
                        id = a.Id,
                        applicantName = a.ApplicantName,
                        email = a.Email,
                        phone = a.Phone,
                        status = a.Status.ToString(),
                        statusColor = a.StatusColor,
                        appliedDate = a.AppliedDate.ToString("yyyy-MM-dd HH:mm"),
                        daysSinceApplied = a.DaysSinceApplied,
                        priority = a.Priority,
                        source = a.Source
                    })
                    .OrderByDescending(a => a.appliedDate)
                    .ToListAsync();

                return Json(new { success = true, data = applications });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting applications for job: {JobId}", jobId);
                return Json(new { success = false, message = "An error occurred while retrieving applications" });
            }
        }
    }

    // ViewModel for Dashboard
    public class ApplicationDashboardViewModel
    {
        public int TotalApplications { get; set; }
        public int PendingApplications { get; set; }
        public int InterviewApplications { get; set; }
        public int AcceptedApplications { get; set; }
        public int RejectedApplications { get; set; }
        public List<Application> RecentApplications { get; set; } = new();
        public Dictionary<string, int> TopJobs { get; set; } = new();
    }
}
Status > ()
                    .Select(s => new { Value = s.ToString(), Text = s.ToString() });
ViewBag.IsRecruiterOrAdmin = currentUser.Role == UserRole.Recruiter || currentUser.Role == UserRole.Admin;

return View(applications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading applications index");
TempData["ErrorMessage"] = "An error occurred while loading applications.";
return View(new List<Application>());
            }
        }
        
        // GET: Applications/Details/5
        public async Task<IActionResult> Details(int id)
{
    try
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var application = await _context.Applications
            .Include(a => a.Job)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null)
        {
            TempData["ErrorMessage"] = "Application not found.";
            return RedirectToAction(nameof(Index));
        }

        // Check if user can view this application
        if (currentUser.Role == UserRole.JobSeeker && application.UserId != currentUser.Id)
        {
            TempData["ErrorMessage"] = "You don't have permission to view this application.";
            return RedirectToAction(nameof(Index));
        }

        return View(application);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading application details for ID: {ApplicationId}", id);
        TempData["ErrorMessage"] = "An error occurred while loading application details.";
        return RedirectToAction(nameof(Index));
    }
}

// GET: Applications/Create
public async Task<IActionResult> Create(int? jobId)
{
    try
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var application = new Application
        {
            UserId = currentUser.Id,
            ApplicantName = currentUser.FullName,
            Email = currentUser.Email ?? "",
            Phone = currentUser.PhoneNumber ?? ""
        };

        if (jobId.HasValue)
        {
            var job = await _context.Jobs.FindAsync(jobId.Value);
            if (job != null)
            {
                application.JobId = jobId.Value;
                ViewBag.JobTitle = job.Title;
                ViewBag.Company = job.Company;
            }
        }

        ViewBag.Jobs = await _context.Jobs
            .Where(j => j.Status == JobStatus.Open)
            .OrderBy(j => j.Company)
            .ThenBy(j => j.Title)
            .Select(j => new { j.Id, DisplayText = $"{j.Title} - {j.Company}" })
            .ToListAsync();

        ViewBag.ApplicationStatuses = Enum.GetValues<ApplicationStatus>();

        return View(application);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading create application form");
        TempData["ErrorMessage"] = "An error occurred while loading the form.";
        return RedirectToAction(nameof(Index));
    }
}

// POST: Applications/Create
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(Application application)
{
    try
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        // Ensure the application belongs to the current user
        application.UserId = currentUser.Id;
        application.AppliedDate = DateTime.UtcNow;
        application.CreatedAt = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;

        // Check if user already applied for this job
        var existingApplication = await _context.Applications
            .FirstOrDefaultAsync(a => a.JobId == application.JobId && a.UserId == currentUser.Id);

        if (existingApplication != null)
        {
            ModelState.AddModelError("", "You have already applied for this job.");
        }

        if (ModelState.IsValid)
        {
            _context.Applications.Add(application);
            await _context.SaveChangesAsync();

            // Load related data for notification
            await _context.Entry(application)
                .Reference(a => a.Job)
                .LoadAsync();

            // Send real-time notification
            await _notificationService.SendApplicationCreatedNotification(application);

            TempData["SuccessMessage"] = "Application submitted successfully!";
            return RedirectToAction(nameof(Details), new { id = application.Id });
        }

        // Reload dropdown data
        ViewBag.Jobs = await _context.Jobs
            .Where(j => j.Status == JobStatus.Open)
            .OrderBy(j => j.Company)
            .ThenBy(j => j.Title)
            .Select(j => new { j.Id, DisplayText = $"{j.Title} - {j.Company}" })
            .ToListAsync();

        ViewBag.ApplicationStatuses = Enum.GetValues<ApplicationStatus>();

        return View(application);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error creating application");
        ModelState.AddModelError("", "An error occurred while submitting the application.");

        ViewBag.Jobs = await _context.Jobs
            .Where(j => j.Status == JobStatus.Open)
            .OrderBy(j => j.Company)
            .ThenBy(j => j.Title)
            .Select(j => new { j.Id, DisplayText = $"{j.Title} - {j.Company}" })
            .ToListAsync();

        return View(application);
    }
}

// GET: Applications/Edit/5
public async Task<IActionResult> Edit(int id)
{
    try
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return Challenge();
        }

        var application = await _context.Applications
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null)
        {
            TempData["ErrorMessage"] = "Application not found.";
            return RedirectToAction(nameof(Index));
        }

        // Check permissions
        if (currentUser.Role == UserRole.JobSeeker && application.UserId != currentUser.Id)
        {
            TempData["ErrorMessage"] = "You don't have permission to edit this application.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Jobs = await _context.Jobs
            .OrderBy(j => j.Company)
            .ThenBy(j => j.Title)
            .Select(j => new { j.Id, DisplayText = $"{j.Title} - {j.Company}" })
            .ToListAsync();

        ViewBag.ApplicationStatuses = Enum.GetValues < Application