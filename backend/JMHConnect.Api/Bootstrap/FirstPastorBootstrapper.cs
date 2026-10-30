using System.ComponentModel.DataAnnotations;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace JMHConnect.Api.Bootstrap;

public static class FirstPastorBootstrapper
{
    public static async Task RunAsync(
        IServiceProvider services,
        FirstPastorBootstrapRequest request,
        IFirstPastorBootstrapConsole? console = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        console ??= new SystemFirstPastorBootstrapConsole();
        if (console.IsInputRedirected)
        {
            throw new InvalidOperationException("First Pastor bootstrap requires an interactive console.");
        }

        using var scope = services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        FirstPastorBootstrapConfiguration.Validate(environment, configuration, request);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var candidate = await ReadAndValidateCandidateAsync(userManager, console, cancellationToken);

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var databaseGuard = scope.ServiceProvider.GetRequiredService<IFirstPastorBootstrapDatabaseGuard>();
        await using var transaction = await databaseGuard.AcquireVerifiedTransactionAsync(
            db,
            request.DatabaseName,
            cancellationToken);

        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "First Pastor bootstrap is allowed only when the Identity database contains no users.");
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var existingRoleNames = await roleManager.Roles
            .Select(role => role.Name)
            .ToListAsync(cancellationToken);
        if (existingRoleNames.Any(role => !ApplicationRoles.All.Contains(role, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "The Identity database contains an unrecognized role. Review it before bootstrapping; no roles were changed.");
        }

        foreach (var role in ApplicationRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                EnsureSuccess(roleResult, "Could not initialize an approved application role.");
            }
        }

        var createResult = await userManager.CreateAsync(candidate.User, candidate.Password);
        EnsureSuccess(createResult, "Could not create the first Pastor.");

        var addRoleResult = await userManager.AddToRoleAsync(candidate.User, ApplicationRoles.Pastor);
        EnsureSuccess(addRoleResult, "Could not assign the Pastor role to the first account.");

        var assignedRoles = await userManager.GetRolesAsync(candidate.User);
        if (assignedRoles.Count != 1 || !string.Equals(assignedRoles[0], ApplicationRoles.Pastor, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The first account was not assigned exactly the Pastor role.");
        }

        await transaction.CommitAsync(cancellationToken);
        console.WriteLine("First Pastor account created successfully.");
    }

    private static async Task<BootstrapCandidate> ReadAndValidateCandidateAsync(
        UserManager<ApplicationUser> userManager,
        IFirstPastorBootstrapConsole console,
        CancellationToken cancellationToken)
    {
        console.Write("First Pastor email: ");
        var email = console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            throw new InvalidOperationException("A valid email address is required.");
        }

        console.Write("Show password while typing? Type SHOW to display it; press Enter to keep it hidden: ");
        var showPassword = string.Equals(console.ReadLine()?.Trim(), "SHOW", StringComparison.OrdinalIgnoreCase);

        console.Write("Password: ");
        var password = console.ReadPassword(showPassword);
        if (!showPassword) console.WriteLine();
        console.Write("Confirm password: ");
        var confirmation = console.ReadPassword(showPassword);
        if (!showPassword) console.WriteLine();
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The passwords do not match.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsEnabled = true
        };

        foreach (var validator in userManager.UserValidators)
        {
            EnsureSuccess(await validator.ValidateAsync(userManager, user), "The first Pastor email is invalid.");
        }

        foreach (var validator in userManager.PasswordValidators)
        {
            EnsureSuccess(await validator.ValidateAsync(userManager, user, password), "The supplied password does not meet the required policy.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new BootstrapCandidate(user, password);
    }

    private static void EnsureSuccess(IdentityResult result, string failureMessage)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }

    private sealed record BootstrapCandidate(ApplicationUser User, string Password);
}
