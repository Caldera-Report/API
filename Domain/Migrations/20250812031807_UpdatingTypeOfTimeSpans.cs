using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class UpdatingTypeOfTimeSpans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivityDate",
                table: "PlayerActivityRecords");

            // 1. Add temp column
            migrationBuilder.AddColumn<TimeSpan>(
                name: "TotalTimePlayed_temp",
                table: "PlayerActivityRecords",
                type: "interval",
                nullable: false,
                defaultValue: TimeSpan.Zero);

            // 2. Migrate data (PostgreSQL interval from text: use interval input format, e.g. '1 hour 2 min')
            migrationBuilder.Sql(
                        @"UPDATE ""PlayerActivityRecords"" 
               SET ""TotalTimePlayed_temp"" = 
                 CASE 
                   WHEN ""TotalTimePlayed"" ~ '^\d+$' THEN (""TotalTimePlayed"" || ' seconds')::interval
                   ELSE ""TotalTimePlayed""::interval
                 END"
            );

            // 3. Drop old column
            migrationBuilder.DropColumn(
                name: "TotalTimePlayed",
                table: "PlayerActivityRecords");

            // 4. Rename temp column
            migrationBuilder.RenameColumn(
                name: "TotalTimePlayed_temp",
                table: "PlayerActivityRecords",
                newName: "TotalTimePlayed");

            migrationBuilder.AddColumn<int>(
                name: "TotalGamesPlayed_temp",
                table: "PlayerActivityRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                @"UPDATE ""PlayerActivityRecords"" 
          SET ""TotalGamesPlayed_temp"" = CAST(""TotalGamesPlayed"" AS integer)"
            );

            migrationBuilder.DropColumn(
                name: "TotalGamesPlayed",
                table: "PlayerActivityRecords");

            migrationBuilder.RenameColumn(
                name: "TotalGamesPlayed_temp",
                table: "PlayerActivityRecords",
                newName: "TotalGamesPlayed");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "FastestCompletion_temp",
                table: "PlayerActivityRecords",
                type: "interval",
                nullable: false,
                defaultValue: TimeSpan.Zero);

            // 2. Migrate data (PostgreSQL interval from text: use interval input format, e.g. '1 hour 2 min')
            migrationBuilder.Sql(
                        @"UPDATE ""PlayerActivityRecords"" 
               SET ""FastestCompletion_temp"" = 
                 CASE 
                   WHEN ""FastestCompletion"" ~ '^\d+$' THEN (""FastestCompletion"" || ' seconds')::interval
                   ELSE ""FastestCompletion""::interval
                 END"
            );

            // 3. Drop old column
            migrationBuilder.DropColumn(
                name: "FastestCompletion",
                table: "PlayerActivityRecords");

            // 4. Rename temp column
            migrationBuilder.RenameColumn(
                name: "FastestCompletion_temp",
                table: "PlayerActivityRecords",
                newName: "FastestCompletion");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration_temp",
                table: "ActivityReports",
                type: "interval",
                nullable: false,
                defaultValue: TimeSpan.Zero);

            // 3. Drop old column
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "ActivityReports");

            // 4. Rename temp column
            migrationBuilder.RenameColumn(
                name: "Duration_temp",
                table: "ActivityReports",
                newName: "Duration");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) //I pray I never have to run this
        {
            migrationBuilder.AlterColumn<string>(
                name: "TotalTimePlayed",
                table: "PlayerActivityRecords",
                type: "text",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "interval");

            migrationBuilder.AlterColumn<string>(
                name: "TotalGamesPlayed",
                table: "PlayerActivityRecords",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "FastestCompletion",
                table: "PlayerActivityRecords",
                type: "text",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "interval");

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivityDate",
                table: "PlayerActivityRecords",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<long>(
                name: "Duration",
                table: "ActivityReports",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "interval");
        }
    }
}
