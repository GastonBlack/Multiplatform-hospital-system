using HospitalPlatform.Api.Features.Users.Models;

namespace HospitalPlatform.Api.Features.Staff.Models;

public class Staff
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required Guid UserId { get; set; }
    public ProfileType ProfileType { get; private set; } = ProfileType.Staff;

    public required string EmployeeNumber { get; set; }
    public required StaffRole StaffRole { get; set; }
}
