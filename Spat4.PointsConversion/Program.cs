using Spat4.PointsConversion.Models;
using Spat4.PointsConversion.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<PointConversionService>();
builder.Services.AddOptions<List<Account>>().Bind(builder.Configuration.GetSection("Accounts"));
builder.Services.AddOptions<PointConversionServiceOptions>().Bind(builder.Configuration.GetSection(PointConversionServiceOptions.PointConversionService));
builder.Services.AddOptions<Spat4ClientOptions>().Bind(builder.Configuration.GetSection(Spat4ClientOptions.Spat4Client));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Spat4ClientFactory>();

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