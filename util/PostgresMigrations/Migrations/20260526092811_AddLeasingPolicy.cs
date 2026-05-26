using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

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
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "LeasingPolicy",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                Policy = table.Column<string>(type: "text", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "LeasingPolicy",
            table: "Collection",
            type: "text",
            nullable: true);
    }
}
