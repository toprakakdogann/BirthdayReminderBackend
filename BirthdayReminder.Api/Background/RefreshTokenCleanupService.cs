using BirthdayReminder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BirthdayReminder.Api.Background;

public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenCleanupService> _logger;

    public RefreshTokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<RefreshTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Uygulama açılır açılmaz DB'yi yormamak için kısa bekleme
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var now = DateTime.UtcNow;

                // Expired tokenlar: direkt sil
                // Revoked tokenlar: 7 günden eskiyse sil (istersen değiştir)
                var cutoffRevoked = now.AddDays(-7);

                var deleted = await db.RefreshTokens
                    .Where(t => t.ExpiresAtUtc <= now || (t.RevokedAtUtc != null && t.RevokedAtUtc <= cutoffRevoked))
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                    _logger.LogInformation("RefreshToken cleanup deleted {Count} rows.", deleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshToken cleanup failed.");
            }

            // 6 saatte bir çalışsın
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
