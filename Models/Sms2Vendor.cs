using System;
using System.ComponentModel.DataAnnotations;

namespace MMGT.API.Models
{
    /// <summary>
    /// SMS2Vendor table model for SMSNotification Database
    /// Represents SMS messages queued for sending by the SMS vendor
    /// </summary>
    public class Sms2Vendor
    {
        /// <summary>
        /// Sender ID - Fixed value "ACCESS PFA"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string SenderID { get; set; } = "ACCESS PFA";

        /// <summary>
        /// Formatted phone number (e.g., 2348155665292)
        /// Format: Nigerian format starting with 234
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string PhoneNo { get; set; } = string.Empty;

        /// <summary>
        /// SMS message content
        /// </summary>
        [Required]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Send status - null when first inserted
        /// Will be updated by SMS vendor processing system
        /// </summary>
        [MaxLength(50)]
        public string? SendStatus { get; set; }
    }
}
