using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using JobTrackerApp.Models;
using JobTrackerApp.Services;
using JobTrackerApp.Hubs;

namespace JobTrackerApp.Controllers
{
    [Authorize]
    public class JobsController : Controller
    {
        private readonly IJobService _jobService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<JobsController> _logger;

        public JobsController(
            IJobService jobService,
            INotificationService notificationService,
            ILogger<JobsController> logger)
        {
            _jobService = jobService;
            _notificationService = notificationService;
            _logger = logger;
        }

        // GET: Jobs
        public async Task<IActionResult> Index(string searchTerm = "", string status = "", string sortBy = "date")
        {
            try
            {
                IEnumerable<Job> jobs;

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    jobs = await _jobService.SearchJobsAsync(searchTerm);
                }
                else if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<JobStatus>(status, out var jobStatus))
                {
                    jobs = await _jobService.GetJobsByStatusAsync(jobStatus);
                }
                else
                {
                    jobs = await _jobService.GetAllJobsAsync();
                }

                // Apply sorting
                jobs = sortBy.ToLower() switch
                {
                    "title" => jobs.OrderBy(j => j.Title),
                    "company" => jobs.OrderBy(j => j.Company),
                    "salary" => jobs.OrderByDescending(j => j.Salary ?? 0),
                    "status" => jobs.OrderBy(j => j.Status),
                    _ => jobs.OrderByDescending(j => j.DatePosted)
                };

                ViewBag.SearchTerm = searchTerm;
                ViewBag.Status = status;
                ViewBag.SortBy = sortBy;
                ViewBag.JobStatuses = Enum.GetValues<JobStatus>().Select(s => new { Value = s.ToString(), Text = s.ToString() });

                return View(jobs.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading jobs index");
                TempData["ErrorMessage"] = "An error occurred while loading jobs.";
                return View(new List<Job>());
            }
        }

        // GET: Jobs/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var job = await _jobService.GetJobByIdAsync(id);
                if (job == null)
                {
                    TempData["ErrorMessage"] = "Job not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading job details for ID: {JobId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading job details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Jobs/Create
        [Authorize(Roles = "Recruiter,Admin")]
        public IActionResult Create()
        {
            var job = new Job();
            ViewBag.JobStatuses = Enum.GetValues<JobStatus>();
            return View(job);
        }

        // POST: Jobs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Recruiter,Admin")]
        public async Task<IActionResult> Create(Job job)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var createdJob = await _jobService.CreateJobAsync(job);

                    // Send real-time notification
                    await _notificationService.SendJobCreatedNotification(createdJob);

                    TempData["SuccessMessage"] = $"Job '{job.Title}' created successfully!";
                    return RedirectToAction(nameof(Details), new { id = createdJob.Id });
                }

                ViewBag.JobStatuses = Enum.GetValues<JobStatus>();
                return View(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating job: {JobTitle}", job.Title);
                ModelState.AddModelError("", "An error occurred while creating the job.");
                ViewBag.JobStatuses = Enum.GetValues<JobStatus>();
                return View(job);
            }
        }

        // GET: Jobs/Edit/5
        [Authorize(Roles = "Recruiter,Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var job = await _jobService.GetJobByIdAsync(id);
                if (job == null)
                {
                    TempData["ErrorMessage"] = "Job not found.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.JobStatuses = Enum.GetValues<JobStatus>();
                return View(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading job for edit: {JobId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the job.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Jobs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Recruiter,Admin")]
        public async Task<IActionResult> Edit(int id, Job job)
        {
            if (id != job.Id)
            {
                TempData["ErrorMessage"] = "Invalid job ID.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var updatedJob = await _jobService.UpdateJobAsync(job);

                    // Send real-time notification
                    await _notificationService.SendJobUpdatedNotification(updatedJob);

                    TempData["SuccessMessage"] = $"Job '{job.Title}' updated successfully!";
                    return RedirectToAction(nameof(Details), new { id = job.Id });
                }

                ViewBag.JobStatuses = Enum.GetValues<JobStatus>();
                return View(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating job: {JobId}", id);
                ModelState.AddModelError("", "An error occurred while updating the job.");
                ViewBag.JobStatuses = Enum.GetValues<JobStatus>();
                return View(job);
            }
        }

        // GET: Jobs/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var job = await _jobService.GetJobByIdAsync(id);
                if (job == null)
                {
                    TempData["ErrorMessage"] = "Job not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading job for delete: {JobId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the job.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Jobs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var success = await _jobService.DeleteJobAsync(id);
                if (success)
                {
                    // Send real-time notification
                    await _notificationService.SendJobDeletedNotification(id);

                    TempData["SuccessMessage"] = "Job deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Job not found.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting job: {JobId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the job.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Jobs/Statistics - Dashboard view
        [Authorize(Roles = "Recruiter,Admin")]
        public async Task<IActionResult> Statistics()
        {
            try
            {
                var statistics = await _jobService.GetJobStatisticsAsync();
                return View(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading job statistics");
                TempData["ErrorMessage"] = "An error occurred while loading statistics.";
                return RedirectToAction(nameof(Index));
            }
        }

        // AJAX: Get jobs by company
        [HttpGet]
        public async Task<IActionResult> GetJobsByCompany(string company)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(company))
                {
                    return Json(new { success = false, message = "Company name is required" });
                }

                var jobs = await _jobService.GetJobsByCompanyAsync(company);
                var jobData = jobs.Select(j => new
                {
                    id = j.Id,
                    title = j.Title,
                    company = j.Company,
                    location = j.Location,
                    salary = j.Salary,
                    status = j.Status.ToString(),
                    datePosted = j.DatePosted.ToString("yyyy-MM-dd"),
                    applicationCount = j.ApplicationCount
                });

                return Json(new { success = true, data = jobData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting jobs by company: {Company}", company);
                return Json(new { success = false, message = "An error occurred while retrieving jobs" });
            }
        }

        // AJAX: Get recent jobs
        [HttpGet]
        public async Task<IActionResult> GetRecentJobs(int days = 7)
        {
            try
            {
                var jobs = await _jobService.GetRecentJobsAsync(days);
                var jobData = jobs.Select(j => new
                {
                    id = j.Id,
                    title = j.Title,
                    company = j.Company,
                    location = j.Location,
                    salary = j.Salary,
                    status = j.Status.ToString(),
                    datePosted = j.DatePosted.ToString("yyyy-MM-dd HH:mm"),
                    applicationCount = j.ApplicationCount,
                    isRemote = j.IsRemote
                });

                return Json(new { success = true, data = jobData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent jobs for {Days} days", days);
                return Json(new { success = false, message = "An error occurred while retrieving recent jobs" });
            }
        }

        // AJAX: Quick status update
        [HttpPost]
        [Authorize(Roles = "Recruiter,Admin")]
        public async Task<IActionResult> UpdateJobStatus(int jobId, JobStatus status)
        {
            try
            {
                var job = await _jobService.GetJobByIdAsync(jobId);
                if (job == null)
                {
                    return Json(new { success = false, message = "Job not found" });
                }

                job.Status = status;
                var updatedJob = await _jobService.UpdateJobAsync(job);

                // Send real-time notification
                await _notificationService.SendJobUpdatedNotification(updatedJob);

                return Json(new
                {
                    success = true,
                    message = $"Job status updated to {status}",
                    newStatus = status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating job status: JobId={JobId}, Status={Status}", jobId, status);
                return Json(new { success = false, message = "An error occurred while updating job status" });
            }
        }
    }
}