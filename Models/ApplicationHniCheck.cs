namespace MMGT.API.Models
{
    /// <summary>Audit row written when an application's HNI flag is first set or changes. The balance is never stored.</summary>
    public class ApplicationHniCheck
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public bool? IsHni { get; set; }
        public decimal Threshold { get; set; }
        public string Outcome { get; set; } = string.Empty;   // Checked, NoBalance, InvalidPin
        public string Source { get; set; } = string.Empty;    // Background, Manual
        public bool? PreviousIsHni { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public Guid? CheckedById { get; set; }
    }
}