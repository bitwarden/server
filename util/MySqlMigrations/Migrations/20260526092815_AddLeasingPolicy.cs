using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class AddLeasingPolicy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LeasingEnabled",
            table: "Collection");

        migrationBuilder.DropColumn(
            name: "LeasingPolicy",
            table: "Collection");

        migrationBuilder.AddColumn<Guid>(
            name: "LeasingPolicyId",
            table: "Collection",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.CreateTable(
            name: "LeasingPolicy",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Description = table.Column<string>(type: "longtext", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Policy = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
            })
            .Annotation("MySql:CharSet", "utf8mb4");

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
            onDelete: ReferentialAction.Restrict);
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

        migrationBuilder.DropColumn(
            name: "LeasingPolicyId",
            table: "Collection");

        migrationBuilder.AddColumn<bool>(
            name: "LeasingEnabled",
            table: "Collection",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
                name: "LeasingPolicy",
                table: "Collection",
                type: "longtext",
                nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }
}
