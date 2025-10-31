using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddingIndexesForFasterLeaderboardGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_ActivityId",
                table: "ActivityReports");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReport_Activity_Player",
                table: "ActivityReports",
                columns: new[] { "ActivityId", "PlayerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReport_Activity_Player_Completed_InclDuration",
                table: "ActivityReports",
                columns: new[] { "ActivityId", "PlayerId", "Completed" })
                .Annotation("Npgsql:IndexInclude", new[] { "Duration" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReport_Completed_Fastest",
                table: "ActivityReports",
                columns: new[] { "ActivityId", "PlayerId", "Duration" },
                filter: "\"Completed\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityReport_Activity_Player",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReport_Activity_Player_Completed_InclDuration",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReport_Completed_Fastest",
                table: "ActivityReports");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_ActivityId",
                table: "ActivityReports",
                column: "ActivityId");
        }
    }
}
