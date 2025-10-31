using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class MakingPlayerActivityReportsManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityReports_Players_PlayerId",
                table: "ActivityReports");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_ActivityReports_LastPlayedActivityId",
                table: "Players");

            migrationBuilder.DropTable(
                name: "ActivityHashMappings");

            migrationBuilder.DropIndex(
                name: "IX_Player_Id_UpdatePriority",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_LastPlayedActivityId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReport_Activity_Player",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReport_Activity_Player_Completed_InclDuration",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReport_Completed_Fastest",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_InstanceId",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_PlayerId",
                table: "ActivityReports");

            migrationBuilder.DropColumn(
                name: "LastPlayed",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastPlayedActivityId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "UpdatePriority",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Completed",
                table: "ActivityReports");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "ActivityReports");

            migrationBuilder.DropColumn(
                name: "InstanceId",
                table: "ActivityReports");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "ActivityReports");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "ActivityReports",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateTable(
                name: "ActivityReportPlayers",
                columns: table => new
                {
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityReportId = table.Column<long>(type: "bigint", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityReportPlayers", x => new { x.ActivityReportId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_ActivityReportPlayers_ActivityReports_ActivityReportId",
                        column: x => x.ActivityReportId,
                        principalTable: "ActivityReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityReportPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_ActivityId",
                table: "ActivityReports",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_Id",
                table: "ActivityReports",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReportPlayers_PlayerId",
                table: "ActivityReportPlayers",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityReportPlayers");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_ActivityId",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_Id",
                table: "ActivityReports");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPlayed",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastPlayedActivityId",
                table: "Players",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatePriority",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "ActivityReports",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<bool>(
                name: "Completed",
                table: "ActivityReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "ActivityReports",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<long>(
                name: "InstanceId",
                table: "ActivityReports",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PlayerId",
                table: "ActivityReports",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ActivityHashMappings",
                columns: table => new
                {
                    SourceHash = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanonicalActivityId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityHashMappings", x => x.SourceHash);
                    table.ForeignKey(
                        name: "FK_ActivityHashMappings_Activities_CanonicalActivityId",
                        column: x => x.CanonicalActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Player_Id_UpdatePriority",
                table: "Players",
                columns: new[] { "Id", "UpdatePriority" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_LastPlayedActivityId",
                table: "Players",
                column: "LastPlayedActivityId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_InstanceId",
                table: "ActivityReports",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_PlayerId",
                table: "ActivityReports",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityHashMappings_CanonicalActivityId",
                table: "ActivityHashMappings",
                column: "CanonicalActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityReports_Players_PlayerId",
                table: "ActivityReports",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Players_ActivityReports_LastPlayedActivityId",
                table: "Players",
                column: "LastPlayedActivityId",
                principalTable: "ActivityReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
