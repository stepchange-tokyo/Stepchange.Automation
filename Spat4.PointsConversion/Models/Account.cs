namespace Spat4.PointsConversion.Models;

public record Account
{
    public required string AccountNumber { get; init; }
    public required string Password { get; init; }
    public required string Pin { get; init; }
}
