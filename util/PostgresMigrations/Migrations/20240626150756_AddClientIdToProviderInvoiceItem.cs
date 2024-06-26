using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIdToProviderInvoiceItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProviderInvoiceItem_Id_InvoiceId",
                table: "ProviderInvoiceItem");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "ProviderInvoiceItem",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "ProviderInvoiceItem");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderInvoiceItem_Id_InvoiceId",
                table: "ProviderInvoiceItem",
                columns: new[] { "Id", "InvoiceId" },
                unique: true);
        }
    }
}
