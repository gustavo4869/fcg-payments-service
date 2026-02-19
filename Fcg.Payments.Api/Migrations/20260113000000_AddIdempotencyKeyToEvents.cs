using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fcg.Payments.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKeyToEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Events",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_IdempotencyKey",
                table: "Events",
                column: "IdempotencyKey",
                unique: true,
                filter: "IdempotencyKey IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_IdempotencyKey",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Events");
        }
    }
}
