using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bit.SqliteMigrations.Migrations;

/// <inheritdoc />
public partial class DropObsoleteIntegrationConfigurations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DELETE FROM OrganizationIntegrationConfiguration WHERE Template like '%""service"":""Datadog""%';
            DELETE FROM OrganizationIntegrationConfiguration WHERE Template like '%""service"":""Crowdstrike""%';

            DELETE FROM OrganizationIntegrationConfiguration WHERE OrganizationIntegrationId in 
            (SELECT Id FROM OrganizationIntegration WHERE Type in (6) and Configuration like '%""service"":""Datadog""%');
            DELETE FROM OrganizationIntegrationConfiguration WHERE OrganizationIntegrationId in 
            (SELECT Id FROM OrganizationIntegration WHERE Type in (5) and Configuration like '%""service"":""Crowdstrike""%');

            DELETE FROM OrganizationIntegration WHERE Type in (6) and Configuration like '%""service"":""Datadog""%';
            DELETE FROM OrganizationIntegration WHERE Type in (5) and Configuration like '%""service"":""Crowdstrike""%';
        ");

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

    }
}
