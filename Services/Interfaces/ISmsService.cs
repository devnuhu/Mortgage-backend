using System.Threading.Tasks;

namespace MMGT.API.Services.Interfaces
{
    public interface ISmsService
    {
        /// <summary>
        /// Send SMS notification to borrower
        /// Handles phone number formatting and SMS database insertion
        /// </summary>
        /// <param name="phoneNumber">Borrower phone number (may start with +, 0, or 234)</param>
        /// <param name="message">SMS message content</param>
        /// <param name="applicationNumber">Application number for reference/logging</param>
        /// <returns>True if SMS was queued successfully, false otherwise</returns>
        Task<bool> SendSmsAsync(string phoneNumber, string message, string applicationNumber);

        /// <summary>
        /// Format phone number according to SMS provider requirements
        /// Rules:
        /// - Remove leading "+"
        /// - Convert leading "0" to "234"
        /// - If already starts with "234", keep as is
        /// </summary>
        /// <param name="phoneNumber">Raw phone number</param>
        /// <returns>Formatted phone number or empty string if invalid</returns>
        string FormatPhoneNumber(string phoneNumber);
    }
}
