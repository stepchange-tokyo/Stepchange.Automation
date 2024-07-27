using Spat4.PointsConversion.Models;

namespace Spat4.PointsConversion.Services;

public class Spat4Client : IDisposable
{
    private long? _pointsBalance;
    private bool _isLoggedIn;
    private readonly HttpClient _client;
    private readonly Account _account;
    private readonly Spat4ClientOptions _options;
    private readonly ILogger<Spat4Client> _logger;
    private readonly IDisposable? _loggerScope;

    public Spat4Client(HttpClient client, Account account, Spat4ClientOptions options, ILogger<Spat4Client> logger)
    {
        _client = client;
        _account = account;
        _options = options;
        _logger = logger;

        Dictionary<string, object> scopeState = [];
        scopeState.Add(nameof(account.AccountNumber), account.AccountNumber);
        _loggerScope = _logger.BeginScope(scopeState);
    }

    public async Task LoginAsync()
    {
        _logger.LogInformation("Attempting to log in.");

        var response = await _client.GetAsync("spat4/pp");
        var htmlString = await response.Content.ReadAsStringAsync();

        var sessionId = Spat4Parser.ParseSessionId(htmlString);

        Dictionary<string, string> formData = new()
        {
            { PageData.FUNCTION_ID, PageCode.FUNCTION_LOGIN },
            { PageData.MEDIA_TYPE, "pc" },
            { PageData.PAGE_KIND, string.Empty },
            { PageData.PAGE_TARGET, PageCode.PAGE_INDEX },
            { PageData.NEXT_FUNCTION_ID, PageCode.FUNCTION_INDEX },
            { PageData.NEXT_PAGE_KIND, PageCode.KIND_MEMBER },
            { PageData.NEXT_PAGE_TARGET, PageCode.PAGE_INDEX },
            { PageData.P_1, string.Empty },
            { PageData.P_2, string.Empty },
            { PageData.P_3, string.Empty },
            { PageData.P_4, string.Empty },
            { PageData.P_5, string.Empty },
            { PageData.SESSION_ID, sessionId },
            { PageData.LOGIN_USERNAME, _account.AccountNumber },
            { PageData.LOGIN_PASSWORD, _account.Password }
        };

        var formEncodedContent = new FormUrlEncodedContent(formData);

        response = await _client.PostAsync("spat4/pp", formEncodedContent);
        htmlString = await response.Content.ReadAsStringAsync();

        _isLoggedIn = Spat4Parser.CheckLoggedIn(htmlString);

        if (_isLoggedIn)
        {
            _logger.LogInformation("Successfully logged in.");
        }
    }

