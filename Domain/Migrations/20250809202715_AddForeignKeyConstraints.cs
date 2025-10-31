using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeyConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ID",
                table: "ActivityReports",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "ActivityType",
                table: "Activities",
                newName: "ActivityTypeId");

            migrationBuilder.AddColumn<long>(
                name: "ActivityId",
                table: "PlayerActivityRecords",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "FastestCompletionActivityReportId",
                table: "PlayerActivityRecords",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PlayerId",
                table: "ActivityReports",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerActivityRecords_ActivityId",
                table: "PlayerActivityRecords",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerActivityRecords_FastestCompletionActivityReportId",
                table: "PlayerActivityRecords",
                column: "FastestCompletionActivityReportId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerActivityRecords_PlayerId",
                table: "PlayerActivityRecords",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_ActivityId",
                table: "ActivityReports",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_PlayerId",
                table: "ActivityReports",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ActivityTypeId",
                table: "Activities",
                column: "ActivityTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_ActivityTypes_ActivityTypeId",
                table: "Activities",
                column: "ActivityTypeId",
                principalTable: "ActivityTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityReports_Activities_ActivityId",
                table: "ActivityReports",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityReports_Players_PlayerId",
                table: "ActivityReports",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerActivityRecords_Activities_ActivityId",
                table: "PlayerActivityRecords",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                table: "PlayerActivityRecords",
                column: "FastestCompletionActivityReportId",
                principalTable: "ActivityReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerActivityRecords_Players_PlayerId",
                table: "PlayerActivityRecords",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_ActivityTypes_ActivityTypeId",
                table: "Activities");

            migrationBuilder.DropForeignKey(
                name: "FK_ActivityReports_Activities_ActivityId",
                table: "ActivityReports");

            migrationBuilder.DropForeignKey(
                name: "FK_ActivityReports_Players_PlayerId",
                table: "ActivityReports");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerActivityRecords_Activities_ActivityId",
                table: "PlayerActivityRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                table: "PlayerActivityRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerActivityRecords_Players_PlayerId",
                table: "PlayerActivityRecords");

            migrationBuilder.DropIndex(
                name: "IX_PlayerActivityRecords_ActivityId",
                table: "PlayerActivityRecords");

            migrationBuilder.DropIndex(
                name: "IX_PlayerActivityRecords_FastestCompletionActivityReportId",
                table: "PlayerActivityRecords");

            migrationBuilder.DropIndex(
                name: "IX_PlayerActivityRecords_PlayerId",
                table: "PlayerActivityRecords");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_ActivityId",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_PlayerId",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_Activities_ActivityTypeId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "PlayerActivityRecords");

            migrationBuilder.DropColumn(
                name: "FastestCompletionActivityReportId",
                table: "PlayerActivityRecords");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "ActivityReports");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ActivityReports",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "ActivityTypeId",
                table: "Activities",
                newName: "ActivityType");
        }
    }
}
