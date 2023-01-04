START TRANSACTION;

CREATE TABLE "AccessPolicy" (
    "Id" uuid NOT NULL,
    "GroupId" uuid NULL,
    "GrantedProjectId" uuid NULL,
    "GrantedServiceAccountId" uuid NULL,
    "ServiceAccountId" uuid NULL,
    "OrganizationUserId" uuid NULL,
    "Read" boolean NOT NULL,
    "Write" boolean NOT NULL,
    "CreationDate" timestamp without time zone NOT NULL,
    "RevisionDate" timestamp without time zone NOT NULL,
    "Discriminator" text NOT NULL,
    CONSTRAINT "PK_AccessPolicy" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AccessPolicy_Group_GroupId" FOREIGN KEY ("GroupId") REFERENCES "Group" ("Id"),
    CONSTRAINT "FK_AccessPolicy_OrganizationUser_OrganizationUserId" FOREIGN KEY ("OrganizationUserId") REFERENCES "OrganizationUser" ("Id"),
    CONSTRAINT "FK_AccessPolicy_Project_GrantedProjectId" FOREIGN KEY ("GrantedProjectId") REFERENCES "Project" ("Id"),
    CONSTRAINT "FK_AccessPolicy_ServiceAccount_GrantedServiceAccountId" FOREIGN KEY ("GrantedServiceAccountId") REFERENCES "ServiceAccount" ("Id"),
    CONSTRAINT "FK_AccessPolicy_ServiceAccount_ServiceAccountId" FOREIGN KEY ("ServiceAccountId") REFERENCES "ServiceAccount" ("Id")
);

CREATE INDEX "IX_AccessPolicy_GrantedProjectId" ON "AccessPolicy" ("GrantedProjectId");

CREATE INDEX "IX_AccessPolicy_GrantedServiceAccountId" ON "AccessPolicy" ("GrantedServiceAccountId");

CREATE INDEX "IX_AccessPolicy_GroupId" ON "AccessPolicy" ("GroupId");

CREATE INDEX "IX_AccessPolicy_OrganizationUserId" ON "AccessPolicy" ("OrganizationUserId");

CREATE INDEX "IX_AccessPolicy_ServiceAccountId" ON "AccessPolicy" ("ServiceAccountId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20221125155541_AccessPolicy', '6.0.4');

COMMIT;
