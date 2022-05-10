START TRANSACTION;

ALTER TABLE "OrganizationSponsorship" DROP CONSTRAINT "FK_OrganizationSponsorship_Organization_SponsoringOrganization~";

ALTER TABLE "OrganizationSponsorship" ALTER COLUMN "SponsoringOrganizationUserId" SET NOT NULL;
ALTER TABLE "OrganizationSponsorship" ALTER COLUMN "SponsoringOrganizationUserId" SET DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "OrganizationSponsorship" ALTER COLUMN "SponsoringOrganizationId" SET NOT NULL;
ALTER TABLE "OrganizationSponsorship" ALTER COLUMN "SponsoringOrganizationId" SET DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "OrganizationSponsorship" ADD CONSTRAINT "FK_OrganizationSponsorship_Organization_SponsoringOrganization~" FOREIGN KEY ("SponsoringOrganizationId") REFERENCES "Organization" ("Id") ON DELETE CASCADE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20220411190525_SponsorshipBulkActions', '5.0.12');

COMMIT;