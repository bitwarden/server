using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class DistributedCache : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Cache",
            columns: table => new
            {
                Id = table.Column<string>(type: "varchar(449)", maxLength: 449, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Value = table.Column<byte[]>(type: "longblob", nullable: true),
                ExpiresAtTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                SlidingExpirationInSeconds = table.Column<long>(type: "bigint", nullable: true),
                AbsoluteExpiration = table.Column<DateTime>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cache", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_Cache_ExpiresAtTime",
            table: "Cache",
            column: "ExpiresAtTime");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Cache");
    }
}
