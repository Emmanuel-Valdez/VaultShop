using Microsoft.EntityFrameworkCore;
using VaultShop.DataAccess.Data;

namespace VaultShop.Web.Services.Notifications;

/// <summary>
/// Deletes order notifications older than six months, regardless of read state,
/// in bounded batches so a large backlog does not lock the table in one statement.
/// </summary>
public sealed class NotificationCleanupBackgroundService : BackgroundService
{
	internal const int BatchSize = 500;
	private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

	private readonly IServiceProvider _services;
	private readonly ILogger<NotificationCleanupBackgroundService> _logger;

	public NotificationCleanupBackgroundService(
		IServiceProvider services,
		ILogger<NotificationCleanupBackgroundService> logger)
	{
		_services = services;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(Interval);
		while (await timer.WaitForNextTickAsync(stoppingToken))
		{
			try
			{
				using var scope = _services.CreateScope();
				await CleanupScopeAsync(scope.ServiceProvider, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unexpected error during notification cleanup cycle.");
			}
		}
	}

	internal async Task<int> CleanupScopeAsync(IServiceProvider scopeServices, CancellationToken stoppingToken)
	{
		var db = scopeServices.GetRequiredService<ApplicationDbContext>();
		var cutoff = DateTime.UtcNow.AddMonths(-6);
		var deleted = 0;

		while (true)
		{
			var ids = await db.UserNotifications
				.Where(notification => notification.CreatedAt < cutoff)
				.OrderBy(notification => notification.CreatedAt)
				.Select(notification => notification.Id)
				.Take(BatchSize)
				.ToListAsync(stoppingToken);

			if (ids.Count == 0)
			{
				break;
			}

			deleted += await db.UserNotifications
				.Where(notification => ids.Contains(notification.Id))
				.ExecuteDeleteAsync(stoppingToken);
		}

		_logger.LogInformation("Notification cleanup finished. Deleted={Deleted}.", deleted);
		return deleted;
	}
}
