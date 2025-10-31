using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class UpdatingActivityReportRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                table: "PlayerActivityRecords");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ActivityReports_InstanceId",
                table: "ActivityReports");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_InstanceId",
                table: "ActivityReports",
                column: "InstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                table: "PlayerActivityRecords",
                column: "FastestCompletionActivityReportInstanceId",
                principalTable: "ActivityReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                table: "PlayerActivityRecords");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_InstanceId",
                table: "ActivityReports");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ActivityReports_InstanceId",
                table: "ActivityReports",
                column: "InstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                table: "PlayerActivityRecords",
                column: "FastestCompletionActivityReportInstanceId",
                principalTable: "ActivityReports",
                principalColumn: "InstanceId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
