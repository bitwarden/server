using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.MySqlMigrations.Migrations;

/// <inheritdoc />
public partial class SealPamRotationConfig : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // pam/uat only: not GA, so no data migration. Existing rows predate sealing and cannot be re-derived
        // server-side; wipe the rotation domain clean before reshaping. PamAccessConnector rows are untouched.
        migrationBuilder.Sql("DELETE FROM `PamRotationAttempt`;");
        migrationBuilder.Sql("DELETE FROM `PamRotationJob`;");
        migrationBuilder.Sql("DELETE FROM `PamRotationConfig`;");
        migrationBuilder.Sql("DELETE FROM `PamAccessConnectorTargetAssignment`;");
        migrationBuilder.Sql("DELETE FROM `PamTargetSystem`;");

        migrationBuilder.DropColumn(
            name: "Kind",
            table: "PamTargetSystem");

        migrationBuilder.DropColumn(
            name: "PasswordPolicy",
            table: "PamTargetSystem");

        migrationBuilder.DropColumn(
            name: "SupportsSessionTermination",
            table: "PamTargetSystem");

        migrationBuilder.DropColumn(
            name: "AccountIdentity",
            table: "PamRotationConfig");

        migrationBuilder.DropColumn(
            name: "TerminateSessions",
            table: "PamRotationConfig");

        migrationBuilder.AddColumn<string>(
            name: "SealedData",
            table: "PamTargetSystem",
            type: "varchar(10000)",
            maxLength: 10000,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "SealedData",
            table: "PamRotationConfig",
            type: "varchar(10000)",
            maxLength: 10000,
            nullable: false,
            defaultValue: "")
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SealedData",
            table: "PamTargetSystem");

        migrationBuilder.DropColumn(
            name: "SealedData",
            table: "PamRotationConfig");

        migrationBuilder.AddColumn<byte>(
            name: "Kind",
            table: "PamTargetSystem",
            type: "tinyint unsigned",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PasswordPolicy",
            table: "PamTargetSystem",
            type: "varchar(2000)",
            maxLength: 2000,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<bool>(
            name: "SupportsSessionTermination",
            table: "PamTargetSystem",
            type: "tinyint(1)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AccountIdentity",
            table: "PamRotationConfig",
            type: "varchar(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<bool>(
            name: "TerminateSessions",
            table: "PamRotationConfig",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);
    }
}
