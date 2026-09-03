using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kanban.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPasskeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetUserPasskeys",
                columns: table => new
                {
                    CredentialId = table.Column<byte[]>(type: "bytea", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Data_AttestationObject = table.Column<byte[]>(type: "bytea", nullable: false),
                    Data_ClientDataJson = table.Column<byte[]>(type: "bytea", nullable: false),
                    Data_CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Data_IsBackedUp = table.Column<bool>(type: "boolean", nullable: false),
                    Data_IsBackupEligible = table.Column<bool>(type: "boolean", nullable: false),
                    Data_IsUserVerified = table.Column<bool>(type: "boolean", nullable: false),
                    Data_Name = table.Column<string>(type: "text", nullable: true),
                    Data_PublicKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    Data_SignCount = table.Column<long>(type: "bigint", nullable: false),
                    Data_Transports = table.Column<string[]>(type: "text[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserPasskeys", x => x.CredentialId);
                    table.ForeignKey(
                        name: "FK_AspNetUserPasskeys_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserPasskeys_UserId",
                table: "AspNetUserPasskeys",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetUserPasskeys");
        }
    }
}
