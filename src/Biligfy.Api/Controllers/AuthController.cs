using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Biligfy.Api.Auth;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var response = await authService.RegisterAsync(request, ct);
            return Ok(response);
        }
        catch (EmailAlreadyRegisteredException)
        {
            return Problem(
                title: "E-posta zaten kayıtlı",
                detail: "Bu e-posta adresi ile bir hesap mevcut.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var response = await authService.LoginAsync(request, ct);
        if (response is null)
        {
            // E-posta mi şifre mi yanlış, bilgi sızdırılmaz.
            return Problem(
                title: "Geçersiz kimlik bilgileri",
                detail: "E-posta veya şifre hatalı.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<object> Me()
    {
        return Ok(new
        {
            Id = User.FindFirstValue("sub"),
            Email = User.FindFirstValue("email"),
            DisplayName = User.FindFirstValue("name"),
            Language = User.FindFirstValue("lang")
        });
    }
}
