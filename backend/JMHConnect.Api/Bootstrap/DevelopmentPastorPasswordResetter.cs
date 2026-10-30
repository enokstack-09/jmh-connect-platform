using System.ComponentModel.DataAnnotations;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace JMHConnect.Api.Bootstrap;

public static class DevelopmentPastorPasswordResetter
{
    public static async Task RunAsync(
        IServiceProvider services,
        DevelopmentPastorPasswordResetRequest request,
        IFirstPastorBootstrapConsole? console = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        console ??= new SystemFirstPastorBootstrapConsole();
        if (console.IsInputRedirected)
        {
            throw new InvalidOperationException("Pastor password recovery requires an interactive console.");
        }

        using var scope = services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        FirstPastorBootstrapConfiguration.Validate(environment, configuration, request.DatabaseName);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = ReadEmail(console);
        var password = await ReadAndValidatePasswordAsync(userManager, email, console, cancellationToken);
        await VerifyTargetAsync(userManager, email, cancellationToken);

        var confirmationPhrase = $"RESET {request.DatabaseName}";
        console.Write($"Type {confirmationPhrase} to reset the verified Pastor account on {request.DatabaseName}: ");
        if (!string.Equals(console.ReadLine(), confirmationPhrase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Password recovery was not confirmed; no account was changed.");
        }

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var databaseGuard = scope.ServiceProvider.GetRequiredService<IFirstPastorBootstrapDatabaseGuard>();
        await using var transaction = await databaseGuard.AcquireVerifiedTransactionAsync(
            db,
            request.DatabaseName,
            cancellationToken);

        // Recheck under the existing SQL application lock before changing the password.
        var target = await VerifyTargetAsync(userManager, email, cancellationToken);
        var previousSecurityStamp = target.SecurityStamp;
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(target);
        var resetResult = await userManager.ResetPasswordAsync(target, resetToken, password);
        EnsureSuccess(resetResult, "Could not reset the verified Pastor password.");

        if (string.Equals(previousSecurityStamp, target.SecurityStamp, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Password recovery did not invalidate existing sessions.");
        }

        await transaction.CommitAsync(cancellationToken);
        console.WriteLine("Pastor password recovery completed successfully.");
    }

    private static string ReadEmail(IFirstPastorBootstrapConsole console)
    {
        console.Write("Existing Pastor email: ");
        var email = console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            throw new InvalidOperationException("A valid existing Pastor email is required.");
        }

        return email;
    }

    private static async Task<string> ReadAndValidatePasswordAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        IFirstPastorBootstrapConsole console,
        CancellationToken cancellationToken)
    {
        console.Write("Show password while typing? Type SHOW to display it; press Enter to keep it hidden: ");
        var showPassword = string.Equals(console.ReadLine()?.Trim(), "SHOW", StringComparison.OrdinalIgnoreCase);
        console.Write("New password: ");
        var password = console.ReadPassword(showPassword);
        if (!showPassword) console.WriteLine();
        console.Write("Confirm new password: ");
        var confirmation = console.ReadPassword(showPassword);
        if (!showPassword) console.WriteLine();
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The passwords do not match.");
        }

        var passwordValidationUser = new ApplicationUser { UserName = email, Email = email };
        foreach (var validator in userManager.PasswordValidators)
        {
            EnsureSuccess(await validator.ValidateAsync(userManager, passwordValidationUser, password),
                "The supplied password does not meet the required policy.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return password;
    }

    private static async Task<ApplicationUser> VerifyTargetAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        CancellationToken cancellationToken)
    {
        var target = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("The requested Pastor account was not found.");
        if (!target.IsEnabled || !target.EmailConfirmed)
        {
            throw new InvalidOperationException("The requested Pastor account must be enabled and email-confirmed.");
        }

        var targetRoles = await userManager.GetRolesAsync(target);
        if (targetRoles.Count != 1 || !string.Equals(targetRoles[0], ApplicationRoles.Pastor, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The requested account must have exactly the Pastor role.");
        }

        var pastorAccounts = await userManager.GetUsersInRoleAsync(ApplicationRoles.Pastor);
        if (pastorAccounts.Count != 1 || !string.Equals(pastorAccounts[0].Id, target.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Password recovery requires the verified target to be the only Pastor account.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return target;
    }

    private static void EnsureSuccess(IdentityResult result, string failureMessage)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(failureMessage);
        }
    }
}
