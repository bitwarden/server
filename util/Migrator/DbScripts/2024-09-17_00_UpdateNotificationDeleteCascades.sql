-- NotificationStatus

IF OBJECT_ID('[dbo].[FK_NotificationStatus_Notification]', 'F') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[NotificationStatus]
            DROP CONSTRAINT [FK_NotificationStatus_Notification]
    END
GO

ALTER TABLE [dbo].[NotificationStatus]
    ADD CONSTRAINT [FK_NotificationStatus_Notification] FOREIGN KEY ([NotificationId]) REFERENCES [dbo].[Notification] ([Id])
GO

IF NOT EXISTS(SELECT name
              FROM sys.indexes
              WHERE name = 'IX_NotificationStatus_UserId')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_NotificationStatus_UserId]
            ON [dbo].[NotificationStatus] ([UserId] ASC);
    END
GO

-- Stored Procedure Organization_DeleteById

CREATE OR ALTER PROCEDURE [dbo].[Organization_DeleteById]
@Id UNIQUEIDENTIFIER
    WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
        BEGIN
            BEGIN TRANSACTION Organization_DeleteById_Ciphers

                DELETE TOP(@BatchSize)
                FROM
                    [dbo].[Cipher]
                WHERE
                    [UserId] IS NULL
                  AND [OrganizationId] = @Id

                SET @BatchSize = @@ROWCOUNT

            COMMIT TRANSACTION Organization_DeleteById_Ciphers
        END

    BEGIN TRANSACTION Organization_DeleteById

        DELETE
        FROM
            [dbo].[AuthRequest]
        WHERE
            [OrganizationId] = @Id

        DELETE
        FROM
            [dbo].[SsoUser]
        WHERE
            [OrganizationId] = @Id

        DELETE
        FROM
            [dbo].[SsoConfig]
        WHERE
            [OrganizationId] = @Id

        DELETE CU
        FROM
            [dbo].[CollectionUser] CU
                INNER JOIN
            [dbo].[OrganizationUser] OU ON [CU].[OrganizationUserId] = [OU].[Id]
        WHERE
            [OU].[OrganizationId] = @Id

        DELETE AP
        FROM
            [dbo].[AccessPolicy] AP
                INNER JOIN
            [dbo].[OrganizationUser] OU ON [AP].[OrganizationUserId] = [OU].[Id]
        WHERE
            [OU].[OrganizationId] = @Id

        DELETE GU
        FROM
            [dbo].[GroupUser] GU
                INNER JOIN
            [dbo].[OrganizationUser] OU ON [GU].[OrganizationUserId] = [OU].[Id]
        WHERE
            [OU].[OrganizationId] = @Id

        DELETE
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [OrganizationId] = @Id

        DELETE
        FROM
            [dbo].[ProviderOrganization]
        WHERE
            [OrganizationId] = @Id

        EXEC [dbo].[OrganizationApiKey_OrganizationDeleted] @Id
        EXEC [dbo].[OrganizationConnection_OrganizationDeleted] @Id
        EXEC [dbo].[OrganizationSponsorship_OrganizationDeleted] @Id
        EXEC [dbo].[OrganizationDomain_OrganizationDeleted] @Id

        DELETE
        FROM
            [dbo].[Project]
        WHERE
            [OrganizationId] = @Id

        DELETE
        FROM
            [dbo].[Secret]
        WHERE
            [OrganizationId] = @Id

        DELETE AK
        FROM
            [dbo].[ApiKey] AK
                INNER JOIN
            [dbo].[ServiceAccount] SA ON [AK].[ServiceAccountId] = [SA].[Id]
        WHERE
            [SA].[OrganizationId] = @Id

        DELETE AP
        FROM
            [dbo].[AccessPolicy] AP
                INNER JOIN
            [dbo].[ServiceAccount] SA ON [AP].[GrantedServiceAccountId] = [SA].[Id]
        WHERE
            [SA].[OrganizationId] = @Id

        DELETE
        FROM
            [dbo].[ServiceAccount]
        WHERE
            [OrganizationId] = @Id

        -- Delete Notification Status
        DELETE
            NS
        FROM
            [dbo].[NotificationStatus] NS
                INNER JOIN
            [dbo].[Notification] N ON N.[Id] = NS.[NotificationId]
        WHERE
            N.[OrganizationId] = @Id

        -- Delete Notification
        DELETE
        FROM
            [dbo].[Notification]
        WHERE
            [OrganizationId] = @Id

        DELETE
        FROM
            [dbo].[Organization]
        WHERE
            [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO

-- Stored Procedure User_DeleteById

CREATE OR ALTER PROCEDURE [dbo].[User_DeleteById]
@Id UNIQUEIDENTIFIER
    WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
        BEGIN
            BEGIN TRANSACTION User_DeleteById_Ciphers

                DELETE TOP(@BatchSize)
                FROM
                    [dbo].[Cipher]
                WHERE
                    [UserId] = @Id

                SET @BatchSize = @@ROWCOUNT

            COMMIT TRANSACTION User_DeleteById_Ciphers
        END

    BEGIN TRANSACTION User_DeleteById

        -- Delete WebAuthnCredentials
        DELETE
        FROM
            [dbo].[WebAuthnCredential]
        WHERE
            [UserId] = @Id

        -- Delete folders
        DELETE
        FROM
            [dbo].[Folder]
        WHERE
            [UserId] = @Id

        -- Delete AuthRequest, must be before Device
        DELETE
        FROM
            [dbo].[AuthRequest]
        WHERE
            [UserId] = @Id

        -- Delete devices
        DELETE
        FROM
            [dbo].[Device]
        WHERE
            [UserId] = @Id

        -- Delete collection users
        DELETE
            CU
        FROM
            [dbo].[CollectionUser] CU
                INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
        WHERE
            OU.[UserId] = @Id

        -- Delete group users
        DELETE
            GU
        FROM
            [dbo].[GroupUser] GU
                INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
        WHERE
            OU.[UserId] = @Id

        -- Delete AccessPolicy
        DELETE
            AP
        FROM
            [dbo].[AccessPolicy] AP
                INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[Id] = AP.[OrganizationUserId]
        WHERE
            [UserId] = @Id

        -- Delete organization users
        DELETE
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [UserId] = @Id

        -- Delete provider users
        DELETE
        FROM
            [dbo].[ProviderUser]
        WHERE
            [UserId] = @Id

        -- Delete SSO Users
        DELETE
        FROM
            [dbo].[SsoUser]
        WHERE
            [UserId] = @Id

        -- Delete Emergency Accesses
        DELETE
        FROM
            [dbo].[EmergencyAccess]
        WHERE
            [GrantorId] = @Id
           OR
            [GranteeId] = @Id

        -- Delete Sends
        DELETE
        FROM
            [dbo].[Send]
        WHERE
            [UserId] = @Id

        -- Delete Notification Status
        DELETE
        FROM
            [dbo].[NotificationStatus]
        WHERE
            [UserId] = @Id

        -- Delete Notification
        DELETE
        FROM
            [dbo].[Notification]
        WHERE
            [UserId] = @Id

        -- Finally, delete the user
        DELETE
        FROM
            [dbo].[User]
        WHERE
            [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
GO
