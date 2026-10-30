using JMHConnect.Api.Data;
using JMHConnect.Api.Controllers;
using JMHConnect.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace JMHConnect.Api.Tests;

public sealed class MemberManagementModelTests
{
    [Fact]
    public void DevotionsControllerRouteMatchesTheAngularApiContract()
    {
        var controllerRoute = Assert.Single(typeof(DevotionsController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true).Cast<RouteAttribute>());
        var listRoute = Assert.Single(typeof(DevotionsController).GetMethod(nameof(DevotionsController.List))!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).Cast<HttpGetAttribute>());
        var createRoute = Assert.Single(typeof(DevotionsController).GetMethod(nameof(DevotionsController.Create))!
            .GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Cast<HttpPostAttribute>());

        Assert.Equal("api/devotions", controllerRoute.Template);
        Assert.Null(listRoute.Template);
        Assert.Null(createRoute.Template);
    }

    [Fact]
    public void DevotionAttendanceHasUniqueSessionResidentBoundaryAndRestrictiveRelationships()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"devotion-model-{Guid.NewGuid():N}").Options;
        using var db = new ApplicationDbContext(options);
        var attendance = db.Model.FindEntityType("JMHConnect.Api.Models.DevotionAttendance");
        Assert.NotNull(attendance);
        Assert.Contains(attendance!.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(["DevotionSessionId", "MemberProfileId"]));
        Assert.All(attendance.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void DutiesAndEventsPreserveRestrictiveHistoricalRelationships()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"duties-events-model-{Guid.NewGuid():N}").Options;
        using var db = new ApplicationDbContext(options);
        var duty = db.Model.FindEntityType("JMHConnect.Api.Models.DutyAssignment");
        var dutyAudit = db.Model.FindEntityType("JMHConnect.Api.Models.DutyAssignmentAuditEntry");
        var communityEvent = db.Model.FindEntityType("JMHConnect.Api.Models.CommunityEvent");
        var eventAudit = db.Model.FindEntityType("JMHConnect.Api.Models.CommunityEventAuditEntry");
        Assert.All(new[] { duty, dutyAudit, communityEvent, eventAudit }, entity => Assert.NotNull(entity));
        Assert.All(new[] { duty!, dutyAudit!, communityEvent!, eventAudit! }, entity =>
            Assert.All(entity.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior)));
        Assert.Contains(duty!.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual(["AssignedMemberProfileId", "Status", "ScheduledForUtc"]));
        Assert.Contains(communityEvent!.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual(["Status", "StartsUtc"]));
    }

    [Fact]
    public void BirthdaysDefaultToPrivateAndAnnouncementsKeepHistoricalRelationships()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"birthdays-announcements-model-{Guid.NewGuid():N}").Options;
        using var db = new ApplicationDbContext(options);
        var profile = db.Model.FindEntityType("JMHConnect.Api.Models.MemberProfile");
        var announcement = db.Model.FindEntityType("JMHConnect.Api.Models.Announcement");
        var audit = db.Model.FindEntityType("JMHConnect.Api.Models.AnnouncementAuditEntry");
        Assert.NotNull(profile);
        Assert.False((bool)profile!.FindProperty("IsBirthdayVisible")!.GetDefaultValue()!);
        Assert.Contains(profile.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual(["ResidencyStatus", "IsBirthdayVisible"]));
        Assert.NotNull(announcement);
        Assert.NotNull(audit);
        Assert.All(new[] { announcement!, audit! }, entity =>
            Assert.All(entity.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior)));
        Assert.Contains(announcement.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual(["Status", "Audience", "Priority", "PublishedUtc"]));
    }

    [Fact]
    public void BirthdayAndAnnouncementRoutesMatchAngularApiContracts()
    {
        var birthdayRoute = Assert.Single(typeof(BirthdaysController).GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>());
        var announcementRoute = Assert.Single(typeof(AnnouncementsController).GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>());
        Assert.Equal("api/birthdays", birthdayRoute.Template);
        Assert.Equal("api/announcements", announcementRoute.Template);
        Assert.Equal("manage", Assert.Single(typeof(BirthdaysController).GetMethod(nameof(BirthdaysController.Manage))!
            .GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>()).Template);
        Assert.Equal("{id:int}/publish", Assert.Single(typeof(AnnouncementsController).GetMethod(nameof(AnnouncementsController.Publish))!
            .GetCustomAttributes(typeof(HttpPostAttribute), true).Cast<HttpPostAttribute>()).Template);
    }

    [Fact]
    public void ReportsArePastorOnlyAndExposeOnlyReadContracts()
    {
        var authorize = Assert.Single(typeof(ReportsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        var route = Assert.Single(typeof(ReportsController).GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>());
        var csv = Assert.Single(typeof(ReportsController).GetMethod(nameof(ReportsController.Csv))!
            .GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>());
        Assert.Equal("PastorOnly", authorize.Policy);
        Assert.Equal("api/reports", route.Template);
        Assert.Equal("{kind}/csv", csv.Template);
    }

    [Fact]
    public async Task BirthdayListExcludesPrivateAndArchivedProfilesAndNeverSerializesDobData()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"birthday-privacy-{Guid.NewGuid():N}").Options;
        await using var db = new ApplicationDbContext(options);
        db.MemberProfiles.AddRange(
            new MemberProfile { GivenName = "Private", FamilyName = "Resident", DateOfBirth = new DateOnly(2010, 3, 4), ResidencyStatus = ResidencyStatus.Active },
            new MemberProfile { GivenName = "Shared", FamilyName = "Resident", DateOfBirth = new DateOnly(2010, 3, 5), IsBirthdayVisible = true, ResidencyStatus = ResidencyStatus.Active },
            new MemberProfile { GivenName = "Archived", FamilyName = "Resident", DateOfBirth = new DateOnly(2010, 3, 6), IsBirthdayVisible = true, ResidencyStatus = ResidencyStatus.Archived });
        await db.SaveChangesAsync();
        var controller = new BirthdaysController(db, null!);
        var response = await controller.List(CancellationToken.None);
        var result = Assert.IsType<OkObjectResult>(response.Result);
        var birthdays = Assert.IsAssignableFrom<IReadOnlyList<BirthdayResponse>>(result.Value);
        var birthday = Assert.Single(birthdays);
        Assert.Equal("Shared Resident", birthday.DisplayName);
        var json = JsonSerializer.Serialize(birthdays);
        Assert.DoesNotContain("dateOfBirth", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("age", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2010", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnnouncementListFiltersPublishedMessagesByMemberAudience()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"announcement-audience-{Guid.NewGuid():N}").Options;
        await using var db = new ApplicationDbContext(options);
        db.Announcements.AddRange(
            new Announcement { Title = "Everyone", Body = "Welcome", Audience = AnnouncementAudience.Everyone, Priority = AnnouncementPriority.Normal, Status = AnnouncementStatus.Published, PublishedUtc = DateTimeOffset.UtcNow, CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow, CreatedByUserId = "pastor" },
            new Announcement { Title = "Members", Body = "Member notice", Audience = AnnouncementAudience.Members, Priority = AnnouncementPriority.Normal, Status = AnnouncementStatus.Published, PublishedUtc = DateTimeOffset.UtcNow, CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow, CreatedByUserId = "pastor" },
            new Announcement { Title = "Draft", Body = "Private", Audience = AnnouncementAudience.Everyone, Priority = AnnouncementPriority.Normal, Status = AnnouncementStatus.Draft, CreatedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow, CreatedByUserId = "pastor" });
        await db.SaveChangesAsync();
        var controller = new AnnouncementsController(db, null!) { ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Member")], "test")) } } };
        var response = await controller.List(CancellationToken.None);
        var result = Assert.IsType<OkObjectResult>(response.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<AnnouncementResponse>>(result.Value);
        Assert.Equal(["Everyone", "Members"], items.Select(item => item.Title).Order());
    }
    [Fact]
    public void MemberManagementModelContainsRequiredDependentEntitiesAndProfileConstraints()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"member-management-model-{Guid.NewGuid():N}")
            .Options;
        using var db = new ApplicationDbContext(options);

        var profile = db.Model.FindEntityType("JMHConnect.Api.Models.MemberProfile");
        Assert.NotNull(profile);
        Assert.NotNull(profile.FindProperty("ResidentType"));
        Assert.NotNull(profile.FindProperty("ResidencyStatus"));
        Assert.NotNull(profile.FindProperty("ArchivedUtc"));
        Assert.NotNull(profile.FindProperty("RowVersion"));

        Assert.NotNull(db.Model.FindEntityType("JMHConnect.Api.Models.StudentResidentDetails"));
        Assert.NotNull(db.Model.FindEntityType("JMHConnect.Api.Models.WorkerResidentDetails"));
        Assert.NotNull(db.Model.FindEntityType("JMHConnect.Api.Models.MemberContact"));
        Assert.NotNull(db.Model.FindEntityType("JMHConnect.Api.Models.MemberProfilePhoto"));
        Assert.NotNull(db.Model.FindEntityType("JMHConnect.Api.Models.PhotoGuardianConsent"));
        Assert.NotNull(db.Model.FindEntityType("JMHConnect.Api.Models.MemberProfileAuditEntry"));

        Assert.Contains(profile.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(["ResidencyStatus", "FamilyName", "GivenName"]));

        var studentDetails = db.Model.FindEntityType("JMHConnect.Api.Models.StudentResidentDetails");
        var workerDetails = db.Model.FindEntityType("JMHConnect.Api.Models.WorkerResidentDetails");
        var contact = db.Model.FindEntityType("JMHConnect.Api.Models.MemberContact");
        var photo = db.Model.FindEntityType("JMHConnect.Api.Models.MemberProfilePhoto");
        var consent = db.Model.FindEntityType("JMHConnect.Api.Models.PhotoGuardianConsent");
        var audit = db.Model.FindEntityType("JMHConnect.Api.Models.MemberProfileAuditEntry");

        Assert.Equal("MemberProfileId", studentDetails!.FindPrimaryKey()!.Properties.Single().Name);
        Assert.Equal("MemberProfileId", workerDetails!.FindPrimaryKey()!.Properties.Single().Name);
        Assert.All(new[] { studentDetails, workerDetails, contact!, photo!, consent!, audit! }, entity =>
            Assert.All(entity.GetForeignKeys(), foreignKey =>
                Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior)));

        Assert.Contains(photo!.GetIndexes(), index =>
            index.IsUnique && index.GetFilter() == "[IsCurrent] = 1" &&
            index.Properties.Select(property => property.Name).SequenceEqual(["MemberProfileId"]));
        Assert.Contains(consent!.GetIndexes(), index =>
            index.IsUnique && index.GetFilter() == "[ConsentStatus] = 1 AND [WithdrawnUtc] IS NULL" &&
            index.Properties.Select(property => property.Name).SequenceEqual(["MemberProfileId"]));
    }
}
