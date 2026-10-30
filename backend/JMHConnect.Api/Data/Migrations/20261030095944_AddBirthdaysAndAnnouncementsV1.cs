using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JMHConnect.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBirthdaysAndAnnouncementsV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BirthdayVisibilityUpdatedByUserId",
                table: "MemberProfiles",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "BirthdayVisibilityUpdatedUtc",
                table: "MemberProfiles",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBirthdayVisible",
                table: "MemberProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false),
                    Audience = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    PublishedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchivedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.CheckConstraint("CK_Announcements_Audience", "[Audience] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Announcements_Lifecycle", "([Status] = 1 AND [PublishedUtc] IS NULL AND [ArchivedUtc] IS NULL) OR ([Status] = 2 AND [PublishedUtc] IS NOT NULL AND [ArchivedUtc] IS NULL) OR ([Status] = 3 AND [ArchivedUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_Announcements_Priority", "[Priority] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Announcements_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Announcements_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnnouncementId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementAuditEntries_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnnouncementAuditEntries_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_BirthdayVisibilityUpdatedByUserId",
                table: "MemberProfiles",
                column: "BirthdayVisibilityUpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_ResidencyStatus_IsBirthdayVisible",
                table: "MemberProfiles",
                columns: new[] { "ResidencyStatus", "IsBirthdayVisible" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MemberProfiles_BirthdayVisibilityMetadata",
                table: "MemberProfiles",
                sql: "([BirthdayVisibilityUpdatedUtc] IS NULL AND [BirthdayVisibilityUpdatedByUserId] IS NULL) OR ([BirthdayVisibilityUpdatedUtc] IS NOT NULL AND [BirthdayVisibilityUpdatedByUserId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAuditEntries_ActorUserId",
                table: "AnnouncementAuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAuditEntries_AnnouncementId_OccurredUtc",
                table: "AnnouncementAuditEntries",
                columns: new[] { "AnnouncementId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAuditEntries_CorrelationId",
                table: "AnnouncementAuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatedByUserId",
                table: "Announcements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_Status_Audience_Priority_PublishedUtc",
                table: "Announcements",
                columns: new[] { "Status", "Audience", "Priority", "PublishedUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_MemberProfiles_AspNetUsers_BirthdayVisibilityUpdatedByUserId",
                table: "MemberProfiles",
                column: "BirthdayVisibilityUpdatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberProfiles_AspNetUsers_BirthdayVisibilityUpdatedByUserId",
                table: "MemberProfiles");

            migrationBuilder.DropTable(
                name: "AnnouncementAuditEntries");

            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_BirthdayVisibilityUpdatedByUserId",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_ResidencyStatus_IsBirthdayVisible",
                table: "MemberProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MemberProfiles_BirthdayVisibilityMetadata",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "BirthdayVisibilityUpdatedByUserId",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "BirthdayVisibilityUpdatedUtc",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "IsBirthdayVisible",
                table: "MemberProfiles");
        }
    }
}
