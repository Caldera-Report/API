using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddFullDisplayNameColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Enable pg_trgm extension
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Players""
                ADD COLUMN ""FullDisplayName"" text
                GENERATED ALWAYS AS (""DisplayName"" || '#' || lpad(""DisplayNameCode""::text, 4, '0')) STORED;
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_players_fulldisplayname_trgm
                ON ""Players""
                USING GIN (""FullDisplayName"" gin_trgm_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS `idx_players_fulldisplayname_trgm`;");
            migrationBuilder.Sql(@"ALTER TABLE ""Players"" DROP COLUMN IF EXISTS ""FullDisplayName"";");
            migrationBuilder.Sql(@"DROP EXTENSION IF EXISTS pg_trgm;");
        }
    }
}
