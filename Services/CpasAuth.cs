using Microsoft.Extensions.Configuration;

namespace MMGT.API.Services
{
    /// <summary>Adds the optional CPAS auth header (CpasApi:AuthHeaderName / CpasApi:AuthHeaderValue) to a typed HttpClient. No config = no header.</summary>
    public static class CpasAuth
    {
        public static void Apply(HttpClient http, IConfiguration config)
        {
            var name = config["CpasApi:AuthHeaderName"];
            var value = config["CpasApi:AuthHeaderValue"];
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) return;
            try
            {
                http.DefaultRequestHeaders.Remove(name.Trim());
                http.DefaultRequestHeaders.TryAddWithoutValidation(name.Trim(), value.Trim());
            }
            catch { /* an invalid header name must not stop the API from starting */ }
        }
    }
}