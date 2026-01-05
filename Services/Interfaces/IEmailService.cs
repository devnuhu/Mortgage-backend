using System;
using System.Threading;
using System.Threading.Tasks;

namespace MMGT.API.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendFirstLoginLinkAsync(Guid userId, string toEmail, string token, CancellationToken ct = default);

        // Sends a notification when a new application is created
        Task SendApplicationCreatedNotificationAsync(string toEmail, Guid applicationId, string applicationNumber, string applicantName, CancellationToken ct = default);

        // Sends a notification when an application's status is updated (includes comments)
        Task SendApplicationStatusUpdatedAsync(string toEmail, Guid applicationId, string applicationNumber, string applicantName, string newStatus, string? comments, CancellationToken ct = default);
    }
}