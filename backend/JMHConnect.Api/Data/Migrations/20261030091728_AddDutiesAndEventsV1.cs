using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JMHConnect.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDutiesAndEventsV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityEvents", x => x.Id);
                    table.CheckConstraint("CK_CommunityEvents_Schedule", "[EndsUtc] > [StartsUtc]");
                    table.CheckConstraint("CK_CommunityEvents_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_CommunityEvents_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DutyAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssignedMemberProfileId = table.Column<int>(type: "int", nullable: false),
                    ScheduledForUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyAssignments", x => x.Id);
                    table.CheckConstraint("CK_DutyAssignments_Completion", "([Status] = 2 AND [CompletedUtc] IS NOT NULL AND [CompletedByUserId] IS NOT NULL) OR ([Status] IN (1, 3) AND [CompletedUtc] IS NULL AND [CompletedByUserId] IS NULL)");
                    table.CheckConstraint("CK_DutyAssignments_Schedule", "[DueUtc] IS NULL OR [DueUtc] >= [ScheduledForUtc]");
                    table.CheckConstraint("CK_DutyAssignments_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_DutyAssignments_AspNetUsers_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyAssignments_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyAssignments_MemberProfiles_AssignedMemberProfileId",
                        column: x => x.AssignedMemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunityEventAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommunityEventId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityEventAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityEventAuditEntries_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunityEventAuditEntries_CommunityEvents_CommunityEventId",
                        column: x => x.CommunityEventId,
                        principalTable: "CommunityEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DutyAssignmentAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DutyAssignmentId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DutyAssignmentAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DutyAssignmentAuditEntries_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DutyAssignmentAuditEntries_DutyAssignments_DutyAssignmentId",
                        column: x => x.DutyAssignmentId,
                        principalTable: "DutyAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityEventAuditEntries_ActorUserId",
                table: "CommunityEventAuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityEventAuditEntries_CommunityEventId_OccurredUtc",
                table: "CommunityEventAuditEntries",
                columns: new[] { "CommunityEventId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityEventAuditEntries_CorrelationId",
                table: "CommunityEventAuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityEvents_CreatedByUserId",
                table: "CommunityEvents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityEvents_Status_StartsUtc",
                table: "CommunityEvents",
                columns: new[] { "Status", "StartsUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignmentAuditEntries_ActorUserId",
                table: "DutyAssignmentAuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignmentAuditEntries_CorrelationId",
                table: "DutyAssignmentAuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignmentAuditEntries_DutyAssignmentId_OccurredUtc",
                table: "DutyAssignmentAuditEntries",
                columns: new[] { "DutyAssignmentId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_AssignedMemberProfileId_Status_ScheduledForUtc",
                table: "DutyAssignments",
                columns: new[] { "AssignedMemberProfileId", "Status", "ScheduledForUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_CompletedByUserId",
                table: "DutyAssignments",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_CreatedByUserId",
                table: "DutyAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DutyAssignments_Status_ScheduledForUtc",
                table: "DutyAssignments",
                columns: new[] { "Status", "ScheduledForUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityEventAuditEntries");

            migrationBuilder.DropTable(
                name: "DutyAssignmentAuditEntries");

            migrationBuilder.DropTable(
                name: "CommunityEvents");

            migrationBuilder.DropTable(
                name: "DutyAssignments");
        }
    }
}
