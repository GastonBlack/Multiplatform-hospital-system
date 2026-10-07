namespace HospitalPlatform.Api.Features.Users.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }

    public required ProfileType ProfileType { get; set; }
    public required AccountStatus AccountStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}