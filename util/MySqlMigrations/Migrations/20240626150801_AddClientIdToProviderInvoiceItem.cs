using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddClientIdToProviderInvoiceItem : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProviderInvoiceItem_Id_InvoiceId",
            table: "ProviderInvoiceItem");

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceId",
            table: "ProviderInvoiceItem",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(255)",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<Guid>(
            name: "ClientId",
            table: "ProviderInvoiceItem",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClientId",
            table: "ProviderInvoiceItem");

        migrationBuilder.AlterColumn<string>(
            name: "InvoiceId",
            table: "ProviderInvoiceItem",
            type: "varchar(255)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_ProviderInvoiceItem_Id_InvoiceId",
            table: "ProviderInvoiceItem",
            columns: new[] { "Id", "InvoiceId" },
            unique: true);
    }
}
