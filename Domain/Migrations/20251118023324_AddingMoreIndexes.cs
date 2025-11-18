using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddingMoreIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_arp_cover
            ON ""ActivityReportPlayers"" (""ActivityReportId"")
            INCLUDE (""PlayerId"", ""Score"", ""Duration"", ""Completed"");
        ");

            migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS idx_arp_completed_duration_partial
            ON ""ActivityReportPlayers"" (""ActivityReportId"", ""PlayerId"", ""Duration"")
            WHERE ""Completed"" = true;
        ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS idx_arp_cover;");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS idx_arp_completed_duration_partial;");
        }
    }
}
