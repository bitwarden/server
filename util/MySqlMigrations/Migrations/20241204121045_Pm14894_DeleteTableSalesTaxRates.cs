using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

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
                Id = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Country = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                PostalCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Rate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                State = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaxRate", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");
    }
}
