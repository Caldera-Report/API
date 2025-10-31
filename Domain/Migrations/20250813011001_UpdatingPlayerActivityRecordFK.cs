using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class UpdatingPlayerActivityRecordFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FastestCompletionActivityReportInstanceId",
                table: "PlayerActivityRecords",
                newName: "FastestCompletionActivityReportId");

            migrationBuilder.RenameIndex(
                name: "IX_PlayerActivityRecords_FastestCompletionActivityReportInstan~",
                table: "PlayerActivityRecords",
                newName: "IX_PlayerActivityRecords_FastestCompletionActivityReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FastestCompletionActivityReportId",
                table: "PlayerActivityRecords",
                newName: "FastestCompletionActivityReportInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_PlayerActivityRecords_FastestCompletionActivityReportId",
                table: "PlayerActivityRecords",
                newName: "IX_PlayerActivityRecords_FastestCompletionActivityReportInstan~");
        }
    }
}
