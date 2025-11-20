using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class MoveUpdateQueueAndLeaderboardsToDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUpdateCompleted",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastUpdateStarted",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastUpdateStatus",
                table: "Players");

            migrationBuilder.CreateTable(
                name: "PlayerCrawlQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    EnqueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCrawlQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerLeaderboards",
                columns: table => new
                {
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<long>(type: "bigint", nullable: false),
                    LeaderboardType = table.Column<int>(type: "integer", nullable: false),
                    FullDisplayName = table.Column<string>(type: "text", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerLeaderboards", x => new { x.PlayerId, x.ActivityId, x.LeaderboardType });
                    table.ForeignKey(
                        name: "FK_PlayerLeaderboards_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReports_Id_ActivityId_Date",
                table: "ActivityReports",
                columns: new[] { "Id", "ActivityId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReportPlayers_ActivityReportId_PlayerId",
                table: "ActivityReportPlayers",
                columns: new[] { "ActivityReportId", "PlayerId" },
                filter: "\"Completed\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReportPlayers_ActivityReportId_PlayerId_Score",
                table: "ActivityReportPlayers",
                columns: new[] { "ActivityReportId", "PlayerId", "Score" });

            migrationBuilder.Sql(@"                
                CREATE INDEX IF NOT EXISTS idx_playerleaderboards_fulldisplayname_trgm
                ON ""PlayerLeaderboards""
                USING GIN (""FullDisplayName"" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""idx_playerleaderboards_fulldisplayname_trgm"";");

            migrationBuilder.DropTable(
                name: "PlayerCrawlQueue");

            migrationBuilder.DropTable(
                name: "PlayerLeaderboards");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReports_Id_ActivityId_Date",
                table: "ActivityReports");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReportPlayers_ActivityReportId_PlayerId",
                table: "ActivityReportPlayers");

            migrationBuilder.DropIndex(
                name: "IX_ActivityReportPlayers_ActivityReportId_PlayerId_Score",
                table: "ActivityReportPlayers");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdateCompleted",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdateStarted",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastUpdateStatus",
                table: "Players",
                type: "text",
                nullable: true);
        }
    }
}
