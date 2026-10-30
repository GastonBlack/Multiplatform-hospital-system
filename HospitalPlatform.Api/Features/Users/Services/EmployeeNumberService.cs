using System.Globalization;
using HospitalPlatform.Api.Features.Users.Models;
using HospitalPlatform.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HospitalPlatform.Api.Features.Users.Services;

public static class EmployeeNumberService
{
    public static async Task<string> RegisterAsync(HospitalDbContext db, Guid userId)
    {
        var number = await db.Database.SqlQueryRaw<long>(
            "SELECT nextval('\"EmployeeNumberSequence\"') AS \"Value\"").SingleAsync();
        var employeeNumber = "EMP-" + number.ToString("D6", CultureInfo.InvariantCulture);
        db.EmployeeNumbers.Add(new EmployeeNumberRegistration { UserId = userId, EmployeeNumber = employeeNumber });
        return employeeNumber;
    }
}
