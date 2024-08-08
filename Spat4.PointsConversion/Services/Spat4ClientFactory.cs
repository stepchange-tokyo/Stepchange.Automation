using Microsoft.Extensions.Options;
using Spat4.PointsConversion.Models;
using System.Net;

namespace Spat4.PointsConversion.Services;

public class Spat4ClientFactory(IOptions<Spat4ClientOptions> options, IServiceProvider serviceProvider)
{
    private readonly Spat4ClientOptions _options = options.Value;

    public Spat4Client CreateClient(Account account)
    {
        HttpClientHandler requestHandler = new()
        {
            AutomaticDecompression = DecompressionMethods.GZip
        };

        var client = new HttpClient(requestHandler)
        {
            BaseAddress = _options.BaseAddress,
            Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutInSeconds)
        };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko)");

        var spat4Client = ActivatorUtilities.CreateInstance<Spat4Client>(serviceProvider, client, account);
        return spat4Client;
    }
}
