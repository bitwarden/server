using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class CreateSendControlsPolicies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            INSERT INTO ""Policy"" (""Id"", ""OrganizationId"", ""Type"", ""Enabled"", ""Data"", ""CreationDate"", ""RevisionDate"")
            SELECT gen_random_uuid(),
                   COALESCE(ds.""OrganizationId"", so.""OrganizationId""),
                   20,
                   (COALESCE(ds.""Enabled"", false) OR COALESCE(so.""Enabled"", false)),
                   jsonb_build_object(
                       'disableSend', COALESCE(ds.""Enabled"", false),
                       'disableHideEmail',
                           CASE WHEN so.""Data"" IS NOT NULL
                                     AND so.""Data"" ~ '^\{.*\}$'
                                THEN COALESCE((so.""Data""::jsonb ->> 'disableHideEmail')::boolean, false)
                                ELSE false END
                   )::text,
                   NOW() AT TIME ZONE 'UTC',
                   NOW() AT TIME ZONE 'UTC'
            FROM (
                SELECT ds2.""OrganizationId"", ds2.""Enabled""
                FROM ""Policy"" ds2 WHERE ds2.""Type"" = 6
            ) ds
            FULL OUTER JOIN (
                SELECT so2.""OrganizationId"", so2.""Enabled"", so2.""Data""
                FROM ""Policy"" so2 WHERE so2.""Type"" = 7
            ) so ON ds.""OrganizationId"" = so.""OrganizationId""
            WHERE NOT EXISTS (
                SELECT 1 FROM ""Policy"" sc
                WHERE sc.""OrganizationId"" = COALESCE(ds.""OrganizationId"", so.""OrganizationId"")
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
