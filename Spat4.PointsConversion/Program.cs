using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Spat4.PointsConversion;
using Spat4.PointsConversion.Models;
using Spat4.PointsConversion.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<PointConversionService>();
builder.Services.AddOptions<List<Account>>().BindConfiguration("Accounts");
builder.Services.AddOptions<PointConversionServiceOptions>().BindConfiguration(PointConversionServiceOptions.PointConversionService);
builder.Services.AddOptions<Spat4ClientOptions>().BindConfiguration(Spat4ClientOptions.Spat4Client);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Spat4ClientFactory>();
builder.Services.AddResiliencePipeline<string, HttpResponseMessage>(Constants.ResiliencePipelineKey, (builder, context) =>
{
    var config = context.ServiceProvider.GetRequiredService<IOptions<Spat4ClientOptions>>().Value;

    builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = config.MaxRetries,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(2)
    });

    builder.AddTimeout(TimeSpan.FromSeconds(config.RequestTimeoutInSeconds));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/status", () => "Waiting")
.WithName("GetPointConversionStatus")
.WithOpenApi();

app.Run();