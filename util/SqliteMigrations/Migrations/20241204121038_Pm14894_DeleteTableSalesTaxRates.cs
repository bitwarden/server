using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class Pm14894_DeleteTableSalesTaxRates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TaxRate");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TaxRate",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                Active = table.Column<bool>(type: "INTEGER", nullable: false),
                Country = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                PostalCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                Rate = table.Column<decimal>(type: "TEXT", nullable: false),
                State = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaxRate", x => x.Id);
            });
    }
}
