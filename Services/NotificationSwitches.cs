namespace MMGT.API.Services
{
    /// <summary>Switches read once at start-up from configuration (see Program.cs).</summary>
    public static class NotificationSwitches
    {
        /// <summary>Notifications:EmailOnResubmission. Default false: processors get an in-app message, not an email.</summary>
        public static bool EmailOnResubmission { get; set; } = false;
    }
}