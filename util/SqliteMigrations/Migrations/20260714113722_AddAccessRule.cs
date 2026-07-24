using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class AddAccessRule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AccessRuleId",
            table: "Collection",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "AccessRule",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Conditions = table.Column<string>(type: "TEXT", nullable: false),
                SingleActiveLease = table.Column<bool>(type: "INTEGER", nullable: false),
                DefaultLeaseDurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                MaxLeaseDurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                AllowsExtensions = table.Column<bool>(type: "INTEGER", nullable: false),
                MaxExtensionDurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                LastEditedBy = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccessRule", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccessRule_Organization_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organization",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Collection_AccessRuleId",
            table: "Collection",
            column: "AccessRuleId");

        migrationBuilder.CreateIndex(
            name: "IX_AccessRule_OrganizationId_Name",
            table: "AccessRule",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Collection_AccessRule_AccessRuleId",
            table: "Collection",
            column: "AccessRuleId",
            principalTable: "AccessRule",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Collection_AccessRule_AccessRuleId",
            table: "Collection");

        migrationBuilder.DropTable(
            name: "AccessRule");

        migrationBuilder.DropIndex(
            name: "IX_Collection_AccessRuleId",
            table: "Collection");

        migrationBuilder.DropColumn(
            name: "AccessRuleId",
            table: "Collection");
    }
}
