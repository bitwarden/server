using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations
{
    /// <inheritdoc />
    public partial class CreateTwoFactorRememberToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TwoFactorRememberToken",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeviceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Stamp = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorRememberToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TwoFactorRememberToken_Device_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Device",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TwoFactorRememberToken_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorRememberToken_DeviceId",
                table: "TwoFactorRememberToken",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorRememberToken_ExpirationDate",
                table: "TwoFactorRememberToken",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorRememberToken_UserId_DeviceId",
                table: "TwoFactorRememberToken",
                columns: new[] { "UserId", "DeviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TwoFactorRememberToken");
        }
    }
}
