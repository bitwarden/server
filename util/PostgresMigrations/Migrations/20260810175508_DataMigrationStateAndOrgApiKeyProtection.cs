using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations
{
    /// <inheritdoc />
    public partial class DataMigrationStateAndOrgApiKeyProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ApiKey",
                table: "OrganizationApiKey",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateTable(
                name: "DataMigrationState",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Partition = table.Column<int>(type: "integer", nullable: false),
                    RangeStart = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RangeEnd = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Cursor = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TotalRows = table.Column<long>(type: "bigint", nullable: false),
                    RowsScanned = table.Column<long>(type: "bigint", nullable: false),
                    RowsConverted = table.Column<long>(type: "bigint", nullable: false),
                    RowsSkippedByRace = table.Column<long>(type: "bigint", nullable: false),
                    RowsFailed = table.Column<long>(type: "bigint", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LeaseExpiresDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataMigrationState", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataMigrationState_Name_Partition",
                table: "DataMigrationState",
                columns: new[] { "Name", "Partition" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataMigrationState");

            migrationBuilder.AlterColumn<string>(
                name: "ApiKey",
                table: "OrganizationApiKey",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);
        }
    }
}
