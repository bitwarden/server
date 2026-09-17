using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class RenameAuditAccessConnectorColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DaemonName",
                table: "AccessAuditEvent",
                newName: "AccessConnectorName");

            migrationBuilder.RenameColumn(
                name: "DaemonId",
                table: "AccessAuditEvent",
                newName: "AccessConnectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccessConnectorName",
                table: "AccessAuditEvent",
                newName: "DaemonName");

            migrationBuilder.RenameColumn(
                name: "AccessConnectorId",
                table: "AccessAuditEvent",
                newName: "DaemonId");
        }
    }
}