    public async Task ConvertPoints()
    {
        if (!_isLoggedIn)
        {
            throw new InvalidOperationException("Must be logged in to convert points.");
        }

        _logger.LogInformation("Starting point conversion for account {AccountNumber}, initial point balance: {PointsBalance}.", _account.AccountNumber, _pointsBalance);

        var htmlString = await NavigateToExchangeForCashPage(); // I think we can actually skip this call and go straight to the points to cash page
        _pointsBalance = Spat4Parser.ParsePointsBalance(htmlString);

        var conversionRounds = _pointsBalance / _options.MinimumConversionAmount;
        var maxConversionRounds = conversionRounds > _options.MaxConversionsPerAccountPerDay ? _options.MaxConversionsPerAccountPerDay : conversionRounds;

        var conversionRoundCount = 0;
        while (_pointsBalance >= _options.MinimumConversionAmount && conversionRoundCount < maxConversionRounds)
        {
            conversionRoundCount++;
            _logger.LogInformation("Converting points, round {ConversionRoundCount} of {MaxConversionRounds}", conversionRoundCount, maxConversionRounds);

            if (!Spat4Parser.IsExchangeForCashPage(htmlString))
            {
                _logger.LogError("Failed to navigate to the exchange for cash page.");
                break;
            }

            htmlString = await NavigateToUsePointsPage();

            if (!Spat4Parser.IsUsePointsPage(htmlString))
            {
                _logger.LogError("Failed to navigate to the use points page.");
                break;
            }

            htmlString = await NavigateToExchangeConfirmationPage();

            if (!Spat4Parser.CheckExchangeFormContainer(htmlString))
            {
                _logger.LogError("Failed to confirm the exchange.");
                break;
            }

            htmlString = await NavigateToCompleteConversionPage();

            var metaContent = Spat4Parser.ParseMetaContent(htmlString);
            if (!string.IsNullOrWhiteSpace(metaContent))
            {
                _logger.LogInformation("Finalised points conversion.");
                var keys = Spat4Parser.GetKeys(metaContent);

                var queryParams = new Dictionary<string, string>
                {
                    { PageData.KEY_1, keys["key1"] },
                    { PageData.KEY_2, keys["key2"] }
                };

                var uri = BuildUriWithQueryParameters("spat4/pp", queryParams);
                var response = await _client.GetAsync(uri);
                htmlString = await response.Content.ReadAsStringAsync();

                var newPointsBalance = Spat4Parser.ParsePointsBalance(htmlString);

                if (newPointsBalance < _pointsBalance)
                {
                    _logger.LogInformation("Completed points conversion round {ConversionRoundCount} of {MaxConversionsRounds}, new points balance: {PointsBalance}",
                        conversionRoundCount, maxConversionRounds, newPointsBalance);

                    await Task.Delay(Random.Shared.Next(_options.MinConversionWaitTimeInMilliseconds, _options.MaxConversionWaitTimeInMilliseconds));
                }
                else
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Points conversion complete.");
    }

    private async Task<string> NavigateToExchangeForCashPage()
    {
        Dictionary<string, string> formData = new()
        {
            { PageData.FUNCTION_ID, PageCode.FUNCTION_POINTS },
            { PageData.MEDIA_TYPE, "pc" },
            { PageData.PAGE_KIND, PageCode.KIND_POINTS },
            { PageData.PAGE_TARGET, PageCode.PAGE_INDEX },
            { PageData.P_1, string.Empty },
            { PageData.P_2, string.Empty },
            { PageData.P_3, string.Empty },
            { PageData.P_4, string.Empty },
            { PageData.P_5, string.Empty },
        };

        var formEncodedContent = new FormUrlEncodedContent(formData);

        var response = await _client.PostAsync("spat4/pp", formEncodedContent);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task LogoutAsync()
    {
        _logger.LogInformation("Attempting to log out.");
        Dictionary<string, string> formData = new()
        {
            { PageData.FUNCTION_ID, PageCode.FUNCTION_LOGOUT },
            { PageData.PAGE_KIND, PageCode.KIND_MEMBER },
            { PageData.PAGE_TARGET, PageCode.PAGE_INDEX },
            { PageData.MEDIA_TYPE, "pc" },
            { PageData.P_1, string.Empty },
            { PageData.P_2, string.Empty },
            { PageData.P_3, string.Empty },
            { PageData.P_4, string.Empty },
            { PageData.P_5, string.Empty },
            // The session id is sent in the logout request in browser but appears to be unnecessary.
            // { PageData.SESSION_ID, sessionId }, 
        };

        var formEncodedContent = new FormUrlEncodedContent(formData);

        var response = await _client.PostAsync("spat4/pp", formEncodedContent);
        var htmlString = await response.Content.ReadAsStringAsync();

        _isLoggedIn = Spat4Parser.CheckLoggedIn(htmlString);

        if (!_isLoggedIn)
        {
            _logger.LogInformation("Successfully logged out.");
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _loggerScope?.Dispose();
    }

    private async Task<string> NavigateToCompleteConversionPage()
    {
        Dictionary<string, string> formData = new()
        {
            { PageData.FUNCTION_ID, PageCode.FUNCTION_EXCHANGE_FOR_CASH },
            { PageData.MEDIA_TYPE, "pc" },
            { PageData.PAGE_KIND, PageCode.KIND_EXCHANGE_FOR_CASH},
            { PageData.PAGE_TARGET, PageCode.PAGE_EXCHANGE_FOR_CASH_COMPLETE},
            { PageData.P_1, _options.PointsHTMLInputValue },
            { PageData.P_2, string.Empty },
            { PageData.PIN_NUMBER, _account.Pin },
            { PageData.X, "86" },
            { PageData.Y, "23 "},
        };

        var formEncodedContent = new FormUrlEncodedContent(formData);

        var response = await _client.PostAsync("spat4/pp", formEncodedContent);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> NavigateToExchangeConfirmationPage()
    {
        Dictionary<string, string> formData = new()
        {
            { PageData.FUNCTION_ID, PageCode.FUNCTION_EXCHANGE_FOR_CASH },
            { PageData.MEDIA_TYPE, "pc" },
            { PageData.PAGE_KIND, PageCode.KIND_EXCHANGE_FOR_CASH},
            { PageData.PAGE_TARGET, PageCode.PAGE_EXCHANGE_FOR_CASH_CONFIRM},
            { PageData.P_1, _options.PointsHTMLInputValue },
            { PageData.P_2, string.Empty },
            { PageData.P_3, string.Empty },
            { PageData.P_4, string.Empty },
            { PageData.P_5, string.Empty },
        };

        var formEncodedContent = new FormUrlEncodedContent(formData);

        var response = await _client.PostAsync("spat4/pp", formEncodedContent);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> NavigateToUsePointsPage()
    {
        Dictionary<string, string> formData = new()
        {
            { PageData.FUNCTION_ID, PageCode.FUNCTION_EXCHANGE_FOR_CASH },
            { PageData.MEDIA_TYPE, "pc" },
            { PageData.PAGE_KIND, PageCode.KIND_EXCHANGE_FOR_CASH},
            { PageData.PAGE_TARGET, PageCode.PAGE_EXCHANGE_FOR_CASH},
            { PageData.P_1, string.Empty },
            { PageData.P_2, string.Empty },
            { PageData.P_3, string.Empty },
            { PageData.P_4, string.Empty },
            { PageData.P_5, string.Empty },
        };

        var formEncodedContent = new FormUrlEncodedContent(formData);

        var response = await _client.PostAsync("spat4/pp", formEncodedContent);
        return await response.Content.ReadAsStringAsync();
    }

    private Uri BuildUriWithQueryParameters(string endpoint, Dictionary<string, string> queryParams)
    {
        var uriBuilder = new UriBuilder(new Uri(_client.BaseAddress, endpoint));
        var query = new List<string>();

        foreach (var param in queryParams)
        {
            query.Add($"{param.Key}={Uri.EscapeDataString(param.Value)}");
        }

        uriBuilder.Query = string.Join("&", query);

        return uriBuilder.Uri;
    }
}
