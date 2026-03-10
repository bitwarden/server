using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class CreateSendControlsPolicies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQLite does not support FULL OUTER JOIN; use two separate inserts.
        // SQLite 3.38+ has json_valid() and json_extract().

        // Insert for orgs that have SendOptions (with or without DisableSend)
        migrationBuilder.Sql(@"
            INSERT INTO ""Policy"" (""Id"", ""OrganizationId"", ""Type"", ""Enabled"", ""Data"", ""CreationDate"", ""RevisionDate"")
            SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' ||
                   substr(lower(hex(randomblob(2))),2) || '-' ||
                   substr('89ab',abs(random()) % 4 + 1, 1) ||
                   substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))),
                   COALESCE(ds.""OrganizationId"", so.""OrganizationId""),
                   20,
                   CASE WHEN IFNULL(ds.""Enabled"", 0) = 1 OR IFNULL(so.""Enabled"", 0) = 1 THEN 1 ELSE 0 END,
                   '{{""disableSend"":' ||
                       CASE WHEN IFNULL(ds.""Enabled"", 0) = 1 THEN 'true' ELSE 'false' END ||
                   ',""disableHideEmail"":' ||
                       CASE WHEN so.""Data"" IS NOT NULL
                                 AND json_valid(so.""Data"") = 1
                                 AND json_extract(so.""Data"", '$.disableHideEmail') = 1
                            THEN 'true' ELSE 'false' END ||
                   '}}',
                   datetime('now'),
                   datetime('now')
            FROM (SELECT ""OrganizationId"", ""Enabled"", ""Data"" FROM ""Policy"" WHERE ""Type"" = 7) so
            LEFT JOIN (SELECT ""OrganizationId"", ""Enabled"" FROM ""Policy"" WHERE ""Type"" = 6) ds
              ON ds.""OrganizationId"" = so.""OrganizationId""
            WHERE NOT EXISTS (
                SELECT 1 FROM ""Policy"" sc
                WHERE sc.""OrganizationId"" = COALESCE(ds.""OrganizationId"", so.""OrganizationId"")
                  AND sc.""Type"" = 20
            );
        ");

        // Insert for orgs that have DisableSend ONLY (no SendOptions)
        migrationBuilder.Sql(@"
            INSERT INTO ""Policy"" (""Id"", ""OrganizationId"", ""Type"", ""Enabled"", ""Data"", ""CreationDate"", ""RevisionDate"")
            SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' ||
                   substr(lower(hex(randomblob(2))),2) || '-' ||
                   substr('89ab',abs(random()) % 4 + 1, 1) ||
                   substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))),
                   ds.""OrganizationId"",
                   20,
                   ds.""Enabled"",
                   '{{""disableSend"":' || CASE WHEN ds.""Enabled"" = 1 THEN 'true' ELSE 'false' END || ',""disableHideEmail"":false}}',
                   datetime('now'),
                   datetime('now')
            FROM (SELECT ""OrganizationId"", ""Enabled"" FROM ""Policy"" WHERE ""Type"" = 6) ds
            WHERE ds.""OrganizationId"" NOT IN (SELECT ""OrganizationId"" FROM ""Policy"" WHERE ""Type"" = 7)
              AND NOT EXISTS (
                SELECT 1 FROM ""Policy"" sc
                WHERE sc.""OrganizationId"" = ds.""OrganizationId""
                  AND sc.""Type"" = 20
            );
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DELETE FROM ""Policy"" WHERE ""Type"" = 20;
        ");
    }
}
