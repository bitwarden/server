using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class AddProviderIdColumnToTransactionTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ProviderId",
            table: "Transaction",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Transaction_ProviderId",
            table: "Transaction",
            column: "ProviderId");

        migrationBuilder.AddForeignKey(
            name: "FK_Transaction_Provider_ProviderId",
            table: "Transaction",
            column: "ProviderId",
            principalTable: "Provider",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Transaction_Provider_ProviderId",
            table: "Transaction");

        migrationBuilder.DropIndex(
            name: "IX_Transaction_ProviderId",
            table: "Transaction");

        migrationBuilder.DropColumn(
            name: "ProviderId",
            table: "Transaction");
    }
}
