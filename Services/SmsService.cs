using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MMGT.API.Data;
using MMGT.API.Models;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    public class SmsService : ISmsService
    {
        private readonly SmsNotificationDbContext _smsContext;
        private readonly ILogger<SmsService> _logger;

        public SmsService(SmsNotificationDbContext smsContext, ILogger<SmsService> logger)
        {
            _smsContext = smsContext;
            _logger = logger;
        }

        /// <summary>
        /// Format phone number according to SMS provider requirements
        /// Rules:
        /// - If starts with "+", remove it
        /// - If starts with "0", remove it and prepend "234"
        /// - If already starts with "234", keep as is
        /// </summary>
        public string FormatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                _logger.LogWarning("[FMT] Phone number is null or empty");
                return string.Empty;
            }

            // Remove whitespace
            string cleaned = phoneNumber.Trim();
            _logger.LogInformation("[FMT-STEP1] Raw input: '{Input}' → After trim: '{Trimmed}'", phoneNumber, cleaned);

            // Remove leading "+"
            if (cleaned.StartsWith("+"))
            {
                cleaned = cleaned.Substring(1);
                _logger.LogInformation("[FMT-STEP2] Removed leading '+': '{Result}'", cleaned);
            }

            // Convert leading "0" to "234"
            if (cleaned.StartsWith("0"))
            {
                cleaned = "234" + cleaned.Substring(1);
                _logger.LogInformation("[FMT-STEP3] Converted leading '0' to '234': '{Result}'", cleaned);
            }

            // Validate format (should start with 234 and be numeric)
            if (!cleaned.StartsWith("234"))
            {
                _logger.LogWarning("[FMT-FAIL] Formatted number does not start with '234': {Phone}", cleaned);
                return string.Empty;
            }

            if (!IsNumeric(cleaned))
            {
                _logger.LogWarning("[FMT-FAIL] Formatted number is not fully numeric: {Phone}", cleaned);
                return string.Empty;
            }

            // Validate length (Nigerian numbers should be 13 digits: 234 + 10 digits)
            if (cleaned.Length != 13)
            {
                _logger.LogWarning("[FMT-FAIL] Phone number has invalid length {Length} (expected 13): {Phone}", cleaned.Length, cleaned);
                return string.Empty;
            }

            _logger.LogInformation("[FMT-SUCCESS] Phone formatting successful: '{Original}' → '{Formatted}' (length: {Length})", phoneNumber, cleaned, cleaned.Length);
            return cleaned;
        }

        /// <summary>
        /// Send SMS notification to borrower
        /// Inserts SMS record into SMS2Vendor table in SMSNotification database
        /// </summary>
        public async Task<bool> SendSmsAsync(string phoneNumber, string message, string applicationNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    _logger.LogWarning("[SMS-INPUT] Cannot send SMS: Phone number is empty for application {ApplicationNumber}", applicationNumber);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    _logger.LogWarning("[SMS-INPUT] Cannot send SMS: Message is empty for application {ApplicationNumber}", applicationNumber);
                    return false;
                }

                _logger.LogInformation("[SMS-FORMAT] Formatting phone number for application {ApplicationNumber}. Raw input: {RawPhone}", applicationNumber, phoneNumber);

                // Format phone number
                var formattedPhone = FormatPhoneNumber(phoneNumber);

                if (string.IsNullOrEmpty(formattedPhone))
                {
                    _logger.LogWarning("[SMS-FORMAT-FAIL] Failed to format phone number: {PhoneNumber} for application {ApplicationNumber}. Returning false without sending.", phoneNumber, applicationNumber);
                    return false;
                }

                _logger.LogInformation("[SMS-FORMAT-SUCCESS] Phone formatted successfully. Input: {RawPhone} → Formatted: {FormattedPhone}", phoneNumber, formattedPhone);

                // Use raw SQL insert since SMS2Vendor table has no primary key
                var sql = "INSERT INTO SMS2Vendor (SenderID, PhoneNo, Message, SendStatus) VALUES (@senderID, @phoneNo, @message, @sendStatus)";

                _logger.LogInformation("[SMS-QUEUE] Inserting into SMS2Vendor queue for application {ApplicationNumber}. Phone: {Phone}, Message length: {MsgLen}",
                    applicationNumber, formattedPhone, message.Length);

                await _smsContext.Database.ExecuteSqlRawAsync(
                    sql,
                    new Microsoft.Data.SqlClient.SqlParameter("@senderID", "ACCESS PFA"),
                    new Microsoft.Data.SqlClient.SqlParameter("@phoneNo", formattedPhone),
                    new Microsoft.Data.SqlClient.SqlParameter("@message", message),
                    new Microsoft.Data.SqlClient.SqlParameter("@sendStatus", DBNull.Value)
                );

                _logger.LogInformation("[SMS-QUEUED] ✓ SMS queued successfully. Application: {ApplicationNumber}, Phone: {Phone}, Message length: {MessageLength}",
                    applicationNumber, formattedPhone, message.Length);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SMS-EXCEPTION] Error while sending SMS for application {ApplicationNumber}. ExceptionType: {ExType}, Message: {ExMessage}, StackTrace: {StackTrace}",
                    applicationNumber, ex.GetType().Name, ex.Message, ex.StackTrace);
                return false;
            }
        }

        /// <summary>
        /// Check if string contains only numeric characters
        /// </summary>
        private bool IsNumeric(string text)
        {
            return !string.IsNullOrEmpty(text) && text.All(char.IsDigit);
        }
    }
}
