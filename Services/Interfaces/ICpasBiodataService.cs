namespace MMGT.API.Services.Interfaces
{
    /// <summary>Name and phone number held for an RSA PIN in the CPAS pension system.</summary>
    public sealed record CpasBiodata(string FirstName, string? MiddleName, string LastName, string? PhoneNumber);

    public interface ICpasBiodataService
    {
        /// <summary>
        /// Looks the RSA PIN up in CPAS. Throws BusinessRuleException (with a message safe to show the user)
        /// when CPAS cannot be reached, answers with an error, or has no usable record for the PIN.
        /// </summary>
        Task<CpasBiodata> GetBiodataAsync(string rsaPin, CancellationToken cancellationToken = default);
    }
}