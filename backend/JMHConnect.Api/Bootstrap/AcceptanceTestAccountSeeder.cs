using System.ComponentModel.DataAnnotations;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace JMHConnect.Api.Bootstrap;

public static class AcceptanceTestAccountSeeder
{
    public static async Task RunAsync(
        IServiceProvider services,
        AcceptanceTestAccountSeedRequest request,
        IFirstPastorBootstrapConsole? console = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        console ??= new SystemFirstPastorBootstrapConsole();
        if (console.IsInputRedirected)
        {
            throw new InvalidOperationException("Acceptance account seeding requires an interactive console.");
        }

        using var scope = services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        AcceptanceTestAccountSeedConfiguration.Validate(environment, configuration, request);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var pastor = await ReadCandidateAsync(userManager, console, "dummy Pastor", cancellationToken);
        var member = await ReadCandidateAsync(userManager, console, "dummy Member", cancellationToken);
        if (string.Equals(pastor.User.Email, member.User.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The dummy Pastor and Member must use different email addresses.");
        }

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var guard = scope.ServiceProvider.GetRequiredService<IAcceptanceTestAccountSeedDatabaseGuard>();
        await using var transaction = await guard.AcquireVerifiedTransactionAsync(db, request, cancellationToken);

        if (await userManager.Users.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("Acceptance account seeding requires an empty Identity user store.");
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var existingRoleNames = await roleManager.Roles.Select(role => role.Name).ToListAsync(cancellationToken);
        if (existingRoleNames.Any(role => !ApplicationRoles.All.Contains(role, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("The acceptance database contains an unrecognized role; no accounts were seeded.");
        }

        foreach (var role in ApplicationRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSuccess(await roleManager.CreateAsync(new IdentityRole(role)), "Could not initialize an approved application role.");
            }
        }

        EnsureSuccess(await userManager.CreateAsync(pastor.User, pastor.Password), "Could not create the dummy Pastor account.");
        EnsureSuccess(await userManager.AddToRoleAsync(pastor.User, ApplicationRoles.Pastor), "Could not assign the dummy Pastor role.");

        var memberProfile = new MemberProfile
        {
            GivenName = "Acceptance",
            FamilyName = "Member",
            PreferredName = "Test Member"
        };
        db.MemberProfiles.Add(memberProfile);
        await db.SaveChangesAsync(cancellationToken);
        member.User.MemberProfileId = memberProfile.Id;

        EnsureSuccess(await userManager.CreateAsync(member.User, member.Password), "Could not create the dummy Member account.");
        EnsureSuccess(await userManager.AddToRoleAsync(member.User, ApplicationRoles.Member), "Could not assign the dummy Member role.");

        await VerifyExactRoleAsync(userManager, pastor.User, ApplicationRoles.Pastor);
        await VerifyExactRoleAsync(userManager, member.User, ApplicationRoles.Member);
        await transaction.CommitAsync(cancellationToken);
        console.WriteLine("Acceptance test accounts created successfully. Credentials were not displayed or stored by the seeder.");
    }

    private static async Task<SeedCandidate> ReadCandidateAsync(
        UserManager<ApplicationUser> userManager,
        IFirstPastorBootstrapConsole console,
        string label,
        CancellationToken cancellationToken)
    {
        console.Write($"{label} email: ");
        var email = console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            throw new InvalidOperationException($"A valid {label} email address is required.");
        }

        console.Write($"{label} password: ");
        var password = console.ReadPassword(showPassword: false);
        console.WriteLine();
        console.Write($"Confirm {label} password: ");
        var confirmation = console.ReadPassword(showPassword: false);
        console.WriteLine();
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The {label} passwords do not match.");
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
            EnsureSuccess(await validator.ValidateAsync(userManager, user), $"The {label} email is invalid.");
        }

        foreach (var validator in userManager.PasswordValidators)
        {
            EnsureSuccess(await validator.ValidateAsync(userManager, user, password), $"The {label} password does not meet the required policy.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new SeedCandidate(user, password);
    }

    private static async Task VerifyExactRoleAsync(UserManager<ApplicationUser> users, ApplicationUser user, string expectedRole)
    {
        var assigned = await users.GetRolesAsync(user);
        if (assigned.Count != 1 || !string.Equals(assigned[0], expectedRole, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An acceptance account was not assigned exactly its intended role.");
        }
    }

    private static void EnsureSuccess(IdentityResult result, string failureMessage)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }

    private sealed record SeedCandidate(ApplicationUser User, string Password);
}
