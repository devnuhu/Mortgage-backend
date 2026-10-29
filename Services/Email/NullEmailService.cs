using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services.Email
{
    /// <summary>Used when EmailSettings:Provider is Off. Nothing is sent; callers carry on as normal.</summary>
    public class NullEmailService : IEmailService
    {
        private readonly ILogger<NullEmailService> _logger;

        public NullEmailService(ILogger<NullEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendFirstLoginLinkAsync(Guid userId, string toEmail, string token, CancellationToken ct = default)
        {
            _logger.LogInformation("Email sending is switched off (EmailSettings:Provider). Not sent: first login link.");
            return Task.CompletedTask;
        }

        public Task SendApplicationCreatedNotificationAsync(string toEmail, Guid applicationId, string applicationNumber, string applicantName, CancellationToken ct = default)
        {
            _logger.LogInformation("Email sending is switched off (EmailSettings:Provider). Not sent: new application {Number}.", applicationNumber);
            return Task.CompletedTask;
        }

        public Task SendTemporaryPasswordAsync(string toEmail, string firstName, string temporaryPassword, bool newAccount, CancellationToken ct = default)
        {
            _logger.LogInformation("Email sending is switched off (EmailSettings:Provider). Not sent: temporary password email.");
            return Task.CompletedTask;
        }

        public Task SendApplicationRejectedAsync(string toEmail, Guid applicationId, string applicationNumber, string rsaPin, string applicantName, string? reason, CancellationToken ct = default)
        {
            _logger.LogInformation("Email sending is switched off (EmailSettings:Provider). Not sent: rejection of {Number}.", applicationNumber);
            return Task.CompletedTask;
        }

        public Task SendApplicationStatusUpdatedAsync(string toEmail, Guid applicationId, string applicationNumber, string applicantName, string newStatus, string? comments, CancellationToken ct = default)
        {
            _logger.LogInformation("Email sending is switched off (EmailSettings:Provider). Not sent: status update for {Number}.", applicationNumber);
            return Task.CompletedTask;
        }
    }
}