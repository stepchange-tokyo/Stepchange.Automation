using Microsoft.Extensions.Options;
using Spat4.PointsConversion.Models;

namespace Spat4.PointsConversion.Services;

internal class PointConversionService(IOptions<PointConversionServiceOptions> serviceOptions, IOptions<List<Account>> accounts, TimeProvider timeProvider, Spat4ClientFactory clientFactory, ILogger<PointConversionService> logger) : IHostedService
{
    private readonly PointConversionServiceOptions _options = serviceOptions.Value;
    private readonly List<Account> _accounts = accounts.Value;
    private const string TokyoTimeZoneId = "Tokyo Standard Time";
    private Timer? _timer;
    private CancellationTokenSource? _stoppingCts;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = new CancellationTokenSource();

        _timer = new Timer(StartPointConversion, _stoppingCts.Token, GetPointConversionDueTime(), TimeSpan.FromHours(24));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _stoppingCts?.Cancel();
        _timer?.Dispose();
    }

    private void StartPointConversion(object? state)
    {
        if (state is not null && state is CancellationToken cancellationToken)
        {
            _ = ConvertPoints(cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("State must be a cancellation token.");
        }
    }

    private async Task ConvertPoints(CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            var tasks = new List<Task>();

            tasks.AddRange(_accounts.Select(async account =>
            {
                var conversionStartDelay = Random.Shared.Next(_options.MinConversionStartDelayInMilliseconds, _options.MaxConversionStartDelayInMilliseconds);
                await Task.Delay(conversionStartDelay, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

                using var client = clientFactory.CreateClient(account);
                await client.LoginAsync();
                await client.ConvertPoints();
                await client.LogoutAsync();
            }));

            await Task.WhenAll(tasks);
        }
    }

    private TimeSpan GetPointConversionDueTime()
    {
        var currentTime = TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(timeProvider.GetUtcNow(), TokyoTimeZoneId).TimeOfDay);
        TimeSpan dueTime;

        if (currentTime < _options.DailyConversionTime)
        {
            // Conversion time is later today.
            dueTime = _options.DailyConversionTime - currentTime;
        }
        else
        {
            // Conversion time is tomorrow.
            dueTime = _options.DailyConversionTime.Add(TimeSpan.FromDays(1)) - currentTime;
        }

        return dueTime;
    }
}
