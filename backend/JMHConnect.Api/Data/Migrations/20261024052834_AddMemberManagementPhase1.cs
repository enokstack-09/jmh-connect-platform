using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JMHConnect.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberManagementPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivedByUserId",
                table: "MemberProfiles",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedUtc",
                table: "MemberProfiles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedUtc",
                table: "MemberProfiles",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "CONVERT(datetimeoffset(7), SYSUTCDATETIME())");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "MemberProfiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FamilyName",
                table: "MemberProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GivenName",
                table: "MemberProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredName",
                table: "MemberProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ResidencyStatus",
                table: "MemberProfiles",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<byte>(
                name: "ResidentType",
                table: "MemberProfiles",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MemberProfiles",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedUtc",
                table: "MemberProfiles",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "CONVERT(datetimeoffset(7), SYSUTCDATETIME())");

            migrationBuilder.CreateTable(
                name: "MemberContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberProfileId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    IsEmergencyContact = table.Column<bool>(type: "bit", nullable: false),
                    IsGuardian = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberContacts", x => x.Id);
                    table.CheckConstraint("CK_MemberContacts_SortOrder", "[SortOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_MemberContacts_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberProfileAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberProfileId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberProfileAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberProfileAuditEntries_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberProfileAuditEntries_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberProfilePhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberProfileId = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ApprovalStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    ApprovedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberProfilePhotos", x => x.Id);
                    table.CheckConstraint("CK_MemberProfilePhotos_ApprovalMetadata", "([ApprovalStatus] = 2 AND [ApprovedUtc] IS NOT NULL AND [ApprovedByUserId] IS NOT NULL) OR ([ApprovalStatus] IN (1, 3) AND [ApprovedUtc] IS NULL AND [ApprovedByUserId] IS NULL)");
                    table.CheckConstraint("CK_MemberProfilePhotos_ApprovalStatus", "[ApprovalStatus] IN (1, 2, 3)");
                    table.CheckConstraint("CK_MemberProfilePhotos_FileSize", "[FileSizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_MemberProfilePhotos_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberProfilePhotos_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberProfilePhotos_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentResidentDetails",
                columns: table => new
                {
                    MemberProfileId = table.Column<int>(type: "int", nullable: false),
                    SchoolName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EducationLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StudentIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentResidentDetails", x => x.MemberProfileId);
                    table.ForeignKey(
                        name: "FK_StudentResidentDetails_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkerResidentDetails",
                columns: table => new
                {
                    MemberProfileId = table.Column<int>(type: "int", nullable: false),
                    EmployerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    WorkLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerResidentDetails", x => x.MemberProfileId);
                    table.ForeignKey(
                        name: "FK_WorkerResidentDetails_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhotoGuardianConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberProfileId = table.Column<int>(type: "int", nullable: false),
                    GuardianContactId = table.Column<int>(type: "int", nullable: false),
                    ConsentStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    GrantedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WithdrawnUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoGuardianConsents", x => x.Id);
                    table.CheckConstraint("CK_PhotoGuardianConsents_Status", "([ConsentStatus] = 1 AND [WithdrawnUtc] IS NULL) OR ([ConsentStatus] = 2 AND [WithdrawnUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PhotoGuardianConsents_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhotoGuardianConsents_MemberContacts_GuardianContactId",
                        column: x => x.GuardianContactId,
                        principalTable: "MemberContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhotoGuardianConsents_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_ArchivedByUserId",
                table: "MemberProfiles",
                column: "ArchivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_ResidencyStatus_FamilyName_GivenName",
                table: "MemberProfiles",
                columns: new[] { "ResidencyStatus", "FamilyName", "GivenName" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MemberProfiles_ArchiveMetadata",
                table: "MemberProfiles",
                sql: "([ResidencyStatus] = 1 AND [ArchivedUtc] IS NULL AND [ArchivedByUserId] IS NULL) OR ([ResidencyStatus] = 2 AND [ArchivedUtc] IS NOT NULL AND [ArchivedByUserId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MemberProfiles_ResidencyStatus",
                table: "MemberProfiles",
                sql: "[ResidencyStatus] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MemberProfiles_ResidentType",
                table: "MemberProfiles",
                sql: "[ResidentType] IS NULL OR [ResidentType] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_MemberContacts_MemberProfileId_IsEmergencyContact",
                table: "MemberContacts",
                columns: new[] { "MemberProfileId", "IsEmergencyContact" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberContacts_MemberProfileId_SortOrder",
                table: "MemberContacts",
                columns: new[] { "MemberProfileId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfileAuditEntries_ActorUserId",
                table: "MemberProfileAuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfileAuditEntries_CorrelationId",
                table: "MemberProfileAuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfileAuditEntries_MemberProfileId_OccurredUtc",
                table: "MemberProfileAuditEntries",
                columns: new[] { "MemberProfileId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfilePhotos_ApprovedByUserId",
                table: "MemberProfilePhotos",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfilePhotos_CreatedByUserId",
                table: "MemberProfilePhotos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfilePhotos_MemberProfileId",
                table: "MemberProfilePhotos",
                column: "MemberProfileId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfilePhotos_MemberProfileId_ApprovalStatus",
                table: "MemberProfilePhotos",
                columns: new[] { "MemberProfileId", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoGuardianConsents_GuardianContactId",
                table: "PhotoGuardianConsents",
                column: "GuardianContactId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoGuardianConsents_MemberProfileId",
                table: "PhotoGuardianConsents",
                column: "MemberProfileId",
                unique: true,
                filter: "[ConsentStatus] = 1 AND [WithdrawnUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoGuardianConsents_RecordedByUserId",
                table: "PhotoGuardianConsents",
                column: "RecordedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberProfiles_AspNetUsers_ArchivedByUserId",
                table: "MemberProfiles",
                column: "ArchivedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberProfiles_AspNetUsers_ArchivedByUserId",
                table: "MemberProfiles");

            migrationBuilder.DropTable(
                name: "MemberProfileAuditEntries");

            migrationBuilder.DropTable(
                name: "MemberProfilePhotos");

            migrationBuilder.DropTable(
                name: "PhotoGuardianConsents");

            migrationBuilder.DropTable(
                name: "StudentResidentDetails");

            migrationBuilder.DropTable(
                name: "WorkerResidentDetails");

            migrationBuilder.DropTable(
                name: "MemberContacts");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_ArchivedByUserId",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_ResidencyStatus_FamilyName_GivenName",
                table: "MemberProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MemberProfiles_ArchiveMetadata",
                table: "MemberProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MemberProfiles_ResidencyStatus",
                table: "MemberProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MemberProfiles_ResidentType",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "ArchivedUtc",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "FamilyName",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "GivenName",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredName",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "ResidencyStatus",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "ResidentType",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                table: "MemberProfiles");
        }
    }
}
