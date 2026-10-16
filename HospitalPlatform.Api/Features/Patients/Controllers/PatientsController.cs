using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

    // //////////////////////////////////////////
    // Current patient profile
    // //////////////////////////////////////////
    [Authorize(Policy = "AuthenticatedAccount", Roles = "Patient")]
    [HttpGet("me")]
    public async Task<ActionResult<GetPatientProfile>> GetPatientProfile()
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var response = await _registrationService.GetPatientProfileAsync(userId);
        return Ok(response);
    }
}
