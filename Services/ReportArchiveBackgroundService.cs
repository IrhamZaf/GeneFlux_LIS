using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LIS.Services;

/// <summary>Daily job: archive approved reports older than 30 days (see <see cref="ReportService.AutoArchiveApprovedReportsAsync"/>).</summary>
public sealed class ReportArchiveBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportArchiveBackgroundService> _logger;

    public ReportArchiveBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReportArchiveBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reportService = scope.ServiceProvider.GetRequiredService<ReportService>();
                var n = await reportService.AutoArchiveApprovedReportsAsync(TimeSpan.FromDays(30), stoppingToken);
                if (n > 0)
                    _logger.LogInformation("Auto-archived {Count} approved report(s) older than 30 days.", n);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report auto-archive run failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
