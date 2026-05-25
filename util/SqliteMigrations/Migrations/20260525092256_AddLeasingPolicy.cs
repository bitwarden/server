using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddLeasingPolicy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LeasingEnabled",
            table: "Collection");

        migrationBuilder.RenameColumn(
            name: "LeasingPolicy",
            table: "Collection",
            newName: "LeasingPolicyId");

        migrationBuilder.CreateTable(
            name: "LeasingPolicy",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Policy = table.Column<string>(type: "TEXT", nullable: false),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LeasingPolicy", x => x.Id);
                table.ForeignKey(
                    name: "FK_LeasingPolicy_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Collection_LeasingPolicyId",
            table: "Collection",
            column: "LeasingPolicyId");

        migrationBuilder.CreateIndex(
            name: "IX_LeasingPolicy_OrganizationId_Name",
            table: "LeasingPolicy",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Collection_LeasingPolicy_LeasingPolicyId",
            table: "Collection",
            column: "LeasingPolicyId",
            principalTable: "LeasingPolicy",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Collection_LeasingPolicy_LeasingPolicyId",
            table: "Collection");

        migrationBuilder.DropTable(
            name: "LeasingPolicy");

        migrationBuilder.DropIndex(
            name: "IX_Collection_LeasingPolicyId",
            table: "Collection");

        migrationBuilder.RenameColumn(
            name: "LeasingPolicyId",
            table: "Collection",
            newName: "LeasingPolicy");

        migrationBuilder.AddColumn<bool>(
            name: "LeasingEnabled",
            table: "Collection",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }
}
