namespace HospitalPlatform.Api.Features.Authentication.Models;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required Guid SessionId { get; set; }
    public required byte[] TokenHash { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public required DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
