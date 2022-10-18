using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

public partial class ApiKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApiKey",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ServiceAccountId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ClientSecret = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Scope = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                EncryptedPayload = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Key = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExpireAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKey", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApiKey_ServiceAccount_ServiceAccountId",
                    column: x => x.ServiceAccountId,
                    principalTable: "ServiceAccount",
                    principalColumn: "Id");
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_ApiKey_ServiceAccountId",
            table: "ApiKey",
            column: "ServiceAccountId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApiKey");
    }
}
