using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Kanban.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeTrackingAndSubCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHours",
                table: "Cards",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentCardId",
                table: "Cards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TimeLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CardId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationHours = table.Column<decimal>(type: "numeric", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeLogEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimeLogEntries_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_ParentCardId",
                table: "Cards",
                column: "ParentCardId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogEntries_CardId",
                table: "TimeLogEntries",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeLogEntries_UserId",
                table: "TimeLogEntries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_Cards_ParentCardId",
                table: "Cards",
                column: "ParentCardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cards_Cards_ParentCardId",
                table: "Cards");

            migrationBuilder.DropTable(
                name: "TimeLogEntries");

            migrationBuilder.DropIndex(
                name: "IX_Cards_ParentCardId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ParentCardId",
                table: "Cards");
        }
    }
}
