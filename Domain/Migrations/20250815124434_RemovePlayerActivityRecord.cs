using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlayerActivityRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerActivityRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerActivityRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActivityId = table.Column<long>(type: "bigint", nullable: false),
                    FastestCompletionActivityReportId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    FastestCompletion = table.Column<TimeSpan>(type: "interval", nullable: false),
                    TotalGamesPlayed = table.Column<int>(type: "integer", nullable: false),
                    TotalTimePlayed = table.Column<TimeSpan>(type: "interval", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerActivityRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerActivityRecords_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerActivityRecords_ActivityReports_FastestCompletionActi~",
                        column: x => x.FastestCompletionActivityReportId,
                        principalTable: "ActivityReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerActivityRecords_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
        }
    }
}
