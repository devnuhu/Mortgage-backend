using Microsoft.EntityFrameworkCore;
using MMGT.API.Data;
using MMGT.API.Models;

namespace MMGT.API.Services
{
    /// <summary>
    /// Service for managing system-wide configuration settings.
    /// SUPER_ADMIN can set key-value pairs that control system behavior.
    /// </summary>
    public interface ISystemConfigurationService
    {
        Task<string> GetConfigValueAsync(string key, string defaultValue = "");
        Task<int> GetConfigValueAsIntAsync(string key, int defaultValue = 0);
        Task<bool> GetConfigValueAsBoolAsync(string key, bool defaultValue = false);
        Task SetConfigValueAsync(string key, string value, string? description, Guid setBy);
        Task<SystemConfiguration?> GetConfigAsync(string key);
        Task<List<SystemConfiguration>> GetAllConfigsAsync();
    }

    public class SystemConfigurationService : ISystemConfigurationService
    {
        private readonly ApplicationDbContext _context;

        public SystemConfigurationService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get configuration value as string.
        /// Returns defaultValue if key not found.
        /// </summary>
        public async Task<string> GetConfigValueAsync(string key, string defaultValue = "")
        {
            var config = await GetConfigAsync(key);
            return config?.ConfigValue ?? defaultValue;
        }

        /// <summary>
        /// Get configuration value as integer.
        /// Returns defaultValue if key not found or value cannot be parsed.
        /// </summary>
        public async Task<int> GetConfigValueAsIntAsync(string key, int defaultValue = 0)
        {
            var value = await GetConfigValueAsync(key, defaultValue.ToString());
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// Get configuration value as boolean.
        /// Accepts: "true", "1", "yes" as true; "false", "0", "no" as false.
        /// Returns defaultValue if key not found or value cannot be parsed.
        /// </summary>
        public async Task<bool> GetConfigValueAsBoolAsync(string key, bool defaultValue = false)
        {
            var value = await GetConfigValueAsync(key, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("1") ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Set or update a configuration value.
        /// If key already exists, updates the value; otherwise creates new.
        /// </summary>
        public async Task SetConfigValueAsync(string key, string value, string? description, Guid setBy)
        {
            var config = await GetConfigAsync(key);

            if (config == null)
            {
                // Create new
                config = new SystemConfiguration
                {
                    Id = Guid.NewGuid(),
                    ConfigKey = key,
                    ConfigValue = value,
                    Description = description,
                    LastModifiedBy = setBy,
                    LastModifiedAt = DateTime.UtcNow
                };

                _context.SystemConfigurations.Add(config);
            }
            else
            {
                // Update existing
                config.ConfigValue = value;
                if (!string.IsNullOrWhiteSpace(description))
                {
                    config.Description = description;
                }
                config.LastModifiedBy = setBy;
                config.LastModifiedAt = DateTime.UtcNow;

                _context.SystemConfigurations.Update(config);
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get a single configuration by key.
        /// Returns null if not found.
        /// </summary>
        public async Task<SystemConfiguration?> GetConfigAsync(string key)
        {
            return await _context.SystemConfigurations
                .FirstOrDefaultAsync(c => c.ConfigKey == key);
        }

        /// <summary>
        /// Get all configurations.
        /// Sorted by ConfigKey.
        /// </summary>
        public async Task<List<SystemConfiguration>> GetAllConfigsAsync()
        {
            return await _context.SystemConfigurations
                .OrderBy(c => c.ConfigKey)
                .ToListAsync();
        }
    }
}
