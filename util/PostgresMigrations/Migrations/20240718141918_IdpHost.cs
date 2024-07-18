using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class IdpHost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdpHost",
                table: "Organization",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdpHost",
                table: "Organization");
        }
    }
}
