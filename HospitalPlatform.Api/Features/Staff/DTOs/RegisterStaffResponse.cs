namespace HospitalPlatform.Api.Features.Staff.DTOs;

public class RegisterStaffResponse
{
    public required Guid UserId { get; set; }
    public required Guid StaffId { get; set; }
    public required string EmployeeNumber { get; set; }
    public required string StaffRole { get; set; }
    public required string AccountStatus { get; set; }
}
