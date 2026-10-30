using System.ComponentModel.DataAnnotations;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace JMHConnect.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAntiforgery antiforgery,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("antiforgery")]
    public IActionResult GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryResponse(tokens.RequestToken!));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsEnabled)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count != 1 || !ApplicationRoles.All.Contains(roles[0], StringComparer.Ordinal))
        {
            await signInManager.SignOutAsync();
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return Ok(new CurrentUserResponse(user.Id, user.Email!, roles[0], user.MemberProfileId));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> CurrentUser()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsEnabled)
        {
            await signInManager.SignOutAsync();
            return Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count != 1 || !ApplicationRoles.All.Contains(roles[0], StringComparer.Ordinal))
        {
            await signInManager.SignOutAsync();
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return Ok(new CurrentUserResponse(user.Id, user.Email!, roles[0], user.MemberProfileId));
    }

    [AllowAnonymous]
    [HttpPost("complete-setup")]
    public async Task<IActionResult> CompleteSetup(CompleteAccountSetupRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null || user.EmailConfirmed)
        {
            return BadRequest(new { message = "This account setup link is invalid or has expired." });
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = "This account setup link is invalid or has expired." });
        }

        user.EmailConfirmed = true;
        var confirmResult = await userManager.UpdateAsync(user);
        if (!confirmResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return NoContent();
    }
}

public sealed record AntiforgeryResponse(string RequestToken);
public sealed record CurrentUserResponse(string Id, string Email, string Role, int? MemberProfileId);

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;
}

public sealed class CompleteAccountSetupRequest
{
    [Required, StringLength(450)]
    public string UserId { get; init; } = string.Empty;

    [Required, StringLength(4096)]
    public string Token { get; init; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 1)]
    public string Password { get; init; } = string.Empty;
}
