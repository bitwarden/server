-- Adding indices
IF NOT EXISTS (SELECT *
               FROM   sys.indexes
               WHERE  [name] = 'IX_OrganizationUser_EmailOrganizationIdStatus'
                 AND object_id = Object_id('[dbo].[OrganizationUser]'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrganizationUser_EmailOrganizationIdStatus]
            ON [dbo].[OrganizationUser]([email] ASC, [organizationid] ASC, [status] ASC)
            WITH (online = ON);
    END

go

IF NOT EXISTS (SELECT *
               FROM   sys.indexes
               WHERE  [name] =
                      'IX_ProviderOrganization_OrganizationIdProviderId'
                 AND object_id = Object_id('[dbo].[ProviderOrganization]'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_ProviderOrganization_OrganizationIdProviderId]
            ON [dbo].[ProviderOrganization]([organizationid] ASC, [providerid] ASC)
            WITH (online = ON);
    END

go

IF NOT EXISTS (SELECT *
               FROM   sys.indexes
               WHERE  [name] = 'IX_ProviderUser_UserIdProviderId'
                 AND object_id = Object_id('[dbo].[ProviderUser]'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_ProviderUser_UserIdProviderId]
            ON [dbo].[ProviderUser]([userid] ASC, [providerid] ASC)
            WITH (online = ON);
    END

go
