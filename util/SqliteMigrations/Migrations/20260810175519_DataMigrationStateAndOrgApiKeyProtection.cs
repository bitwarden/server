using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class DataMigrationStateAndOrgApiKeyProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataMigrationState",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Partition = table.Column<int>(type: "INTEGER", nullable: false),
                    RangeStart = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    RangeEnd = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Cursor = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    TotalRows = table.Column<long>(type: "INTEGER", nullable: false),
                    RowsScanned = table.Column<long>(type: "INTEGER", nullable: false),
                    RowsConverted = table.Column<long>(type: "INTEGER", nullable: false),
                    RowsSkippedByRace = table.Column<long>(type: "INTEGER", nullable: false),
                    RowsFailed = table.Column<long>(type: "INTEGER", nullable: false),
                    LeaseOwner = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LeaseExpiresDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevisionDate = table.Column<DateTime>(type: "TEXT", nullable: false)
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
        }
    }
}
