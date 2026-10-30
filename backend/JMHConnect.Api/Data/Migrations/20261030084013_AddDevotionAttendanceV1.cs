using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JMHConnect.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDevotionAttendanceV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DevotionSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ScheduledStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DurationMinutes = table.Column<short>(type: "smallint", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevotionSessions", x => x.Id);
                    table.CheckConstraint("CK_DevotionSessions_Duration", "[DurationMinutes] BETWEEN 15 AND 240");
                    table.CheckConstraint("CK_DevotionSessions_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_DevotionSessions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DevotionAttendance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DevotionSessionId = table.Column<int>(type: "int", nullable: false),
                    MemberProfileId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevotionAttendance", x => x.Id);
                    table.CheckConstraint("CK_DevotionAttendance_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_DevotionAttendance_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DevotionAttendance_DevotionSessions_DevotionSessionId",
                        column: x => x.DevotionSessionId,
                        principalTable: "DevotionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DevotionAttendance_MemberProfiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "MemberProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DevotionSessionAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DevotionSessionId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevotionSessionAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DevotionSessionAuditEntries_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DevotionSessionAuditEntries_DevotionSessions_DevotionSessionId",
                        column: x => x.DevotionSessionId,
                        principalTable: "DevotionSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DevotionAttendance_DevotionSessionId_MemberProfileId",
                table: "DevotionAttendance",
                columns: new[] { "DevotionSessionId", "MemberProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DevotionAttendance_MemberProfileId_DevotionSessionId",
                table: "DevotionAttendance",
                columns: new[] { "MemberProfileId", "DevotionSessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DevotionAttendance_RecordedByUserId",
                table: "DevotionAttendance",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DevotionSessionAuditEntries_ActorUserId",
                table: "DevotionSessionAuditEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DevotionSessionAuditEntries_CorrelationId",
                table: "DevotionSessionAuditEntries",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_DevotionSessionAuditEntries_DevotionSessionId_OccurredUtc",
                table: "DevotionSessionAuditEntries",
                columns: new[] { "DevotionSessionId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DevotionSessions_CreatedByUserId",
                table: "DevotionSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DevotionSessions_Status_ScheduledStartUtc",
                table: "DevotionSessions",
                columns: new[] { "Status", "ScheduledStartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DevotionAttendance");

            migrationBuilder.DropTable(
                name: "DevotionSessionAuditEntries");

            migrationBuilder.DropTable(
                name: "DevotionSessions");
        }
    }
}
