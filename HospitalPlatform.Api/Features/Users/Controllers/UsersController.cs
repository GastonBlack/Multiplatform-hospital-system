using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HospitalPlatform.Api.Features.Users.DTOs;
using HospitalPlatform.Api.Features.Users.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalPlatform.Api.Features.Users.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserProfileService _profileService;

    public UsersController(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    [Authorize(Policy = "AuthenticatedAccount")]
    [HttpGet("me")]
    public async Task<ActionResult<GetUserProfile>> GetProfile()
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        return Ok(await _profileService.GetProfileAsync(userId));
    }
}
