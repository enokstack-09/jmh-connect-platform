using JMHConnect.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JMHConnect.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<MemberProfile> MemberProfiles => Set<MemberProfile>();
    public DbSet<StudentResidentDetails> StudentResidentDetails => Set<StudentResidentDetails>();
    public DbSet<WorkerResidentDetails> WorkerResidentDetails => Set<WorkerResidentDetails>();
    public DbSet<MemberContact> MemberContacts => Set<MemberContact>();
    public DbSet<MemberProfilePhoto> MemberProfilePhotos => Set<MemberProfilePhoto>();
    public DbSet<PhotoGuardianConsent> PhotoGuardianConsents => Set<PhotoGuardianConsent>();
    public DbSet<MemberProfileAuditEntry> MemberProfileAuditEntries => Set<MemberProfileAuditEntry>();
    public DbSet<DevotionSession> DevotionSessions => Set<DevotionSession>();
    public DbSet<DevotionAttendance> DevotionAttendance => Set<DevotionAttendance>();
    public DbSet<DevotionSessionAuditEntry> DevotionSessionAuditEntries => Set<DevotionSessionAuditEntry>();
    public DbSet<DutyAssignment> DutyAssignments => Set<DutyAssignment>();
    public DbSet<DutyAssignmentAuditEntry> DutyAssignmentAuditEntries => Set<DutyAssignmentAuditEntry>();
    public DbSet<CommunityEvent> CommunityEvents => Set<CommunityEvent>();
    public DbSet<CommunityEventAuditEntry> CommunityEventAuditEntries => Set<CommunityEventAuditEntry>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementAuditEntry> AnnouncementAuditEntries => Set<AnnouncementAuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<IdentityRole>().ToTable("AspNetRoles", table =>
            table.HasCheckConstraint(
                "CK_AspNetRoles_ApplicationRole",
                "[Name] IS NOT NULL AND [Name] COLLATE Latin1_General_100_BIN2 IN (N'Pastor', N'Leader', N'Member')"));

        builder.Entity<ApplicationUser>()
            .HasOne(user => user.MemberProfile)
            .WithOne(profile => profile.LoginAccount)
            .HasForeignKey<ApplicationUser>(user => user.MemberProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.MemberProfileId)
            .IsUnique()
            .HasFilter("[MemberProfileId] IS NOT NULL");

        ConfigureMemberManagement(builder);
        ConfigureDevotions(builder);
        ConfigureDutiesAndEvents(builder);
        ConfigureAnnouncements(builder);
    }

    private static void ConfigureAnnouncements(ModelBuilder builder)
    {
        builder.Entity<Announcement>(entity =>
        {
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Body).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.Priority).HasConversion<byte>();
            entity.Property(item => item.Audience).HasConversion<byte>();
            entity.Property(item => item.Status).HasConversion<byte>();
            entity.Property(item => item.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.Status, item.Audience, item.Priority, item.PublishedUtc });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Announcements_Status", "[Status] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Announcements_Priority", "[Priority] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Announcements_Audience", "[Audience] IN (1, 2, 3, 4)");
                table.HasCheckConstraint("CK_Announcements_Lifecycle",
                    "([Status] = 1 AND [PublishedUtc] IS NULL AND [ArchivedUtc] IS NULL) OR " +
                    "([Status] = 2 AND [PublishedUtc] IS NOT NULL AND [ArchivedUtc] IS NULL) OR " +
                    "([Status] = 3 AND [ArchivedUtc] IS NOT NULL)");
            });
            entity.HasOne(item => item.CreatedByUser).WithMany()
                .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AnnouncementAuditEntry>(entity =>
        {
            entity.Property(entry => entry.ActionType).HasMaxLength(80).IsRequired();
            entity.Property(entry => entry.ActorUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(entry => new { entry.AnnouncementId, entry.OccurredUtc });
            entity.HasIndex(entry => entry.CorrelationId);
            entity.HasOne(entry => entry.Announcement).WithMany(item => item.AuditEntries)
                .HasForeignKey(entry => entry.AnnouncementId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(entry => entry.ActorUser).WithMany()
                .HasForeignKey(entry => entry.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDutiesAndEvents(ModelBuilder builder)
    {
        builder.Entity<DutyAssignment>(entity =>
        {
            entity.Property(duty => duty.Title).HasMaxLength(200).IsRequired();
            entity.Property(duty => duty.Description).HasMaxLength(1000);
            entity.Property(duty => duty.Status).HasConversion<byte>();
            entity.Property(duty => duty.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(duty => duty.CompletedByUserId).HasMaxLength(450);
            entity.Property(duty => duty.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(duty => new { duty.AssignedMemberProfileId, duty.Status, duty.ScheduledForUtc });
            entity.HasIndex(duty => new { duty.Status, duty.ScheduledForUtc });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_DutyAssignments_Status", "[Status] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_DutyAssignments_Schedule", "[DueUtc] IS NULL OR [DueUtc] >= [ScheduledForUtc]");
                table.HasCheckConstraint("CK_DutyAssignments_Completion",
                    "([Status] = 2 AND [CompletedUtc] IS NOT NULL AND [CompletedByUserId] IS NOT NULL) OR " +
                    "([Status] IN (1, 3) AND [CompletedUtc] IS NULL AND [CompletedByUserId] IS NULL)");
            });
            entity.HasOne(duty => duty.AssignedMemberProfile).WithMany(profile => profile.DutyAssignments)
                .HasForeignKey(duty => duty.AssignedMemberProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(duty => duty.CreatedByUser).WithMany()
                .HasForeignKey(duty => duty.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(duty => duty.CompletedByUser).WithMany()
                .HasForeignKey(duty => duty.CompletedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DutyAssignmentAuditEntry>(entity =>
        {
            entity.Property(entry => entry.ActionType).HasMaxLength(80).IsRequired();
            entity.Property(entry => entry.ActorUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(entry => new { entry.DutyAssignmentId, entry.OccurredUtc });
            entity.HasIndex(entry => entry.CorrelationId);
            entity.HasOne(entry => entry.DutyAssignment).WithMany(duty => duty.AuditEntries)
                .HasForeignKey(entry => entry.DutyAssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(entry => entry.ActorUser).WithMany()
                .HasForeignKey(entry => entry.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CommunityEvent>(entity =>
        {
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2000);
            entity.Property(item => item.Location).HasMaxLength(200);
            entity.Property(item => item.Status).HasConversion<byte>();
            entity.Property(item => item.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.Status, item.StartsUtc });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_CommunityEvents_Status", "[Status] IN (1, 2)");
                table.HasCheckConstraint("CK_CommunityEvents_Schedule", "[EndsUtc] > [StartsUtc]");
            });
            entity.HasOne(item => item.CreatedByUser).WithMany()
                .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CommunityEventAuditEntry>(entity =>
        {
            entity.Property(entry => entry.ActionType).HasMaxLength(80).IsRequired();
            entity.Property(entry => entry.ActorUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(entry => new { entry.CommunityEventId, entry.OccurredUtc });
            entity.HasIndex(entry => entry.CorrelationId);
            entity.HasOne(entry => entry.CommunityEvent).WithMany(item => item.AuditEntries)
                .HasForeignKey(entry => entry.CommunityEventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(entry => entry.ActorUser).WithMany()
                .HasForeignKey(entry => entry.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureDevotions(ModelBuilder builder)
    {
        builder.Entity<DevotionSession>(entity =>
        {
            entity.Property(session => session.Title).HasMaxLength(200).IsRequired();
            entity.Property(session => session.Location).HasMaxLength(200);
            entity.Property(session => session.Notes).HasMaxLength(1000);
            entity.Property(session => session.Status).HasConversion<byte>();
            entity.Property(session => session.CreatedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(session => session.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(session => new { session.Status, session.ScheduledStartUtc });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_DevotionSessions_Status", "[Status] IN (1, 2)");
                table.HasCheckConstraint("CK_DevotionSessions_Duration", "[DurationMinutes] BETWEEN 15 AND 240");
            });
            entity.HasOne(session => session.CreatedByUser).WithMany()
                .HasForeignKey(session => session.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DevotionAttendance>(entity =>
        {
            entity.Property(attendance => attendance.Status).HasConversion<byte>();
            entity.Property(attendance => attendance.Notes).HasMaxLength(500);
            entity.Property(attendance => attendance.RecordedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(attendance => attendance.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(attendance => new { attendance.DevotionSessionId, attendance.MemberProfileId }).IsUnique();
            entity.HasIndex(attendance => new { attendance.MemberProfileId, attendance.DevotionSessionId });
            entity.ToTable(table => table.HasCheckConstraint("CK_DevotionAttendance_Status", "[Status] IN (1, 2, 3)"));
            entity.HasOne(attendance => attendance.DevotionSession).WithMany(session => session.Attendance)
                .HasForeignKey(attendance => attendance.DevotionSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(attendance => attendance.MemberProfile).WithMany(profile => profile.DevotionAttendance)
                .HasForeignKey(attendance => attendance.MemberProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(attendance => attendance.RecordedByUser).WithMany()
                .HasForeignKey(attendance => attendance.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DevotionSessionAuditEntry>(entity =>
        {
            entity.Property(entry => entry.ActionType).HasMaxLength(80).IsRequired();
            entity.Property(entry => entry.ActorUserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(entry => new { entry.DevotionSessionId, entry.OccurredUtc });
            entity.HasIndex(entry => entry.CorrelationId);
            entity.HasOne(entry => entry.DevotionSession).WithMany(session => session.AuditEntries)
                .HasForeignKey(entry => entry.DevotionSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(entry => entry.ActorUser).WithMany()
                .HasForeignKey(entry => entry.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureMemberManagement(ModelBuilder builder)
    {
        builder.Entity<MemberProfile>(entity =>
        {
            entity.Property(profile => profile.GivenName).HasMaxLength(100);
            entity.Property(profile => profile.FamilyName).HasMaxLength(100);
            entity.Property(profile => profile.PreferredName).HasMaxLength(100);
            entity.Property(profile => profile.HomeTown).HasMaxLength(100);
            entity.Property(profile => profile.ResidentType).HasConversion<byte?>();
            entity.Property(profile => profile.ResidencyStatus).HasConversion<byte>()
                .HasDefaultValue(ResidencyStatus.Active)
                .HasSentinel((ResidencyStatus)0);
            entity.Property(profile => profile.ArchivedByUserId).HasMaxLength(450);
            entity.Property(profile => profile.IsBirthdayVisible).HasDefaultValue(false);
            entity.Property(profile => profile.BirthdayVisibilityUpdatedByUserId).HasMaxLength(450);
            entity.Property(profile => profile.CreatedUtc).HasDefaultValueSql("CONVERT(datetimeoffset(7), SYSUTCDATETIME())");
            entity.Property(profile => profile.UpdatedUtc).HasDefaultValueSql("CONVERT(datetimeoffset(7), SYSUTCDATETIME())");
            entity.Property(profile => profile.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(profile => new { profile.ResidencyStatus, profile.FamilyName, profile.GivenName });
            entity.HasIndex(profile => new { profile.ResidencyStatus, profile.IsBirthdayVisible });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_MemberProfiles_ResidentType",
                    "[ResidentType] IS NULL OR [ResidentType] IN (1, 2)");
                table.HasCheckConstraint("CK_MemberProfiles_ResidencyStatus", "[ResidencyStatus] IN (1, 2)");
                table.HasCheckConstraint("CK_MemberProfiles_ArchiveMetadata",
                    "([ResidencyStatus] = 1 AND [ArchivedUtc] IS NULL AND [ArchivedByUserId] IS NULL) OR " +
                    "([ResidencyStatus] = 2 AND [ArchivedUtc] IS NOT NULL AND [ArchivedByUserId] IS NOT NULL)");
                table.HasCheckConstraint("CK_MemberProfiles_BirthdayVisibilityMetadata",
                    "([BirthdayVisibilityUpdatedUtc] IS NULL AND [BirthdayVisibilityUpdatedByUserId] IS NULL) OR " +
                    "([BirthdayVisibilityUpdatedUtc] IS NOT NULL AND [BirthdayVisibilityUpdatedByUserId] IS NOT NULL)");
            });
            entity.HasOne(profile => profile.ArchivedByUser)
                .WithMany()
                .HasForeignKey(profile => profile.ArchivedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(profile => profile.BirthdayVisibilityUpdatedByUser)
                .WithMany()
                .HasForeignKey(profile => profile.BirthdayVisibilityUpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(profile => profile.StudentDetails)
                .WithOne(details => details.MemberProfile)
                .HasForeignKey<StudentResidentDetails>(details => details.MemberProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(profile => profile.WorkerDetails)
                .WithOne(details => details.MemberProfile)
                .HasForeignKey<WorkerResidentDetails>(details => details.MemberProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StudentResidentDetails>(entity =>
        {
            entity.HasKey(details => details.MemberProfileId);
            entity.Property(details => details.SchoolName).HasMaxLength(200);
            entity.Property(details => details.EducationLevel).HasMaxLength(100);
            entity.Property(details => details.StudentIdentifier).HasMaxLength(100);
            entity.Property(details => details.RowVersion).IsRowVersion().IsConcurrencyToken();
        });

        builder.Entity<WorkerResidentDetails>(entity =>
        {
            entity.HasKey(details => details.MemberProfileId);
            entity.Property(details => details.EmployerName).HasMaxLength(200);
            entity.Property(details => details.JobTitle).HasMaxLength(150);
            entity.Property(details => details.WorkLocation).HasMaxLength(200);
            entity.Property(details => details.WorkPhone).HasMaxLength(50);
            entity.Property(details => details.RowVersion).IsRowVersion().IsConcurrencyToken();
        });

        builder.Entity<MemberContact>(entity =>
        {
            entity.Property(contact => contact.FullName).HasMaxLength(200).IsRequired();
            entity.Property(contact => contact.Relationship).HasMaxLength(100).IsRequired();
            entity.Property(contact => contact.PhoneNumber).HasMaxLength(50);
            entity.Property(contact => contact.Email).HasMaxLength(254);
            entity.Property(contact => contact.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(contact => new { contact.MemberProfileId, contact.SortOrder }).IsUnique();
            entity.HasIndex(contact => new { contact.MemberProfileId, contact.IsEmergencyContact });
            entity.ToTable(table => table.HasCheckConstraint("CK_MemberContacts_SortOrder", "[SortOrder] >= 0"));
            entity.HasOne(contact => contact.MemberProfile)
                .WithMany(profile => profile.Contacts)
                .HasForeignKey(contact => contact.MemberProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MemberProfilePhoto>(entity =>
        {
            entity.Property(photo => photo.StorageKey).HasMaxLength(512).IsRequired();
            entity.Property(photo => photo.OriginalFileName).HasMaxLength(255);
            entity.Property(photo => photo.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(photo => photo.ApprovalStatus).HasConversion<byte>();
            entity.Property(photo => photo.ApprovedByUserId).HasMaxLength(450);
            entity.Property(photo => photo.CreatedByUserId).HasMaxLength(450);
            entity.Property(photo => photo.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(photo => photo.MemberProfileId).IsUnique().HasFilter("[IsCurrent] = 1");
            entity.HasIndex(photo => new { photo.MemberProfileId, photo.ApprovalStatus });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_MemberProfilePhotos_FileSize", "[FileSizeBytes] > 0");
                table.HasCheckConstraint("CK_MemberProfilePhotos_ApprovalStatus", "[ApprovalStatus] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_MemberProfilePhotos_ApprovalMetadata",
                    "([ApprovalStatus] = 2 AND [ApprovedUtc] IS NOT NULL AND [ApprovedByUserId] IS NOT NULL) OR " +
                    "([ApprovalStatus] IN (1, 3) AND [ApprovedUtc] IS NULL AND [ApprovedByUserId] IS NULL)");
            });
            entity.HasOne(photo => photo.MemberProfile)
                .WithMany(profile => profile.Photos)
                .HasForeignKey(photo => photo.MemberProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(photo => photo.ApprovedByUser)
                .WithMany()
                .HasForeignKey(photo => photo.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(photo => photo.CreatedByUser)
                .WithMany()
                .HasForeignKey(photo => photo.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PhotoGuardianConsent>(entity =>
        {
            entity.Property(consent => consent.ConsentStatus).HasConversion<byte>();
            entity.Property(consent => consent.RecordedByUserId).HasMaxLength(450).IsRequired();
            entity.Property(consent => consent.EvidenceReference).HasMaxLength(512);
            entity.Property(consent => consent.Notes).HasMaxLength(1000);
            entity.Property(consent => consent.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(consent => consent.MemberProfileId).IsUnique()
                .HasFilter("[ConsentStatus] = 1 AND [WithdrawnUtc] IS NULL");
            entity.ToTable(table => table.HasCheckConstraint("CK_PhotoGuardianConsents_Status",
                "([ConsentStatus] = 1 AND [WithdrawnUtc] IS NULL) OR ([ConsentStatus] = 2 AND [WithdrawnUtc] IS NOT NULL)"));
            entity.HasOne(consent => consent.MemberProfile)
                .WithMany(profile => profile.PhotoGuardianConsents)
                .HasForeignKey(consent => consent.MemberProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(consent => consent.GuardianContact)
                .WithMany()
                .HasForeignKey(consent => consent.GuardianContactId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(consent => consent.RecordedByUser)
                .WithMany()
                .HasForeignKey(consent => consent.RecordedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MemberProfileAuditEntry>(entity =>
        {
            entity.Property(entry => entry.ActionType).HasMaxLength(80).IsRequired();
            entity.HasIndex(entry => new { entry.MemberProfileId, entry.OccurredUtc });
            entity.HasIndex(entry => entry.CorrelationId);
            entity.HasOne(entry => entry.MemberProfile)
                .WithMany(profile => profile.AuditEntries)
                .HasForeignKey(entry => entry.MemberProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(entry => entry.ActorUser)
                .WithMany()
                .HasForeignKey(entry => entry.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
