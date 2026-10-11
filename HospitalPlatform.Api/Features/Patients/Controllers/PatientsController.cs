using HospitalPlatform.Api.Features.Patients.DTOs;
using HospitalPlatform.Api.Features.Patients.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalPlatform.Api.Features.Patients.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController(IPatientRegistrationService registrationService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<RegisterPatientResponse>> Register(RegisterPatientRequest request)
    {
        var response = await registrationService.RegisterAsync(request);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
