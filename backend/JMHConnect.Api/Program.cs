using JMHConnect.Api.Bootstrap;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Options;
using JMHConnect.Api.Security;
using JMHConnect.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

var bootstrapRequested = args.Contains(FirstPastorBootstrapRequest.BootstrapFlag, StringComparer.Ordinal);
var passwordResetRequested = args.Contains(DevelopmentPastorPasswordResetRequest.ResetFlag, StringComparer.Ordinal);
var acceptanceSeedRequested = args.Contains(AcceptanceTestAccountSeedRequest.SeedFlag, StringComparer.Ordinal);
if (new[] { bootstrapRequested, passwordResetRequested, acceptanceSeedRequested }.Count(requested => requested) > 1)
{
    throw new InvalidOperationException("Choose exactly one approved Development Identity maintenance command.");
}

var bootstrapRequest = bootstrapRequested ? FirstPastorBootstrapRequest.Parse(args) : null;
var passwordResetRequest = passwordResetRequested ? DevelopmentPastorPasswordResetRequest.Parse(args) : null;
var acceptanceSeedRequest = acceptanceSeedRequested ? AcceptanceTestAccountSeedRequest.Parse(args) : null;
if (!bootstrapRequested && !passwordResetRequested && !acceptanceSeedRequested &&
    (args.Contains(FirstPastorBootstrapRequest.DatabaseFlag, StringComparer.Ordinal) ||
     args.Contains(AcceptanceTestAccountSeedRequest.OwnershipFlag, StringComparer.Ordinal)))
{
    throw new InvalidOperationException($"{FirstPastorBootstrapRequest.DatabaseFlag} is valid only with an approved Development Identity maintenance command.");
}

var appArguments = bootstrapRequest is not null
    ? FirstPastorBootstrapRequest.RemoveBootstrapArguments(args)
    : passwordResetRequest is not null
        ? DevelopmentPastorPasswordResetRequest.RemoveResetArguments(args)
        : acceptanceSeedRequest is not null
            ? AcceptanceTestAccountSeedRequest.RemoveSeedArguments(args)
            : args;
var builder = WebApplication.CreateBuilder(appArguments);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length == 0)
{
    allowedOrigins = ["http://localhost:4200"];
}

var connectionString = builder.Configuration.GetConnectionString("IdentityDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Server=localhost;Database=JMHConnect;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
}

DevelopmentDatabaseTargetConfiguration.Validate(builder.Environment, builder.Configuration, connectionString);

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

var keyRingPath = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(keyRingPath) && builder.Environment.IsDevelopment())
{
    keyRingPath = Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys");
}

var dataProtection = builder.Services.AddDataProtection().SetApplicationName("JMHConnect");
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
}

var sessionMinutes = builder.Configuration.GetValue<int?>("Authentication:SessionIdleTimeoutMinutes") ?? 30;
if (sessionMinutes is < 5 or > 480)
{
    throw new InvalidOperationException("Authentication:SessionIdleTimeoutMinutes must be between 5 and 480.");
}

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(24));

var isDevelopment = builder.Environment.IsDevelopment();
builder.Services.ConfigureApplicationCookie(options =>
{
    // Browsers reject __Host- cookies over HTTP. Development uses localhost HTTP only;
    // production uses the host-only prefix and requires HTTPS.
    options.Cookie.Name = isDevelopment ? "JmhConnect.Dev" : "__Host-JmhConnect";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionMinutes);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
    options.Events.OnValidatePrincipal = async context =>
    {
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user = context.Principal is null ? null : await userManager.GetUserAsync(context.Principal);
        if (user is null || !user.IsEnabled)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        var securityStampValidator = context.HttpContext.RequestServices.GetRequiredService<ISecurityStampValidator>();
        await securityStampValidator.ValidateAsync(context);
    };
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = isDevelopment ? "JmhConnect.Dev.Antiforgery" : "__Host-JmhConnect.Antiforgery";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;
});

builder.Services.AddCors(options => options.AddPolicy("Angular", policy =>
{
    policy.WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
        .WithHeaders("Content-Type", "X-CSRF-TOKEN")
        .AllowCredentials();
}));

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("PastorOnly", policy => policy.RequireRole(ApplicationRoles.Pastor));
});

builder.Services.Configure<EmailSetupOptions>(builder.Configuration.GetSection(EmailSetupOptions.SectionName));
builder.Services.AddSingleton<IAccountSetupEmailSender, SmtpAccountSetupEmailSender>();
builder.Services.AddSingleton<IProfilePhotoStorage, LocalProfilePhotoStorage>();
builder.Services.AddSingleton<IFirstPastorBootstrapDatabaseGuard, SqlServerFirstPastorBootstrapDatabaseGuard>();
builder.Services.AddSingleton<IAcceptanceTestAccountSeedDatabaseGuard, SqlServerAcceptanceTestAccountSeedDatabaseGuard>();
builder.Services.AddHostedService<ProductionConfigurationValidator>();
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddOpenApi();

var app = builder.Build();

if (bootstrapRequest is not null)
{
    await FirstPastorBootstrapper.RunAsync(app.Services, bootstrapRequest);
    return;
}

if (passwordResetRequest is not null)
{
    await DevelopmentPastorPasswordResetter.RunAsync(app.Services, passwordResetRequest);
    return;
}

if (acceptanceSeedRequest is not null)
{
    await AcceptanceTestAccountSeeder.RunAsync(app.Services, acceptanceSeedRequest);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }

internal sealed class ProductionConfigurationValidator(IConfiguration configuration, IHostEnvironment environment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsProduction()) return Task.CompletedTask;

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0 || origins.Any(origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Production requires an explicit HTTPS origin allowlist in Cors:AllowedOrigins.");

        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*")
            throw new InvalidOperationException("Production requires an explicit AllowedHosts value.");

        var frontend = configuration["ClientApp:BaseUrl"];
        if (!Uri.TryCreate(frontend, UriKind.Absolute, out var frontendUri) || frontendUri.Scheme != Uri.UriSchemeHttps ||
            !origins.Contains(frontendUri.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production ClientApp:BaseUrl must be HTTPS and match an origin in Cors:AllowedOrigins.");

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("IdentityDatabase")))
            throw new InvalidOperationException("ConnectionStrings:IdentityDatabase must be configured in production.");
        if (string.IsNullOrWhiteSpace(configuration["DataProtection:KeysPath"]))
            throw new InvalidOperationException("Production requires a persistent, access-restricted DataProtection:KeysPath for session continuity.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
