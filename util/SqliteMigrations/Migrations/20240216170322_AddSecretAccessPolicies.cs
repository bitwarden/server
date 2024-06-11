using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddSecretAccessPolicies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "GrantedSecretId",
            table: "AccessPolicy",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AccessPolicy_GrantedSecretId",
            table: "AccessPolicy",
            column: "GrantedSecretId");

        migrationBuilder.AddForeignKey(
            name: "FK_AccessPolicy_Secret_GrantedSecretId",
            table: "AccessPolicy",
            column: "GrantedSecretId",
            principalTable: "Secret",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AccessPolicy_Secret_GrantedSecretId",
            table: "AccessPolicy");

        migrationBuilder.DropIndex(
            name: "IX_AccessPolicy_GrantedSecretId",
            table: "AccessPolicy");

        migrationBuilder.DropColumn(
            name: "GrantedSecretId",
            table: "AccessPolicy");
    }
}
