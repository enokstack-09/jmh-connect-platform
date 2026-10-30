using System.ComponentModel.DataAnnotations;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using JMHConnect.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JMHConnect.Api.Controllers;

[ApiController]
[Authorize(Policy = "PastorOnly")]
[Route("api/accounts")]
public sealed class AccountsController(
    ApplicationDbContext dbContext,
    IConfiguration configuration,
    IAccountSetupEmailSender emailSender,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (!ApplicationRoles.AccountCreationRoles.Contains(request.Role, StringComparer.Ordinal))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Role)] = ["New accounts may be created as Leader or Member."]
            }));
        }

        MemberProfile? profile = null;
        if (request.MemberProfileId is not null)
        {
            profile = await dbContext.MemberProfiles
                .SingleOrDefaultAsync(item => item.Id == request.MemberProfileId, cancellationToken);
            if (profile is null || await dbContext.Users.AnyAsync(
                    user => user.MemberProfileId == request.MemberProfileId, cancellationToken))
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    [nameof(request.MemberProfileId)] = ["The selected profile does not exist or already has a login."]
                }));
            }
        }

        if (!await roleManager.RoleExistsAsync(request.Role))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Application roles have not been initialized." });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            MemberProfile = profile,
            IsEnabled = true
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            return IdentityValidationProblem(createResult);
        }

        var roleResult = await userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return IdentityValidationProblem(roleResult);
        }

        try
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var frontendBaseUrl = GetFrontendBaseUrl();
            var setupLink = new Uri(frontendBaseUrl,
                $"/account/setup?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}");
            await emailSender.SendSetupLinkAsync(email, setupLink, cancellationToken);
        }
        catch (Exception)
        {
            await userManager.DeleteAsync(user);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "The account could not be created because setup email delivery is unavailable." });
        }

        return Accepted(new { userId = user.Id, email, role = request.Role });
    }

    [HttpPut("{userId}/role")]
    public async Task<IActionResult> ChangeRole(
        string userId,
        ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApplicationRoles.All.Contains(request.Role, StringComparer.Ordinal))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Role)] = ["The role must be Pastor, Leader, or Member."]
            }));
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        var existingRoles = await userManager.GetRolesAsync(user);
        if (existingRoles.Count != 1 || !ApplicationRoles.All.Contains(existingRoles[0], StringComparer.Ordinal))
        {
            return Conflict(new { message = "The account does not have exactly one recognized role." });
        }

        var existingRole = existingRoles[0];
        if (existingRole == request.Role)
        {
            return NoContent();
        }

        if (!await roleManager.RoleExistsAsync(request.Role))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Application roles have not been initialized." });
        }

        if (existingRole == ApplicationRoles.Pastor && request.Role != ApplicationRoles.Pastor)
        {
            var pastorCount = (await userManager.GetUsersInRoleAsync(ApplicationRoles.Pastor)).Count;
            if (pastorCount <= 1)
            {
                return Conflict(new { message = "The last Pastor account cannot be demoted." });
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var addResult = await userManager.AddToRoleAsync(user, request.Role);
        if (!addResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IdentityValidationProblem(addResult);
        }

        var removeResult = await userManager.RemoveFromRoleAsync(user, existingRole);
        if (!removeResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IdentityValidationProblem(removeResult);
        }

        await userManager.UpdateSecurityStampAsync(user);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private Uri GetFrontendBaseUrl()
    {
        var configured = configuration["ClientApp:BaseUrl"];
        if (string.IsNullOrWhiteSpace(configured) &&
            HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
        {
            configured = "http://localhost:4200";
        }

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var frontendUri) ||
            (frontendUri.Scheme != Uri.UriSchemeHttps &&
             !HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment()))
        {
            throw new InvalidOperationException("A valid HTTPS ClientApp:BaseUrl must be configured.");
        }

        return frontendUri;
    }

    private IActionResult IdentityValidationProblem(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
        return ValidationProblem(new ValidationProblemDetails(errors));
    }
}

public sealed class CreateAccountRequest
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string Role { get; init; } = string.Empty;

    public int? MemberProfileId { get; init; }
}

public sealed class ChangeRoleRequest
{
    [Required, StringLength(20)]
    public string Role { get; init; } = string.Empty;
}
