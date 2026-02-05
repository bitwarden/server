using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddGatewayIndexes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_User_GatewayCustomerId",
            table: "User",
            column: "GatewayCustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_User_GatewaySubscriptionId",
            table: "User",
            column: "GatewaySubscriptionId");

        migrationBuilder.CreateIndex(
            name: "IX_Provider_GatewayCustomerId",
            table: "Provider",
            column: "GatewayCustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Provider_GatewaySubscriptionId",
            table: "Provider",
            column: "GatewaySubscriptionId");

        migrationBuilder.CreateIndex(
            name: "IX_Organization_GatewayCustomerId",
            table: "Organization",
            column: "GatewayCustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Organization_GatewaySubscriptionId",
            table: "Organization",
            column: "GatewaySubscriptionId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_User_GatewayCustomerId",
            table: "User");

        migrationBuilder.DropIndex(
            name: "IX_User_GatewaySubscriptionId",
            table: "User");

        migrationBuilder.DropIndex(
            name: "IX_Provider_GatewayCustomerId",
            table: "Provider");

        migrationBuilder.DropIndex(
            name: "IX_Provider_GatewaySubscriptionId",
            table: "Provider");

        migrationBuilder.DropIndex(
            name: "IX_Organization_GatewayCustomerId",
            table: "Organization");

        migrationBuilder.DropIndex(
            name: "IX_Organization_GatewaySubscriptionId",
            table: "Organization");
    }
}
