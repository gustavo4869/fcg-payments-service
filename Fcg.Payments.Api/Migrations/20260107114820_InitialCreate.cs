using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fcg.Payments.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Payload = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "Pagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamentos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_AggregateId",
                table: "Events",
                column: "AggregateId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_OccurredAt",
                table: "Events",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Pagamentos");
        }
    }
}
