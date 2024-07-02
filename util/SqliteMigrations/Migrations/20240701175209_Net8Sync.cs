using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class Net8Sync : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProviderInvoiceItem_Id_InvoiceId",
            table: "ProviderInvoiceItem");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_ProviderInvoiceItem_Id_InvoiceId",
            table: "ProviderInvoiceItem",
            columns: new[] { "Id", "InvoiceId" },
            unique: true);
    }
}
