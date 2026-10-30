using HospitalPlatform.Api.Features.Staff.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using StaffProfile = HospitalPlatform.Api.Features.Staff.Models.Staff;

namespace HospitalPlatform.Api.Features.Staff.Seeds;

public static class StaffSeed
{
    public static void ConfigureAdministrator(HospitalDbContext db, Guid userId, string employeeNumber)
    {
        db.Staff.Add(new StaffProfile
        {
            UserId = userId,
            EmployeeNumber = employeeNumber,
            StaffRole = StaffRole.Administrator
        });
    }
}
