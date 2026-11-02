using HospitalPlatform.Api.Features.Doctors.DTOs;
using HospitalPlatform.Api.Features.Doctors.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalPlatform.Api.Features.Doctors.Controllers;

[ApiController]
[Route("api/doctors")]
[Authorize]
public class DoctorsController : ControllerBase
{
    // //////////////////////////////////////////
    // Dependencies
    // //////////////////////////////////////////
    private readonly IDoctorRegistrationService _registrationService;

    public DoctorsController(IDoctorRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    // //////////////////////////////////////////
    // Registration
    // //////////////////////////////////////////
    [Authorize(Roles = "Administrator")]
    [HttpPost("register")]
    [ProducesResponseType<RegisterDoctorResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RegisterDoctorResponse>> Register(RegisterDoctorRequest request)
    {
        var response = await _registrationService.RegisterAsync(request);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
