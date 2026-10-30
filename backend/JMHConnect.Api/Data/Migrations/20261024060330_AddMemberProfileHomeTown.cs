using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JMHConnect.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberProfileHomeTown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HomeTown",
                table: "MemberProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeTown",
                table: "MemberProfiles");
        }
    }
}
