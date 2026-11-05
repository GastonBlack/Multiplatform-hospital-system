using System.Security.Claims;
using HospitalPlatform.Api.Features.Authentication.Controllers;
using HospitalPlatform.Api.Features.Authentication.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HospitalPlatform.Api.Tests.Unit.Features.Authentication;

public class AuthenticationControllerTests
{
    [Fact]
    public void GetSession_WithoutRoleClaim_DoesNotDefaultToAdministratorForStaff()
    {
        var controller = new AuthenticationController(null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim("sub", Guid.NewGuid().ToString()),
                        new Claim("sid", Guid.NewGuid().ToString()),
                        new Claim("profile_type", "Staff"),
                        new Claim("account_status", "Active")
                    ], "test"))
                }
            }
        };

        var result = Assert.IsType<OkObjectResult>(controller.GetSession().Result);
        Assert.Null(Assert.IsType<CurrentSessionResponse>(result.Value).Role);
    }
}
