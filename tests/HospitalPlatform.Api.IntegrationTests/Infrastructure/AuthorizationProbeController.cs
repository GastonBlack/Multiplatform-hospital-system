using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalPlatform.Api.IntegrationTests.Infrastructure;

// These endpoints are loaded only by the test host to exercise authorization policies.
[ApiController]
[Route("__tests/authorization")]
public class AuthorizationProbeController : ControllerBase
{
    [Authorize]
    [HttpGet("active")]
    public IActionResult ActiveAccount()
    {
        return Ok();
    }

    [Authorize(Roles = "Administrator")]
    [HttpGet("administrator")]
    public IActionResult Administrator()
    {
        return Ok();
    }
}
