using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Services
{
    /// <summary>
    /// Background service that periodically checks for delayed assignments and triggers escalations.
    /// Runs every hour by default (configurable).
    /// </summary>
    public class EscalationCheckBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EscalationCheckBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

        public EscalationCheckBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<EscalationCheckBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EscalationCheckBackgroundService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for the interval
                    await Task.Delay(_checkInterval, stoppingToken);

                    // Create a new scope for the service
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var escalationService = scope.ServiceProvider.GetRequiredService<IEscalationService>();
                        var performanceService = scope.ServiceProvider.GetRequiredService<IReviewerPerformanceService>();
                        var availabilityService = scope.ServiceProvider.GetRequiredService<IReviewerAvailabilityService>();

                        // Auto-reset expired unavailability/leave records back to Available
                        _logger.LogInformation("Checking for expired reviewer unavailability records...");
                        var resetCount = await availabilityService.AutoResetExpiredUnavailabilityAsync();
                        if (resetCount > 0)
                        {
                            _logger.LogInformation($"Auto-reset {resetCount} reviewer(s) from unavailable back to available status");
                        }

                        // Check for delayed assignments
                        _logger.LogInformation("Checking for delayed assignments...");
                        await escalationService.CheckAndEscalateDelayedAssignmentsAsync();

                        // Recalculate performance metrics
                        _logger.LogInformation("Recalculating reviewer performance metrics...");
                        await performanceService.RecalculateAllMetricsAsync();

                        _logger.LogInformation("Escalation check and metrics recalculation completed");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("EscalationCheckBackgroundService is stopping");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in EscalationCheckBackgroundService: {ex.Message}");
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("EscalationCheckBackgroundService stopped");
            await base.StopAsync(cancellationToken);
        }
    }
}
