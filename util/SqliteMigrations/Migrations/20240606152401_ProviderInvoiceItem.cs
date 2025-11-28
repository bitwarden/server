using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class ProviderInvoiceItem : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProviderInvoiceItem",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProviderId = table.Column<Guid>(type: "TEXT", nullable: false),
                InvoiceId = table.Column<string>(type: "TEXT", nullable: true),
                InvoiceNumber = table.Column<string>(type: "TEXT", nullable: true),
                ClientName = table.Column<string>(type: "TEXT", nullable: true),
                PlanName = table.Column<string>(type: "TEXT", nullable: true),
                AssignedSeats = table.Column<int>(type: "INTEGER", nullable: false),
                UsedSeats = table.Column<int>(type: "INTEGER", nullable: false),
                Total = table.Column<decimal>(type: "TEXT", nullable: false),
                Created = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderInvoiceItem", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProviderInvoiceItem_Provider_ProviderId",
                    column: x => x.ProviderId,
                    principalTable: "Provider",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProviderInvoiceItem_Id_InvoiceId",
            table: "ProviderInvoiceItem",
            columns: new[] { "Id", "InvoiceId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProviderInvoiceItem_ProviderId",
            table: "ProviderInvoiceItem",
            column: "ProviderId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProviderInvoiceItem");
    }
}
