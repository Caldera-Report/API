using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class MakingRelationshipBetweenPlayerAndLastPlayedActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPlayedActivityInstanceId",
                table: "Players");

            migrationBuilder.AddColumn<long>(
                name: "LastPlayedActivityId",
                table: "Players",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_LastPlayedActivityId",
                table: "Players",
                column: "LastPlayedActivityId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_ActivityReports_LastPlayedActivityId",
                table: "Players",
                column: "LastPlayedActivityId",
                principalTable: "ActivityReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_ActivityReports_LastPlayedActivityId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_LastPlayedActivityId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastPlayedActivityId",
                table: "Players");

            migrationBuilder.AddColumn<long>(
                name: "LastPlayedActivityInstanceId",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
