using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddingIndexesAndActivityEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "Activities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLeaderboards_ActivityId_LeaderboardType_Rank",
                table: "PlayerLeaderboards",
                columns: new[] { "ActivityId", "LeaderboardType", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_Id_ActivityId",
                table: "ActivityReports",
                columns: new[] { "Id", "ActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReportPlayers_ActivityReportId_PlayerId_Duration",
                table: "ActivityReportPlayers",
                columns: new[] { "ActivityReportId", "PlayerId", "Duration" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerLeaderboards_ActivityId_LeaderboardType_Rank",
                table: "PlayerLeaderboards");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_Id_ActivityId",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReportPlayers_ActivityReportId_PlayerId_Duration",
                table: "ActivityReportPlayers");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "Activities");
        }
    }
}
