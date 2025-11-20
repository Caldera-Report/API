using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddingCompletedPlayerDurationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ActivityReportPlayers_ActivityId_Completed_Duration",
                table: "ActivityReportPlayers",
                columns: new[] { "ActivityId", "Completed", "Duration" },
                filter: "\"Completed\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityReportPlayers_ActivityId_Completed_Duration",
                table: "ActivityReportPlayers");
        }
    }
}
