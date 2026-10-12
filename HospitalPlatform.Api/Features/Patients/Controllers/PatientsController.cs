using HospitalPlatform.Api.Features.Patients.DTOs;
using HospitalPlatform.Api.Features.Patients.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalPlatform.Api.Features.Patients.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    // //////////////////////////////////////////
    // Dependencies
    // //////////////////////////////////////////
    private readonly IPatientRegistrationService _registrationService;

    public PatientsController(IPatientRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    // //////////////////////////////////////////
    // Registration
    // //////////////////////////////////////////
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<RegisterPatientResponse>> Register(RegisterPatientRequest request)
    {
        var response = await _registrationService.RegisterAsync(request);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
