using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class Net8Sync : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProviderInvoiceItem_Id_InvoiceId",
            table: "ProviderInvoiceItem");

        migrationBuilder.AlterColumn<string>(
            name: "Discriminator",
            table: "AccessPolicy",
            type: "character varying(34)",
            maxLength: 34,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Discriminator",
            table: "AccessPolicy",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(34)",
            oldMaxLength: 34);

        migrationBuilder.CreateIndex(
            name: "IX_ProviderInvoiceItem_Id_InvoiceId",
            table: "ProviderInvoiceItem",
            columns: new[] { "Id", "InvoiceId" },
            unique: true);
    }
}
