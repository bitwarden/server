using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations
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
                type: "TEXT",
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
