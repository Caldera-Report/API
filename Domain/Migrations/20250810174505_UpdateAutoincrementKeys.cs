using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAutoincrementKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                table: "PlayerActivityRecords");

            migrationBuilder.RenameColumn(
                name: "FastestCompletionActivityReportId",
                table: "PlayerActivityRecords",
                newName: "FastestCompletionActivityReportInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_PlayerActivityRecords_FastestCompletionActivityReportId",
                table: "PlayerActivityRecords",
                newName: "IX_PlayerActivityRecords_FastestCompletionActivityReportInstan~");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Players",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "ActivityTypes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<long>(
                name: "InstanceId",
                table: "ActivityReports",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Activities",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                table: "PlayerActivityRecords");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ActivityReports_InstanceId",
                table: "ActivityReports");

            migrationBuilder.DropColumn(
                name: "InstanceId",
                table: "ActivityReports");

            migrationBuilder.RenameColumn(
                name: "FastestCompletionActivityReportInstanceId",
                table: "PlayerActivityRecords",
                newName: "FastestCompletionActivityReportId");

            migrationBuilder.RenameIndex(
                name: "IX_PlayerActivityRecords_FastestCompletionActivityReportInstan~",
                table: "PlayerActivityRecords",
                newName: "IX_PlayerActivityRecords_FastestCompletionActivityReportId");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Players",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "ActivityTypes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Activities",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                table: "PlayerActivityRecords",
                column: "FastestCompletionActivityReportId",
                principalTable: "ActivityReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
