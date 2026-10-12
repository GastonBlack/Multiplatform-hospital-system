using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using HospitalPlatform.Api.Features.Authentication.Services;
using HospitalPlatform.Api.Features.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalPlatform.Api.Features.Authentication.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthenticationController : ControllerBase
{
    // //////////////////////////////////////////
    // Dependencies
    // //////////////////////////////////////////
    private readonly IAuthenticationService _authenticationService;

    public AuthenticationController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    // //////////////////////////////////////////
    // Public authentication
    // //////////////////////////////////////////
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginRequest request)
    {
        return Ok(await _authenticationService.LoginAsync(request));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(RefreshRequest request)
    {
        return Ok(await _authenticationService.RefreshAsync(request));
    }

    // //////////////////////////////////////////
    // Current session
    // //////////////////////////////////////////
    [Authorize(Policy = "AuthenticatedAccount")]
    [HttpGet("session")]
    public ActionResult<CurrentSessionResponse> GetSession()
    {
        return Ok(new CurrentSessionResponse
        {
            UserId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!),
            SessionId = Guid.Parse(User.FindFirstValue("sid")!),
            ProfileType = Enum.Parse<ProfileType>(User.FindFirstValue("profile_type")!),
            AccountStatus = Enum.Parse<AccountStatus>(User.FindFirstValue("account_status")!)
        });
    }

    [Authorize(Policy = "AuthenticatedAccount")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var sessionId = Guid.Parse(User.FindFirstValue("sid")!);
        await _authenticationService.LogoutAsync(userId, sessionId);
        return NoContent();
    }
}
