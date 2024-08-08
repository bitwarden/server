using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

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
                Id = table.Column<string>(type: "character varying(449)", maxLength: 449, nullable: false),
                Value = table.Column<byte[]>(type: "bytea", nullable: true),
                ExpiresAtTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SlidingExpirationInSeconds = table.Column<long>(type: "bigint", nullable: true),
                AbsoluteExpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cache", x => x.Id);
            });

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
