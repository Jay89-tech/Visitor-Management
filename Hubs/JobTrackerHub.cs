using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using JobTrackerApp.Models;

namespace JobTrackerApp.Hubs
{
    [Authorize]
    public class JobTrackerHub : Hub
    {
        private readonly ILogger<JobTrackerHub> _logger;

        public JobTrackerHub(ILogger<JobTrackerHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            _logger.LogInformation("User {UserId} connected to JobTracker hub", userId);

            // Join user to their personal group for user-specific notifications
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
            }

            // Join all users to general notifications group
            await Groups.AddToGroupAsync(Context.ConnectionId, "AllUsers");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            _logger.LogInformation("User {UserId} disconnected from JobTracker hub", userId);

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"User_{userId}");
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "AllUsers");

            await base.OnDisconnectedAsync(exception);
        }

        // Client can join specific job groups to receive job-specific updates
        public async Task JoinJobGroup(string jobId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Job_{jobId}");
            _logger.LogInformation("User {UserId} joined job group {JobId}", Context.UserIdentifier, jobId);
        }

        public async Task LeaveJobGroup(string jobId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Job_{jobId}");
            _logger.LogInformation("User {UserId} left job group {JobId}", Context.UserIdentifier, jobId);
        }

        // Client can request real-time job statistics
        public async Task RequestJobStatistics()
        {
            // This would typically call the JobService to get current statistics
            // and send them back to the requesting client
            await Clients.Caller.SendAsync("JobStatisticsRequested");
        }

        // Send typing indicator for collaborative features (if implemented)
        public async Task SendTypingIndicator(string area)
        {
            var userId = Context.UserIdentifier;
            await Clients.Others.SendAsync("UserTyping", userId, area);
        }
    }

    // Service for sending real-time notifications
    public interface INotificationService
    {
        Task SendJobCreatedNotification(Job job);
        Task SendJobUpdatedNotification(Job job);
        Task SendJobDeletedNotification(int jobId);
        Task SendApplicationCreatedNotification(Application application);
        Task SendApplicationStatusChangedNotification(Application application);
        Task SendUserNotification(string userId, string message, string type = "info");
        Task SendGlobalNotification(string message, string type = "info");
    }

    public class NotificationService : INotificationService
    {
        private readonly IHubContext<JobTrackerHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IHubContext<JobTrackerHub> hubContext, ILogger<NotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task SendJobCreatedNotification(Job job)
        {
            try
            {
                var notification = new
                {
                    Type = "JobCreated",
                    JobId = job.Id,
                    Title = job.Title,
                    Company = job.Company,
                    Salary = job.Salary,
                    Location = job.Location,
                    IsRemote = job.IsRemote,
                    DatePosted = job.DatePosted,
                    Message = $"New job posted: {job.Title} at {job.Company}"
                };

                await _hubContext.Clients.Group("AllUsers").SendAsync("JobCreated", notification);
                _logger.LogInformation("Job created notification sent for job: {JobTitle}", job.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending job created notification");
            }
        }

        public async Task SendJobUpdatedNotification(Job job)
        {
            try
            {
                var notification = new
                {
                    Type = "JobUpdated",
                    JobId = job.Id,
                    Title = job.Title,
                    Company = job.Company,
                    Status = job.Status.ToString(),
                    UpdatedAt = job.UpdatedAt,
                    Message = $"Job updated: {job.Title} at {job.Company}"
                };

                // Send to all users and job-specific group
                await _hubContext.Clients.Group("AllUsers").SendAsync("JobUpdated", notification);
                await _hubContext.Clients.Group($"Job_{job.Id}").SendAsync("JobUpdated", notification);

                _logger.LogInformation("Job updated notification sent for job: {JobTitle}", job.Title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending job updated notification");
            }
        }

        public async Task SendJobDeletedNotification(int jobId)
        {
            try
            {
                var notification = new
                {
                    Type = "JobDeleted",
                    JobId = jobId,
                    Message = $"Job with ID {jobId} has been deleted"
                };

                await _hubContext.Clients.Group("AllUsers").SendAsync("JobDeleted", notification);
                await _hubContext.Clients.Group($"Job_{jobId}").SendAsync("JobDeleted", notification);

                _logger.LogInformation("Job deleted notification sent for job ID: {JobId}", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending job deleted notification");
            }
        }

        public async Task SendApplicationCreatedNotification(Application application)
        {
            try
            {
                var notification = new
                {
                    Type = "ApplicationCreated",
                    ApplicationId = application.Id,
                    JobId = application.JobId,
                    JobTitle = application.Job?.Title,
                    ApplicantName = application.ApplicantName,
                    AppliedDate = application.AppliedDate,
                    Message = $"New application received for {application.Job?.Title} from {application.ApplicantName}"
                };

                // Send to job-specific group and recruiters
                await _hubContext.Clients.Group($"Job_{application.JobId}").SendAsync("ApplicationCreated", notification);

                _logger.LogInformation("Application created notification sent for application: {ApplicationId}", application.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending application created notification");
            }
        }

        public async Task SendApplicationStatusChangedNotification(Application application)
        {
            try
            {
                var notification = new
                {
                    Type = "ApplicationStatusChanged",
                    ApplicationId = application.Id,
                    JobId = application.JobId,
                    JobTitle = application.Job?.Title,
                    ApplicantName = application.ApplicantName,
                    Status = application.Status.ToString(),
                    UpdatedAt = application.UpdatedAt,
                    Message = $"Application status changed to {application.Status} for {application.Job?.Title}"
                };

                // Send to the specific user and job group
                await _hubContext.Clients.Group($"User_{application.UserId}").SendAsync("ApplicationStatusChanged", notification);
                await _hubContext.Clients.Group($"Job_{application.JobId}").SendAsync("ApplicationStatusChanged", notification);

                _logger.LogInformation("Application status changed notification sent for application: {ApplicationId}", application.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending application status changed notification");
            }
        }

        public async Task SendUserNotification(string userId, string message, string type = "info")
        {
            try
            {
                var notification = new
                {
                    Type = "UserNotification",
                    Message = message,
                    NotificationType = type,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.Group($"User_{userId}").SendAsync("UserNotification", notification);
                _logger.LogInformation("User notification sent to user: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending user notification");
            }
        }

        public async Task SendGlobalNotification(string message, string type = "info")
        {
            try
            {
                var notification = new
                {
                    Type = "GlobalNotification",
                    Message = message,
                    NotificationType = type,
                    Timestamp = DateTime.UtcNow
                };

                await _hubContext.Clients.Group("AllUsers").SendAsync("GlobalNotification", notification);
                _logger.LogInformation("Global notification sent: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending global notification");
            }
        }
    }
}