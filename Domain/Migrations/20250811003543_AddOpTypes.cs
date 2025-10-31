using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddOpTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageURL",
                table: "ActivityTypes");

            migrationBuilder.AddColumn<int>(
                name: "OpTypeId",
                table: "ActivityTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OpTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTypes_OpTypeId",
                table: "ActivityTypes",
                column: "OpTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityTypes_OpTypes_OpTypeId",
                table: "ActivityTypes",
                column: "OpTypeId",
                principalTable: "OpTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityTypes_OpTypes_OpTypeId",
                table: "ActivityTypes");

            migrationBuilder.DropTable(
                name: "OpTypes");

            migrationBuilder.DropIndex(
                name: "IX_ActivityTypes_OpTypeId",
                table: "ActivityTypes");

            migrationBuilder.DropColumn(
                name: "OpTypeId",
                table: "ActivityTypes");

            migrationBuilder.AddColumn<string>(
                name: "ImageURL",
                table: "ActivityTypes",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
