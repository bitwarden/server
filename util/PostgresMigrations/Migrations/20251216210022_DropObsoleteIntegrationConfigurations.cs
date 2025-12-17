using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.PostgresMigrations.Migrations;

/// <inheritdoc />
public partial class DropObsoleteIntegrationConfigurations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            /*  postgresql */
                DELETE FROM public.""OrganizationIntegrationConfiguration"" WHERE ""Template"" like '%""service"":""Datadog""%';
                DELETE FROM public.""OrganizationIntegrationConfiguration"" WHERE ""Template"" like '%""service"":""Crowdstrike""%';

                DELETE FROM public.""OrganizationIntegrationConfiguration"" WHERE ""OrganizationIntegrationId"" in 
                (SELECT ""Id"" FROM public.""OrganizationIntegration"" WHERE ""Type"" in (6) and ""Configuration"" like '%""service"":""Datadog""%');
                DELETE FROM public.""OrganizationIntegrationConfiguration"" WHERE ""OrganizationIntegrationId"" in 
                (SELECT ""Id"" FROM public.""OrganizationIntegration"" WHERE ""Type"" in (5) and ""Configuration"" like '%""service"":""Crowdstrike""%');

                DELETE FROM public.""OrganizationIntegration"" WHERE ""Type"" in (6) and ""Configuration"" like '%""service"":""Datadog""%';
                DELETE FROM public.""OrganizationIntegration"" WHERE ""Type"" in (5) and ""Configuration"" like '%""service"":""Crowdstrike""%';

        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
