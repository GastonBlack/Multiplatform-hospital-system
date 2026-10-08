namespace HospitalPlatform.Api.Features.Users.Models;

public class EmployeeNumberRegistration
{
    public required string EmployeeNumber { get; set; }
    public required Guid UserId { get; set; }
}
