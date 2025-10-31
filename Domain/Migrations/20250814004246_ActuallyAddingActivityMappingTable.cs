using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class ActuallyAddingActivityMappingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityHashMapping_Activities_CanonicalActivityId",
                table: "ActivityHashMapping");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ActivityHashMapping",
                table: "ActivityHashMapping");

            migrationBuilder.RenameTable(
                name: "ActivityHashMapping",
                newName: "ActivityHashMappings");

            migrationBuilder.RenameIndex(
                name: "IX_ActivityHashMapping_CanonicalActivityId",
                table: "ActivityHashMappings",
                newName: "IX_ActivityHashMappings_CanonicalActivityId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActivityHashMappings",
                table: "ActivityHashMappings",
                column: "SourceHash");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityHashMappings_Activities_CanonicalActivityId",
                table: "ActivityHashMappings",
                column: "CanonicalActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityHashMappings_Activities_CanonicalActivityId",
                table: "ActivityHashMappings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ActivityHashMappings",
                table: "ActivityHashMappings");

            migrationBuilder.RenameTable(
                name: "ActivityHashMappings",
                newName: "ActivityHashMapping");

            migrationBuilder.RenameIndex(
                name: "IX_ActivityHashMappings_CanonicalActivityId",
                table: "ActivityHashMapping",
                newName: "IX_ActivityHashMapping_CanonicalActivityId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActivityHashMapping",
                table: "ActivityHashMapping",
                column: "SourceHash");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityHashMapping_Activities_CanonicalActivityId",
                table: "ActivityHashMapping",
                column: "CanonicalActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
