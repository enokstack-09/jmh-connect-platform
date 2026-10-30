using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JMHConnect.Api.Data;
using JMHConnect.Api.Models;
using JMHConnect.Api.Security;
using JMHConnect.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JMHConnect.Api.Tests;

public sealed class AuthenticationEndpointTests
{
    [Fact]
    public async Task ProtectedCurrentUserEndpointRejectsUnauthenticatedRequests()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(ApplicationRoles.Leader)]
    [InlineData(ApplicationRoles.Member)]
    public async Task LeaderAndMemberCannotCreateAccounts(string role)
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();
        await factory.LoginAsync(client, $"{role.ToLowerInvariant()}@example.test", "Jmh-test!2026");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            email = "created@example.test",
            role = "Member"
        });
        var roleChange = await client.PutAsJsonAsync("/api/accounts/other-user/role", new { role = "Pastor" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, roleChange.StatusCode);
    }

    [Fact]
    public async Task PastorCanCreateAccountWithoutReturningSetupToken()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();
        await factory.LoginAsync(client, "pastor@example.test", "Jmh-test!2026");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            email = "new-member@example.test",
            role = "Member"
        });
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.Accepted,
            $"Account creation failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.DoesNotContain("token", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(factory.EmailSender.LastSetupLink);
        Assert.Contains("token=", factory.EmailSender.LastSetupLink!.Query, StringComparison.Ordinal);

        var setupQuery = QueryHelpers.ParseQuery(factory.EmailSender.LastSetupLink.Query);
        using var setupRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/complete-setup")
        {
            Content = JsonContent.Create(new
            {
                userId = setupQuery["userId"].ToString(),
                token = setupQuery["token"].ToString(),
                password = "New-member!2026"
            })
        };
        setupRequest.Headers.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);
        var setupResponse = await client.SendAsync(setupRequest);
        Assert.Equal(HttpStatusCode.NoContent, setupResponse.StatusCode);

        var memberClient = factory.CreateHttpsClient();
        await factory.LoginAsync(memberClient, "new-member@example.test", "New-member!2026");
    }

    [Fact]
    public async Task LoginIssuesSecureHttpOnlySameSiteSessionCookie()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();
        await factory.LoginAsync(client, "pastor@example.test", "Jmh-test!2026");

        Assert.Contains("JmhConnect.Dev=", factory.LastAuthCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", factory.LastAuthCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", factory.LastAuthCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", factory.LastAuthCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "Leader", "Member", "Pastor" }, ApplicationRoles.All.OrderBy(role => role));
    }

    [Fact]
    public async Task DevelopmentHttpUsesAnHttpOnlySameSiteLocalCookie()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpClient();
        await factory.LoginAsync(client, "pastor@example.test", "Jmh-test!2026");

        Assert.Contains("JmhConnect.Dev=", factory.LastAuthCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", factory.LastAuthCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", factory.LastAuthCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", factory.LastAuthCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicRegistrationIsNotMappedAndWeatherRemainsAnonymous()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();

        var registration = await client.PostAsJsonAsync("/api/auth/register", new { email = "x", password = "x" });
        var weather = await client.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.Unauthorized, registration.StatusCode);
        Assert.Equal(HttpStatusCode.OK, weather.StatusCode);
    }

    [Fact]
    public async Task LogoutEndsTheAuthenticatedSession()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();
        await factory.LoginAsync(client, "pastor@example.test", "Jmh-test!2026");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { });
        var currentUser = await client.GetAsync("/api/auth/me");

        Assert.True(logout.StatusCode == HttpStatusCode.NoContent,
            $"Logout failed with {(int)logout.StatusCode}: {await logout.Content.ReadAsStringAsync()}");
        Assert.Equal(HttpStatusCode.Unauthorized, currentUser.StatusCode);
    }

    [Fact]
    public async Task AntiforgeryCookieAndCredentialedDevelopmentCorsAreConfigured()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-csrf-token");

        var preflight = await client.SendAsync(request);
        var csrf = await client.GetAsync("/api/auth/antiforgery");
        var cookieHeaders = string.Join(";", csrf.Headers.GetValues("Set-Cookie"));

        Assert.Equal(HttpStatusCode.NoContent, preflight.StatusCode);
        Assert.Equal("http://localhost:4200", preflight.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", preflight.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.Contains("JmhConnect.Dev.Antiforgery", cookieHeaders, StringComparison.Ordinal);
        Assert.Contains("httponly", cookieHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookieHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookieHeaders, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisabledUserWithAnExistingCookieCannotAccessPastorEndpoints()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();
        await factory.LoginAsync(client, "pastor@example.test", "Jmh-test!2026");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);
        await factory.SetEnabledAsync("pastor@example.test", false);

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            email = "blocked@example.test",
            role = "Member"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtectedStateChangesRejectMissingOrInvalidAntiforgeryTokens(bool sendInvalidToken)
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();
        await factory.LoginAsync(client, "pastor@example.test", "Jmh-test!2026");
        if (sendInvalidToken)
        {
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid-test-token");
        }

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            email = "csrf-blocked@example.test",
            role = "Member"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FiveFailedPasswordsLockTheAccountAndRejectTheCorrectPassword()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var client = factory.CreateHttpsClient();
        var token = await factory.GetAntiforgeryTokenAsync(client);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "member@example.test", password = "Wrong-password!2026" })
            };
            request.Headers.Add("X-CSRF-TOKEN", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
        }

        using var correctPasswordRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "member@example.test", password = "Jmh-test!2026" })
        };
        correctPasswordRequest.Headers.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(correctPasswordRequest)).StatusCode);
        Assert.True(await factory.IsLockedOutAsync("member@example.test"));
    }

    [Fact]
    public async Task MemberListRequiresAuthenticationAndOnlyReturnsActivePrivacySafeSummaries()
    {
        await using var factory = await ApiFactory.CreateAsync();
        await factory.CreateMemberProfileAsync("Active", "Resident", ResidencyStatus.Active);
        await factory.CreateMemberProfileAsync("Archived", "Resident", ResidencyStatus.Archived);
        using var anonymous = factory.CreateHttpsClient();
        using var member = factory.CreateHttpsClient();
        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");

        var anonymousResponse = await anonymous.GetAsync("/api/members");
        var memberResponse = await member.GetAsync("/api/members");
        var body = await memberResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, memberResponse.StatusCode);
        Assert.Contains("Active", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Archived", body, StringComparison.Ordinal);
        Assert.DoesNotContain("dateOfBirth", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contacts", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OtherAuthenticatedUsersReceiveOnlyPublicActiveProfileFieldsAndCannotChangeProfiles()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var profileId = await factory.CreateMemberProfileAsync("Private", "Resident", ResidencyStatus.Active);
        using var leader = factory.CreateHttpsClient();
        await factory.LoginAsync(leader, "leader@example.test", "Jmh-test!2026");
        leader.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var detail = await leader.GetAsync($"/api/members/{profileId}");
        var archive = await leader.PostAsJsonAsync($"/api/members/{profileId}/archive", new { });
        var body = await detail.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains("Private", body, StringComparison.Ordinal);
        Assert.DoesNotContain("dateOfBirth", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contacts", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
    }

    [Fact]
    public async Task LinkedProfileOwnerCanReadSensitiveDetailsButCannotArchive()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var profileId = await factory.CreateMemberProfileAsync("Owned", "Resident", ResidencyStatus.Active, "member@example.test");
        using var member = factory.CreateHttpsClient();
        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");
        member.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var detail = await member.GetAsync($"/api/members/{profileId}");
        var archive = await member.PostAsJsonAsync($"/api/members/{profileId}/archive", new { });
        var body = await detail.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains("dateOfBirth", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("contacts", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
    }

    [Fact]
    public async Task LinkedMemberCanSelfEditOnlyPreferredNameAndPermittedContactFields()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var profileId = await factory.CreateMemberProfileAsync("Official", "Resident", ResidencyStatus.Active, "member@example.test");
        var guardianId = await factory.GetGuardianContactIdAsync(profileId);
        using var member = factory.CreateHttpsClient();
        using var pastor = factory.CreateHttpsClient();
        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");
        member.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var update = await member.PutAsJsonAsync("/api/members/me", new
        {
            preferredName = "My Preferred Name",
            givenName = "Unauthorized Given Name",
            homeTown = "Unauthorized Town",
            dateOfBirth = "2000-01-01",
            contacts = new object[]
            {
                new { id = guardianId, fullName = "Guardian One", relationship = "Parent", phoneNumber = "+94 77 111 1111", email = "guardian@example.test", isEmergencyContact = true, sortOrder = 0 },
                new { fullName = "Sibling Contact", relationship = "Sibling", phoneNumber = "+94 77 222 2222", isEmergencyContact = false, sortOrder = 1 }
            }
        });
        var mine = await member.GetAsync("/api/members/me");
        var currentUser = await member.GetAsync("/api/auth/me");
        var mineBody = await mine.Content.ReadAsStringAsync();
        using var currentUserJson = JsonDocument.Parse(await currentUser.Content.ReadAsStringAsync());

        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        var official = await pastor.GetAsync($"/api/members/{profileId}");
        var officialBody = await official.Content.ReadAsStringAsync();
        var history = await pastor.GetAsync($"/api/members/{profileId}/history");

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal(profileId, currentUserJson.RootElement.GetProperty("memberProfileId").GetInt32());
        Assert.Contains("My Preferred Name", mineBody, StringComparison.Ordinal);
        Assert.Contains("Sibling Contact", mineBody, StringComparison.Ordinal);
        Assert.Contains("Official", officialBody, StringComparison.Ordinal);
        Assert.Contains("Jaffna", officialBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Unauthorized Given Name", officialBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Unauthorized Town", officialBody, StringComparison.Ordinal);
        Assert.Contains("MemberSelfUpdated", await history.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelfServiceRejectsGuardianIdentityChangesAndUnlinkedAccounts()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var profileId = await factory.CreateMemberProfileAsync("Owned", "Resident", ResidencyStatus.Active, "member@example.test");
        var guardianId = await factory.GetGuardianContactIdAsync(profileId);
        using var member = factory.CreateHttpsClient();
        using var leader = factory.CreateHttpsClient();
        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");
        member.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var guardianChange = await member.PutAsJsonAsync("/api/members/me", new
        {
            preferredName = "Allowed",
            contacts = new[] { new { id = guardianId, fullName = "Different Guardian", relationship = "Other", phoneNumber = "+94 77 000 0000", isEmergencyContact = true, sortOrder = 0 } }
        });
        await factory.LoginAsync(leader, "leader@example.test", "Jmh-test!2026");
        var unlinked = await leader.GetAsync("/api/members/me");

        Assert.Equal(HttpStatusCode.BadRequest, guardianChange.StatusCode);
        Assert.Contains("Pastor", await guardianChange.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, unlinked.StatusCode);
    }

    [Fact]
    public async Task ArchivedProfilesRemainPastorOnlyEvenForTheirLinkedOwner()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var profileId = await factory.CreateMemberProfileAsync("Archived", "Owner", ResidencyStatus.Archived, "member@example.test");
        using var member = factory.CreateHttpsClient();
        using var pastor = factory.CreateHttpsClient();
        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");

        var memberDetail = await member.GetAsync($"/api/members/{profileId}");
        var memberSelfDetail = await member.GetAsync("/api/members/me");
        var pastorDetail = await pastor.GetAsync($"/api/members/{profileId}");

        Assert.Equal(HttpStatusCode.Forbidden, memberDetail.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberSelfDetail.StatusCode);
        Assert.Contains("archived", await memberSelfDetail.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, pastorDetail.StatusCode);
    }

    [Fact]
    public async Task RestoreRequiresAntiforgeryAndDoesNotChangeStateWhenVerificationFails()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var profileId = await factory.CreateMemberProfileAsync("Protected", "Restore", ResidencyStatus.Archived);
        using var pastor = factory.CreateHttpsClient();
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");

        var response = await pastor.PostAsJsonAsync($"/api/members/{profileId}/restore", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ResidencyStatus.Archived, await factory.GetResidencyStatusAsync(profileId));
    }

    [Fact]
    public async Task PastorCanCreateEditAndArchiveAStudentProfileWithoutALoginAccount()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var pastor = factory.CreateHttpsClient();
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        pastor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var create = await pastor.PostAsJsonAsync("/api/members", new
        {
            givenName = "Nila",
            familyName = "Student",
            homeTown = "Jaffna",
            residentType = "Student",
            dateOfBirth = "2010-03-04",
            studentDetails = new { schoolName = "Jaffna College", educationLevel = "Grade 10" },
            contacts = new[] { new { fullName = "Guardian One", relationship = "Parent", phoneNumber = "+94 77 000 0000", isGuardian = true, isEmergencyContact = true, sortOrder = 0 } }
        });
        var created = await create.Content.ReadFromJsonAsync<MemberResponse>();

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(created);
        Assert.Null(await factory.GetLoginAccountIdAsync(created!.Id));

        var edit = await pastor.PutAsJsonAsync($"/api/members/{created.Id}", new
        {
            givenName = "Nila",
            familyName = "Student Updated",
            homeTown = "Jaffna",
            residentType = "Student",
            dateOfBirth = "2010-03-04",
            studentDetails = new { schoolName = "Jaffna College", educationLevel = "Grade 11" }
        });
        var archive = await pastor.PostAsJsonAsync($"/api/members/{created.Id}/archive", new { });
        var list = await pastor.GetAsync("/api/members?residentType=Student");

        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.DoesNotContain("Nila", await list.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(ResidencyStatus.Archived, await factory.GetResidencyStatusAsync(created.Id));
    }

    [Fact]
    public async Task OnlyPastorsCanViewArchivedHistoryAndRestoreAnArchivedProfile()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var profileId = await factory.CreateMemberProfileAsync("Former", "Resident", ResidencyStatus.Archived);
        using var pastor = factory.CreateHttpsClient();
        using var leader = factory.CreateHttpsClient();
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        var pastorToken = factory.AntiforgeryToken!;
        await factory.LoginAsync(leader, "leader@example.test", "Jmh-test!2026");
        var leaderToken = factory.AntiforgeryToken!;
        pastor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", pastorToken);
        leader.DefaultRequestHeaders.Add("X-CSRF-TOKEN", leaderToken);

        var pastorList = await pastor.GetAsync("/api/members/archived");
        var pastorDetail = await pastor.GetAsync($"/api/members/archived/{profileId}");
        var audit = await pastor.GetAsync($"/api/members/{profileId}/history");
        var leaderList = await leader.GetAsync("/api/members/archived");
        var leaderRestore = await leader.PostAsJsonAsync($"/api/members/{profileId}/restore", new { });
        var restore = await pastor.PostAsJsonAsync($"/api/members/{profileId}/restore", new { });
        var activeList = await pastor.GetAsync("/api/members");
        var refreshedArchive = await pastor.GetAsync("/api/members/archived");
        var repeatRestore = await pastor.PostAsJsonAsync($"/api/members/{profileId}/restore", new { });

        Assert.Equal(HttpStatusCode.OK, pastorList.StatusCode);
        Assert.Contains("Former", await pastorList.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, pastorDetail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, leaderList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, leaderRestore.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);
        Assert.Contains("Former", await activeList.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("Former", await refreshedArchive.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, repeatRestore.StatusCode);
        Assert.Equal(ResidencyStatus.Active, await factory.GetResidencyStatusAsync(profileId));
        Assert.Contains("MemberRestored", await (await pastor.GetAsync($"/api/members/{profileId}/history")).Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PastorManagesDevotionsWhileOtherRolesReceiveReadOnlySchedules()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var pastor = factory.CreateHttpsClient();
        using var member = factory.CreateHttpsClient();
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        var pastorToken = factory.AntiforgeryToken!;
        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");
        var memberToken = factory.AntiforgeryToken!;
        pastor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", pastorToken);
        member.DefaultRequestHeaders.Add("X-CSRF-TOKEN", memberToken);
        var request = new { title = "Evening Devotion", scheduledStartUtc = "2026-11-01T13:00:00Z", durationMinutes = 45, location = "JMH Hall" };

        var forbiddenCreate = await member.PostAsJsonAsync("/api/devotions", request);
        var create = await pastor.PostAsJsonAsync("/api/devotions", request);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var sessionId = created.RootElement.GetProperty("id").GetInt32();
        var memberList = await member.GetAsync("/api/devotions");
        var memberAudit = await member.GetAsync($"/api/devotions/{sessionId}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Contains("Evening Devotion", await memberList.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, memberAudit.StatusCode);
    }

    [Fact]
    public async Task AttendanceUsesOnlyActiveResidentsAndMemberReadsOnlyOwnHistory()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var activeId = await factory.CreateMemberProfileAsync("Active", "Member", ResidencyStatus.Active, "member@example.test");
        var otherActiveId = await factory.CreateMemberProfileAsync("Other", "Active", ResidencyStatus.Active);
        var archivedId = await factory.CreateMemberProfileAsync("Former", "Resident", ResidencyStatus.Archived);
        using var pastor = factory.CreateHttpsClient();
        using var member = factory.CreateHttpsClient();
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        var pastorToken = factory.AntiforgeryToken!;
        pastor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", pastorToken);
        var create = await pastor.PostAsJsonAsync("/api/devotions", new { title = "Morning Devotion", scheduledStartUtc = "2026-11-02T00:30:00Z", durationMinutes = 30 });
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var sessionId = created.RootElement.GetProperty("id").GetInt32();

        var archivedAttempt = await pastor.PutAsJsonAsync($"/api/devotions/{sessionId}/attendance", new
        {
            entries = new[] { new { memberProfileId = archivedId, status = 1, notes = "Must be rejected" } }
        });
        var save = await pastor.PutAsJsonAsync($"/api/devotions/{sessionId}/attendance", new
        {
            entries = new[] { new { memberProfileId = activeId, status = 1, notes = "On time" }, new { memberProfileId = otherActiveId, status = 2, notes = "Away" } }
        });
        var roster = await pastor.GetAsync($"/api/devotions/{sessionId}/attendance");
        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");
        var own = await member.GetAsync("/api/devotions/my-attendance");
        var ownBody = await own.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, archivedAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        var rosterBody = await roster.Content.ReadAsStringAsync();
        Assert.Contains("Active Member", rosterBody, StringComparison.Ordinal);
        Assert.Contains("Other Active", rosterBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Former Resident", rosterBody, StringComparison.Ordinal);
        Assert.Contains("Morning Devotion", ownBody, StringComparison.Ordinal);
        Assert.DoesNotContain("On time", ownBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Away", ownBody, StringComparison.Ordinal);

        var clear = await pastor.PutAsJsonAsync($"/api/devotions/{sessionId}/attendance", new
        {
            entries = new[] { new { memberProfileId = activeId, status = (int?)null, notes = (string?)null } }
        });
        var clearedRoster = await pastor.GetAsync($"/api/devotions/{sessionId}/attendance");
        using var clearedJson = JsonDocument.Parse(await clearedRoster.Content.ReadAsStringAsync());
        var clearedMember = clearedJson.RootElement.EnumerateArray().Single(item => item.GetProperty("memberProfileId").GetInt32() == activeId);
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);
        Assert.Equal(JsonValueKind.Null, clearedMember.GetProperty("status").ValueKind);
    }

    [Fact]
    public async Task DutiesRequireActiveAssigneesAndMembersCompleteOnlyTheirOwnAssignments()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var ownedId = await factory.CreateMemberProfileAsync("Duty", "Owner", ResidencyStatus.Active, "member@example.test");
        var otherId = await factory.CreateMemberProfileAsync("Other", "Resident", ResidencyStatus.Active);
        var archivedId = await factory.CreateMemberProfileAsync("Former", "Resident", ResidencyStatus.Archived);
        using var pastor = factory.CreateHttpsClient();
        using var member = factory.CreateHttpsClient();
        using var leader = factory.CreateHttpsClient();
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        pastor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);
        var archivedAttempt = await pastor.PostAsJsonAsync("/api/duties", new
        {
            title = "Archived duty", assignedMemberProfileId = archivedId, scheduledForUtc = "2026-11-03T01:00:00Z"
        });
        var ownCreate = await pastor.PostAsJsonAsync("/api/duties", new
        {
            title = "Prepare the hall", assignedMemberProfileId = ownedId, scheduledForUtc = "2026-11-03T01:00:00Z", dueUtc = "2026-11-03T02:00:00Z"
        });
        var otherCreate = await pastor.PostAsJsonAsync("/api/duties", new
        {
            title = "Garden care", assignedMemberProfileId = otherId, scheduledForUtc = "2026-11-04T01:00:00Z"
        });
        using var ownJson = JsonDocument.Parse(await ownCreate.Content.ReadAsStringAsync());
        using var otherJson = JsonDocument.Parse(await otherCreate.Content.ReadAsStringAsync());
        var ownDutyId = ownJson.RootElement.GetProperty("id").GetInt32();
        var otherDutyId = otherJson.RootElement.GetProperty("id").GetInt32();

        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");
        member.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);
        var memberList = await member.GetAsync("/api/duties");
        var memberBody = await memberList.Content.ReadAsStringAsync();
        var deniedOtherCompletion = await member.PostAsync($"/api/duties/{otherDutyId}/complete", null);
        var completeOwn = await member.PostAsync($"/api/duties/{ownDutyId}/complete", null);
        var memberAudit = await member.GetAsync($"/api/duties/{ownDutyId}/audit");
        var memberCreate = await member.PostAsJsonAsync("/api/duties", new
        {
            title = "Not permitted", assignedMemberProfileId = ownedId, scheduledForUtc = "2026-11-05T01:00:00Z"
        });

        await factory.LoginAsync(leader, "leader@example.test", "Jmh-test!2026");
        var leaderList = await leader.GetAsync("/api/duties");
        var pastorAudit = await pastor.GetAsync($"/api/duties/{ownDutyId}/audit");

        Assert.Equal(HttpStatusCode.BadRequest, archivedAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.Created, ownCreate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, memberList.StatusCode);
        Assert.Contains("Prepare the hall", memberBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Garden care", memberBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, deniedOtherCompletion.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, completeOwn.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberAudit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, leaderList.StatusCode);
        Assert.Contains("DutyCompleted", await pastorAudit.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommunityEventsAreAuthenticatedAndPastorManagedWithAuditHistory()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var pastor = factory.CreateHttpsClient();
        using var member = factory.CreateHttpsClient();
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        pastor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);
        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");
        member.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);
        var request = new { title = "JMH Family Evening", startsUtc = "2026-11-06T12:30:00Z", endsUtc = "2026-11-06T14:30:00Z", location = "JMH Hall" };

        var deniedCreate = await member.PostAsJsonAsync("/api/events", request);
        var create = await pastor.PostAsJsonAsync("/api/events", request);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var eventId = created.RootElement.GetProperty("id").GetInt32();
        var memberList = await member.GetAsync("/api/events");
        var memberAudit = await member.GetAsync($"/api/events/{eventId}/audit");
        var update = await pastor.PutAsJsonAsync($"/api/events/{eventId}", new
        {
            title = "JMH Community Evening", startsUtc = "2026-11-06T12:30:00Z", endsUtc = "2026-11-06T15:00:00Z", location = "JMH Hall"
        });
        var cancel = await pastor.PostAsync($"/api/events/{eventId}/cancel", null);
        var audit = await pastor.GetAsync($"/api/events/{eventId}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, deniedCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Contains("JMH Family Evening", await memberList.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, memberAudit.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var auditBody = await audit.Content.ReadAsStringAsync();
        Assert.Contains("EventCreated", auditBody, StringComparison.Ordinal);
        Assert.Contains("EventUpdated", auditBody, StringComparison.Ordinal);
        Assert.Contains("EventCancelled", auditBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PastorCannotCreateMixedStudentAndWorkerDetails()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var pastor = factory.CreateHttpsClient();
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        pastor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var response = await pastor.PostAsJsonAsync("/api/members", new
        {
            givenName = "Invalid",
            familyName = "Profile",
            homeTown = "Jaffna",
            residentType = "Student",
            workerDetails = new { employerName = "External Company" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HomeTownIsRequiredForNewProfilesAndReturnedToAuthenticatedUsers()
    {
        await using var factory = await ApiFactory.CreateAsync();
        using var pastor = factory.CreateHttpsClient();
        using var leader = factory.CreateHttpsClient();
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        pastor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", factory.AntiforgeryToken!);

        var missingHomeTown = await pastor.PostAsJsonAsync("/api/members", new
        {
            givenName = "Missing",
            familyName = "HomeTown",
            residentType = "Student",
            studentDetails = new { schoolName = "Jaffna College" }
        });
        var create = await pastor.PostAsJsonAsync("/api/members", new
        {
            givenName = "Nila",
            familyName = "HomeTown",
            homeTown = "Jaffna",
            residentType = "Student",
            dateOfBirth = "2010-03-04",
            studentDetails = new { schoolName = "Jaffna College" }
        });
        await factory.LoginAsync(leader, "leader@example.test", "Jmh-test!2026");
        var list = await leader.GetAsync("/api/members");

        Assert.Equal(HttpStatusCode.BadRequest, missingHomeTown.StatusCode);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Contains("Jaffna", await list.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PhotoWorkflowEnforcesOwnershipConsentApprovalAndArchivedPrivacy()
    {
        await using var factory = await ApiFactory.CreateAsync();
        var profileId = await factory.CreateMemberProfileAsync("Photo", "Resident", ResidencyStatus.Active, "member@example.test");
        var guardianContactId = await factory.GetGuardianContactIdAsync(profileId);
        using var member = factory.CreateHttpsClient();
        using var leader = factory.CreateHttpsClient();
        using var pastor = factory.CreateHttpsClient();
        await factory.LoginAsync(member, "member@example.test", "Jmh-test!2026");
        var memberToken = factory.AntiforgeryToken!;
        await factory.LoginAsync(leader, "leader@example.test", "Jmh-test!2026");
        var leaderToken = factory.AntiforgeryToken!;
        await factory.LoginAsync(pastor, "pastor@example.test", "Jmh-test!2026");
        var pastorToken = factory.AntiforgeryToken!;

        var withoutConsent = await UploadPngAsync(member, memberToken, profileId);
        var otherUserUpload = await UploadPngAsync(leader, leaderToken, profileId);
        pastor.DefaultRequestHeaders.Add("X-CSRF-TOKEN", pastorToken);
        var consent = await pastor.PostAsJsonAsync($"/api/members/{profileId}/photo-consent", new
        {
            guardianContactId,
            evidenceReference = "dummy-consent-record"
        });
        var upload = await UploadPngAsync(member, memberToken, profileId);
        var uploaded = await upload.Content.ReadFromJsonAsync<PhotoResponse>();
        var pendingPublicView = await leader.GetAsync($"/api/members/{profileId}/photo");
        var ownerManagement = await member.GetAsync($"/api/members/{profileId}/photo-management");
        var leaderManagement = await leader.GetAsync($"/api/members/{profileId}/photo-management");
        var approve = await pastor.PostAsJsonAsync($"/api/members/{profileId}/photos/{uploaded!.Id}/approve", new { });
        var approvedPublicView = await leader.GetAsync($"/api/members/{profileId}/photo");
        var replacement = await UploadPngAsync(member, memberToken, profileId);
        var duplicatePending = await UploadPngAsync(member, memberToken, profileId);
        var replacementPhoto = await replacement.Content.ReadFromJsonAsync<PhotoResponse>();
        var currentRemainsVisible = await leader.GetAsync($"/api/members/{profileId}/photo");
        var rejectReplacement = await pastor.PostAsJsonAsync($"/api/members/{profileId}/photos/{replacementPhoto!.Id}/reject", new { });
        var archive = await pastor.PostAsJsonAsync($"/api/members/{profileId}/archive", new { });
        var archivedLeaderView = await leader.GetAsync($"/api/members/{profileId}/photo");
        var archivedPastorView = await pastor.GetAsync($"/api/members/{profileId}/photo");
        var withdrawConsent = await pastor.PostAsJsonAsync($"/api/members/{profileId}/photo-consent/withdraw", new { });
        var withdrawnPastorView = await pastor.GetAsync($"/api/members/{profileId}/photo");
        var audit = await pastor.GetAsync($"/api/members/{profileId}/history");

        Assert.Equal(HttpStatusCode.BadRequest, withoutConsent.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherUserUpload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        Assert.NotNull(uploaded);
        Assert.Equal(HttpStatusCode.NotFound, pendingPublicView.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerManagement.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, leaderManagement.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approvedPublicView.StatusCode);
        Assert.Equal("image/png", approvedPublicView.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Accepted, replacement.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicatePending.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentRemainsVisible.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, rejectReplacement.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, archivedLeaderView.StatusCode);
        Assert.Equal(HttpStatusCode.OK, archivedPastorView.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, withdrawConsent.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, withdrawnPastorView.StatusCode);
        var auditBody = await audit.Content.ReadAsStringAsync();
        Assert.Contains("PhotoConsentGranted", auditBody, StringComparison.Ordinal);
        Assert.Contains("PhotoSubmitted", auditBody, StringComparison.Ordinal);
        Assert.Contains("PhotoApproved", auditBody, StringComparison.Ordinal);
        Assert.Contains("PhotoRejected", auditBody, StringComparison.Ordinal);
        Assert.Contains("PhotoConsentWithdrawn", auditBody, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> UploadPngAsync(HttpClient client, string token, int profileId)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        using var image = new ByteArrayContent(png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var body = new MultipartFormDataContent();
        body.Add(image, "file", "profile.png");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/members/{profileId}/photo") { Content = body };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string databaseName = $"JmhConnectTests-{Guid.NewGuid():N}";

        public RecordingSetupEmailSender EmailSender { get; } = new();
        public string? AntiforgeryToken { get; private set; }
        public string LastAuthCookie { get; private set; } = string.Empty;

        public static async Task<ApiFactory> CreateAsync()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", Environments.Development);
            Environment.SetEnvironmentVariable("DataProtection__KeysPath", Path.Combine(Path.GetTempPath(), "JmhConnect-ApiTests-DataProtection"));
            var factory = new ApiFactory();
            await factory.InitializeAsync();
            return factory;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.RemoveAll<IAccountSetupEmailSender>();
                services.AddSingleton<IAccountSetupEmailSender>(EmailSender);
                services.RemoveAll<IProfilePhotoStorage>();
                services.AddSingleton<IProfilePhotoStorage, InMemoryPhotoStorage>();
                services.Configure<PasswordHasherOptions>(options => options.IterationCount = 1000);
            });
        }

        public HttpClient CreateHttpsClient()
        {
            return CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false
            });
        }

        public HttpClient CreateHttpClient()
        {
            return CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false
            });
        }

        private async Task InitializeAsync()
        {
            _ = Server;
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in ApplicationRoles.All)
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var role in ApplicationRoles.All)
            {
                var email = $"{role.ToLowerInvariant()}@example.test";
                var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                var createResult = await userManager.CreateAsync(user, "Jmh-test!2026");
                Assert.True(createResult.Succeeded, JsonSerializer.Serialize(createResult.Errors));
                var roleResult = await userManager.AddToRoleAsync(user, role);
                Assert.True(roleResult.Succeeded, JsonSerializer.Serialize(roleResult.Errors));
            }
        }

        public async Task LoginAsync(HttpClient client, string email, string password)
        {
            await GetAntiforgeryTokenAsync(client);

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email, password })
            };
            request.Headers.Add("X-CSRF-TOKEN", AntiforgeryToken);
            var response = await client.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"Login failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            LastAuthCookie = string.Join(";", response.Headers.GetValues("Set-Cookie"));

            await GetAntiforgeryTokenAsync(client);
        }

        public async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/auth/antiforgery");
            Assert.True(response.IsSuccessStatusCode,
                $"Antiforgery token request failed with {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            Assert.NotNull(tokenResponse);
            AntiforgeryToken = tokenResponse.RequestToken;
            return AntiforgeryToken;
        }

        public async Task SetEnabledAsync(string email, bool isEnabled)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            user.IsEnabled = isEnabled;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }

        public async Task<bool> IsLockedOutAsync(string email)
        {
            using var scope = Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            return await userManager.IsLockedOutAsync(user);
        }

        public async Task<int> CreateMemberProfileAsync(
            string givenName,
            string familyName,
            ResidencyStatus status,
            string? linkedAccountEmail = null)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = new MemberProfile
            {
                GivenName = givenName,
                FamilyName = familyName,
                HomeTown = "Jaffna",
                DateOfBirth = new DateOnly(2010, 3, 4),
                ResidentType = ResidentType.Student,
                ResidencyStatus = status,
                StudentDetails = new StudentResidentDetails { SchoolName = "Jaffna College" }
            };
            profile.Contacts.Add(new MemberContact
            {
                FullName = "Guardian One",
                Relationship = "Parent",
                PhoneNumber = "+94 77 000 0000",
                IsGuardian = true,
                IsEmergencyContact = true,
                SortOrder = 0
            });
            if (status == ResidencyStatus.Archived)
            {
                var pastor = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                    .FindByEmailAsync("pastor@example.test");
                Assert.NotNull(pastor);
                profile.ArchivedUtc = DateTimeOffset.UtcNow;
                profile.ArchivedByUserId = pastor!.Id;
            }

            db.MemberProfiles.Add(profile);
            await db.SaveChangesAsync();
            if (!string.IsNullOrWhiteSpace(linkedAccountEmail))
            {
                var account = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                    .FindByEmailAsync(linkedAccountEmail);
                Assert.NotNull(account);
                account!.MemberProfileId = profile.Id;
                Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().UpdateAsync(account)).Succeeded);
            }
            return profile.Id;
        }

        public async Task<ResidencyStatus> GetResidencyStatusAsync(int profileId)
        {
            using var scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MemberProfiles
                .Where(profile => profile.Id == profileId)
                .Select(profile => profile.ResidencyStatus)
                .SingleAsync();
        }

        public async Task<int> GetGuardianContactIdAsync(int profileId)
        {
            using var scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MemberContacts
                .Where(contact => contact.MemberProfileId == profileId && contact.IsGuardian)
                .Select(contact => contact.Id)
                .SingleAsync();
        }

        public async Task<string?> GetLoginAccountIdAsync(int profileId)
        {
            using var scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
                .Where(user => user.MemberProfileId == profileId)
                .Select(user => user.Id)
                .SingleOrDefaultAsync();
        }

        private sealed record TokenResponse(string RequestToken);

    }

    private sealed class RecordingSetupEmailSender : IAccountSetupEmailSender
    {
        public Uri? LastSetupLink { get; private set; }

        public Task SendSetupLinkAsync(string email, Uri setupLink, CancellationToken cancellationToken)
        {
            LastSetupLink = setupLink;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryPhotoStorage : IProfilePhotoStorage
    {
        private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

        public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
        {
            var key = $"{Guid.NewGuid():N}{extension}";
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            files[key] = buffer.ToArray();
            return key;
        }

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Stream?>(files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);
        }

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed record MemberResponse(int Id);
    private sealed record PhotoResponse(int Id);
}
