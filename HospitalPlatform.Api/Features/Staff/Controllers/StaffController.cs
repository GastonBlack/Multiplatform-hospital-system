using HospitalPlatform.Api.Features.Staff.DTOs;
using HospitalPlatform.Api.Features.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalPlatform.Api.Features.Staff.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize]
public class StaffController : ControllerBase
{
    // //////////////////////////////////////////
    // Dependencies
    // //////////////////////////////////////////
    private readonly IStaffRegistrationService _registrationService;

    public StaffController(IStaffRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    // //////////////////////////////////////////
    // Registration
    // //////////////////////////////////////////
    [Authorize(Roles = "Administrator")]
    [HttpPost("register")]
    [ProducesResponseType<RegisterStaffResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RegisterStaffResponse>> Register(RegisterStaffRequest request)
    {
        var response = await _registrationService.RegisterAsync(request);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
