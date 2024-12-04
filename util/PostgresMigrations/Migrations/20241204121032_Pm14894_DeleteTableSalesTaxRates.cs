using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

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
                Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                Country = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Rate = table.Column<decimal>(type: "numeric", nullable: false),
                State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaxRate", x => x.Id);
            });
    }
}
