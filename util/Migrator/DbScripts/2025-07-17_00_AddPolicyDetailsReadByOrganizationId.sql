CREATE OR ALTER PROCEDURE [dbo].[PolicyDetails_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @PolicyType  TINYINT
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH givenorgusers
                      AS (SELECT OU.id AS OrganizationUserID,
                                 OU.userid,
                                 U.email
                          FROM   dbo.organizationuserview OU
                                     INNER JOIN dbo.userview U
                                                ON U.id = OU.userid
                          WHERE  OU.organizationid = @OrganizationId
                          UNION ALL
                          SELECT OU.id AS OrganizationUserID,
                                 U.id  AS UserId,
                                 U.email
                          FROM   dbo.organizationuserview OU
                                     INNER JOIN dbo.userview U
                                                ON U.email = OU.email
                          WHERE  OU.organizationid = @OrganizationId),
          allorgusers
                      AS (SELECT OU.id            AS OrganizationUserID,
                                 OU.userid,
                                 Ou.organizationid,
                                 AU.email,
                                 OU.[type]        AS OrganizationUserType,
                                 OU.[status]      AS OrganizationUserStatus,
                                 OU.[permissions] AS OrganizationUserPermissionsData
                          FROM   dbo.organizationuserview OU
                                     INNER JOIN givenorgusers AU
                                                ON AU.userid = OU.userid
                          UNION ALL
                          SELECT OU.id            AS OrganizationUserID,
                                 AU.userid,
                                 Ou.organizationid,
                                 AU.email,
                                 OU.[type]        AS OrganizationUserType,
                                 OU.[status]      AS OrganizationUserStatus,
                                 OU.[permissions] AS OrganizationUserPermissionsData
                          FROM   dbo.organizationuserview OU
                                     INNER JOIN givenorgusers AU
                                                ON AU.email = OU.email)
     SELECT OU.organizationuserid,
            P.[organizationid],
            P.[type] AS PolicyType,
            P.[data] AS PolicyData,
            OU.organizationusertype,
            OU.organizationuserstatus,
            OU.organizationuserpermissionsdata
                     ,CASE
                          WHEN EXISTS (SELECT 1
                                       FROM [dbo].[ProviderUserView] PU
                                        INNER JOIN [dbo].[ProviderOrganizationView] PO
                                                   ON PO.[ProviderId] = PU.[ProviderId]
                               WHERE PU.[UserId] = OU.[UserId]
                                 AND PO.[OrganizationId] = P.[OrganizationId]) THEN 1
                          ELSE 0 END   AS IsProvider
     FROM   [dbo].[policyview] P
                 INNER JOIN [dbo].[organizationview] O
     ON P.[organizationid] = O.[id]
                 INNER JOIN allorgusers OU
                 ON OU.organizationid = O.[id]
     WHERE  P.enabled = 1
       AND O.enabled = 1
       AND O.usepolicies = 1
       AND p.type = @PolicyType;
END
GO

-- Adding indices
IF NOT EXISTS (SELECT *
               FROM   sys.indexes
               WHERE  [name] = 'IX_OrganizationUser_EmailOrganizationIdStatus'
                 AND object_id = Object_id('[dbo].[OrganizationUser]'))
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrganizationUser_EmailOrganizationIdStatus]
            ON [dbo].[OrganizationUser]([email] ASC, [organizationid] ASC, [status]
                                        ASC)
            WITH (online = ON);
    END

go

IF NOT EXISTS (SELECT *
               FROM   sys.indexes
               WHERE  [name] =
                      'IX_ProviderOrganization_OrganizationIdProviderId'
                 AND object_id = Object_id('[dbo].[ProviderOrganization]'))
    BEGIN
        CREATE NONCLUSTERED INDEX
            [IX_ProviderOrganization_OrganizationIdProviderId]
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
