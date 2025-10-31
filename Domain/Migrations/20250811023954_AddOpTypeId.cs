using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddOpTypeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityTypes_OpTypes_OpTypeId",
                table: "ActivityTypes");

            migrationBuilder.DropIndex(
                name: "IX_ActivityTypes_OpTypeId",
                table: "ActivityTypes");

            migrationBuilder.AlterColumn<long>(
                name: "OpTypeId",
                table: "ActivityTypes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "OpTypeId1",
                table: "ActivityTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTypes_OpTypeId1",
                table: "ActivityTypes",
                column: "OpTypeId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityTypes_OpTypes_OpTypeId1",
                table: "ActivityTypes",
                column: "OpTypeId1",
                principalTable: "OpTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityTypes_OpTypes_OpTypeId1",
                table: "ActivityTypes");

            migrationBuilder.DropIndex(
                name: "IX_ActivityTypes_OpTypeId1",
                table: "ActivityTypes");

            migrationBuilder.DropColumn(
                name: "OpTypeId1",
                table: "ActivityTypes");

            migrationBuilder.AlterColumn<int>(
                name: "OpTypeId",
                table: "ActivityTypes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

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
    }
}
