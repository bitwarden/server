using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

public partial class PasswordlessAuthRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuthRequest",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Type = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                RequestDeviceIdentifier = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RequestDeviceType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                RequestIpAddress = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RequestFingerprint = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ResponseDeviceId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                AccessCode = table.Column<string>(type: "varchar(25)", maxLength: 25, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PublicKey = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                MasterPasswordHash = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ResponseDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                AuthenticationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuthRequest", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuthRequest_Device_ResponseDeviceId",
                    column: x => x.ResponseDeviceId,
                    principalTable: "Device",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_AuthRequest_User_UserId",
                    column: x => x.UserId,
                    principalTable: "User",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_AuthRequest_ResponseDeviceId",
            table: "AuthRequest",
            column: "ResponseDeviceId");

        migrationBuilder.CreateIndex(
            name: "IX_AuthRequest_UserId",
            table: "AuthRequest",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuthRequest");
    }
}
