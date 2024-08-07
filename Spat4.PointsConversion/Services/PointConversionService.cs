using Microsoft.Extensions.Options;
using Spat4.PointsConversion.Models;

namespace Spat4.PointsConversion.Services;

internal class PointConversionService : IHostedService
{
    private readonly PointConversionServiceOptions _options;
    private readonly List<Account> _accounts;
    private readonly TimeProvider _timeProvider;
    private readonly Spat4ClientFactory _clientFactory;
    private readonly ILogger<PointConversionService> _logger;
    private const string TokyoTimeZoneId = "Tokyo Standard Time";
    private readonly Timer _timer;
    private CancellationTokenSource _stoppingCts;

    public PointConversionService(IOptions<PointConversionServiceOptions> serviceOptions, IOptions<List<Account>> accounts, TimeProvider timeProvider, Spat4ClientFactory clientFactory, ILogger<PointConversionService> logger)
    {
        _timeProvider = timeProvider;
        _clientFactory = clientFactory;
        _logger = logger;
        _options = serviceOptions.Value;
        _accounts = accounts.Value;
        _timer = new Timer(StartPointConversion);
        _stoppingCts = new CancellationTokenSource();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var dueTime = GetPointConversionDueTime();
        _timer.Change(dueTime, TimeSpan.FromHours(24));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppingCts.Cancel();
        _timer.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _stoppingCts.Cancel();
        _timer.Dispose();
    }

    private void StartPointConversion(object? state)
    {
        _ = ConvertPoints(_stoppingCts.Token);
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

                using var client = _clientFactory.CreateClient(account);
                bool isSuccess = await client.LoginAsync();
                if (!isSuccess)
                {
                    return;
                }

                isSuccess = await client.ConvertPoints();
                if (!isSuccess)
                {
                    return;
                }

                isSuccess = await client.LogoutAsync();
                if (!isSuccess)
                {
                    return;
                }

                _logger.LogInformation("Full point conversion process completed for account {Account}.", account.AccountNumber);
            }));

            await Task.WhenAll(tasks);
        }
    }

    private TimeSpan GetPointConversionDueTime()
    {
        var currentTime = TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(_timeProvider.GetUtcNow(), TokyoTimeZoneId).TimeOfDay);
        TimeSpan dueTime;

        if (currentTime < _options.DailyConversionTimeInJst)
        {
            // Conversion time is later today.
            dueTime = _options.DailyConversionTimeInJst - currentTime;
        }
        else
        {
            // Conversion time is tomorrow.
            dueTime = _options.DailyConversionTimeInJst.Add(TimeSpan.FromDays(1)) - currentTime;
        }

        return dueTime;
    }
}
