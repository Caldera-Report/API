using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityIdToARP : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ActivityId",
                table: "ActivityReportPlayers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityReportPlayers_ActivityId",
                table: "ActivityReportPlayers",
                column: "ActivityId");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_arp_activityid_cover
                ON ""ActivityReportPlayers""(""ActivityId"")
                INCLUDE (""PlayerId"", ""Score"", ""Completed"", ""Duration"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS idx_players_id_cover
                ON ""Players""(""Id"")
                INCLUDE (""FullDisplayName"");
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE public.compute_leaderboard_score(
                    IN activity_id bigint
                )
                LANGUAGE sql
                AS $BODY$
                    WITH ranked AS (
                        SELECT
                            ""PlayerId"",
                            MAX(""Score"") AS best_score,
                            ROW_NUMBER() OVER (
                                ORDER BY MAX(""Score"") DESC, ""PlayerId""
                            ) AS rank
                        FROM ""ActivityReportPlayers""
                        WHERE ""ActivityId"" = activity_id
                          AND ""Completed"" = TRUE
                        GROUP BY ""PlayerId""
                    )
                    INSERT INTO ""PlayerLeaderboards""
                        (""PlayerId"",""FullDisplayName"",""ActivityId"",""LeaderboardType"",""Rank"",""Data"")
                    SELECT
                        r.""PlayerId"",
                        p.""FullDisplayName"",
                        activity_id,
                        2,
                        r.rank,
                        jsonb_build_object('score', r.best_score)
                    FROM ranked r
                    JOIN ""Players"" p ON p.""Id"" = r.""PlayerId""
                    ON CONFLICT (""PlayerId"",""ActivityId"",""LeaderboardType"")
                    DO UPDATE SET
                        ""Rank"" = EXCLUDED.""Rank"",
                        ""Data"" = EXCLUDED.""Data"";
                $BODY$;
            ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE public.compute_leaderboard_duration(
                    IN activity_id bigint
                )
                LANGUAGE sql
                AS $BODY$
                    WITH ranked AS (
                        SELECT
                            ""PlayerId"",
                            MIN(""Duration"") AS best_duration,
                            ROW_NUMBER() OVER (
                                ORDER BY MIN(""Duration"") ASC NULLS LAST, ""PlayerId""
                            ) AS rank
                        FROM ""ActivityReportPlayers""
                        WHERE ""ActivityId"" = activity_id
                          AND ""Completed"" = TRUE
                        GROUP BY ""PlayerId""
                        HAVING MIN(""Duration"") IS NOT NULL
                    )
                    INSERT INTO ""PlayerLeaderboards""
                        (""PlayerId"",""FullDisplayName"",""ActivityId"",""LeaderboardType"",""Rank"",""Data"")
                    SELECT
                        r.""PlayerId"",
                        p.""FullDisplayName"",
                        activity_id,
                        0,
                        r.rank,
                        jsonb_build_object('duration', r.best_duration)
                    FROM ranked r
                    JOIN ""Players"" p ON p.""Id"" = r.""PlayerId""
                    ON CONFLICT (""PlayerId"",""ActivityId"",""LeaderboardType"")
                    DO UPDATE SET
                        ""Rank"" = EXCLUDED.""Rank"",
                        ""Data"" = EXCLUDED.""Data"";
                $BODY$;
                ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE public.compute_leaderboard_completions(
                    IN activity_id bigint
                )
                LANGUAGE sql
                AS $BODY$
                    WITH ranked AS (
                        SELECT
                            ""PlayerId"",
                            COUNT(*) AS completion_count,
                            ROW_NUMBER() OVER (
                                ORDER BY COUNT(*) DESC, ""PlayerId""
                            ) AS rank
                        FROM ""ActivityReportPlayers""
                        WHERE ""ActivityId"" = activity_id
                            AND ""Completed"" = TRUE
                        GROUP BY ""PlayerId""
                    )
                    INSERT INTO ""PlayerLeaderboards""
                        (""PlayerId"",""FullDisplayName"",""ActivityId"",""LeaderboardType"",""Rank"",""Data"")
                    SELECT
                        r.""PlayerId"",
                        p.""FullDisplayName"",
                        activity_id,
                        1,
                        r.rank,
                        jsonb_build_object('completions', r.completion_count)
                    FROM ranked r
                    JOIN ""Players"" p ON p.""Id"" = r.""PlayerId""
                    ON CONFLICT (""PlayerId"",""ActivityId"",""LeaderboardType"")
                    DO UPDATE SET
                        ""Rank"" = EXCLUDED.""Rank"",
                        ""Data"" = EXCLUDED.""Data"";
                $BODY$;
                ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ActivityReportPlayers_ActivityId",
                table: "ActivityReportPlayers");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "ActivityReportPlayers");

            migrationBuilder.DropIndex(
                name: "idx_arp_activityid_cover",
                table: "ActivityReportPlayers");

            migrationBuilder.DropIndex(
                name: "idx_players_id_cover",
                table: "Players");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE compute_leaderboard_duration(activity_id bigint)
                LANGUAGE sql
                AS $$
                    WITH player_activity AS (
                        SELECT
                            arp.""PlayerId"",
                            ar.""ActivityId"",
                            MIN(arp.""Duration"") FILTER (WHERE arp.""Completed"") AS best_duration
                        FROM ""ActivityReportPlayers"" arp
                        JOIN ""ActivityReports"" ar ON ar.""Id"" = arp.""ActivityReportId""
                        WHERE ar.""ActivityId"" = activity_id
                        GROUP BY ar.""ActivityId"", arp.""PlayerId""
                    ),
                    ranked AS (
                        SELECT
                            pa.""ActivityId"",
                            pa.""PlayerId"",
                            pa.best_duration,
                            ROW_NUMBER() OVER (
                                PARTITION BY pa.""ActivityId""
                                ORDER BY pa.best_duration ASC NULLS LAST, pa.""PlayerId""
                            ) AS rank
                        FROM player_activity pa
                        WHERE pa.best_duration IS NOT NULL
                    )
                    INSERT INTO ""PlayerLeaderboards""
                        (""PlayerId"",""FullDisplayName"",""ActivityId"",""LeaderboardType"",""Rank"",""Data"")
                    SELECT
                        r.""PlayerId"",
                        p.""FullDisplayName"",
                        r.""ActivityId"",
                        0,
                        r.rank,
                        jsonb_build_object('duration', r.best_duration)
                    FROM ranked r
                    JOIN ""Players"" p ON p.""Id"" = r.""PlayerId""
                    ON CONFLICT (""PlayerId"",""ActivityId"",""LeaderboardType"")
                    DO UPDATE SET
                        ""Rank"" = EXCLUDED.""Rank"",
                        ""Data"" = EXCLUDED.""Data"";
                $$;
                ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE compute_leaderboard_completions(activity_id bigint)
                LANGUAGE sql
                AS $$
                    WITH player_activity AS (
                        SELECT
                            arp.""PlayerId"",
                            ar.""ActivityId"",
                            COUNT(*) FILTER (WHERE arp.""Completed"") AS completion_count
                        FROM ""ActivityReportPlayers"" arp
                        JOIN ""ActivityReports"" ar ON ar.""Id"" = arp.""ActivityReportId""
                        WHERE ar.""ActivityId"" = activity_id
                        GROUP BY ar.""ActivityId"", arp.""PlayerId""
                    ),
                    ranked AS (
                        SELECT
                            pa.""ActivityId"",
                            pa.""PlayerId"",
                            pa.completion_count,
                            ROW_NUMBER() OVER (
                                PARTITION BY pa.""ActivityId""
                                ORDER BY pa.completion_count DESC, pa.""PlayerId""
                            ) AS rank
                        FROM player_activity pa
                    )
                    INSERT INTO ""PlayerLeaderboards""
                        (""PlayerId"",""FullDisplayName"",""ActivityId"",""LeaderboardType"",""Rank"",""Data"")
                    SELECT
                        r.""PlayerId"",
                        p.""FullDisplayName"",
                        r.""ActivityId"",
                        1,
                        r.rank,
                        jsonb_build_object('completions', r.completion_count)
                    FROM ranked r
                    JOIN ""Players"" p ON p.""Id"" = r.""PlayerId""
                    ON CONFLICT (""PlayerId"",""ActivityId"",""LeaderboardType"")
                    DO UPDATE SET
                        ""Rank"" = EXCLUDED.""Rank"",
                        ""Data"" = EXCLUDED.""Data"";
                $$;
                ");

            migrationBuilder.Sql(@"
                CREATE OR REPLACE PROCEDURE compute_leaderboard_score(activity_id bigint)
                LANGUAGE sql
                AS $$
                    WITH player_activity AS (
                        SELECT
                            arp.""PlayerId"",
                            ar.""ActivityId"",
                            MAX(arp.""Score"") AS best_score
                        FROM ""ActivityReportPlayers"" arp
                        JOIN ""ActivityReports"" ar ON ar.""Id"" = arp.""ActivityReportId""
                        WHERE ar.""ActivityId"" = activity_id
                        GROUP BY ar.""ActivityId"", arp.""PlayerId""
                    ),
                    ranked AS (
                        SELECT
                            pa.""ActivityId"",
                            pa.""PlayerId"",
                            pa.best_score,
                            ROW_NUMBER() OVER (
                                PARTITION BY pa.""ActivityId""
                                ORDER BY pa.best_score DESC, pa.""PlayerId""
                            ) AS rank
                        FROM player_activity pa
                    )
                    INSERT INTO ""PlayerLeaderboards""
                        (""PlayerId"",""FullDisplayName"",""ActivityId"",""LeaderboardType"",""Rank"",""Data"")
                    SELECT
                        r.""PlayerId"",
                        p.""FullDisplayName"",
                        r.""ActivityId"",
                        2,
                        r.rank,
                        jsonb_build_object('score', r.best_score)
                    FROM ranked r
                    JOIN ""Players"" p ON p.""Id"" = r.""PlayerId""
                    ON CONFLICT (""PlayerId"",""ActivityId"",""LeaderboardType"")
                    DO UPDATE SET
                        ""Rank"" = EXCLUDED.""Rank"",
                        ""Data"" = EXCLUDED.""Data"";
                $$;
                ");
        }
    }
}
