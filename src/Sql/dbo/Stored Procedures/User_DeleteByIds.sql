CREATE PROCEDURE [dbo].[User_DeleteByIds]
    @Ids [dbo].[GuidIdArray]
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
            [UserId] IN (@Ids)

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    -- Delete WebAuthnCredentials
    DELETE
    FROM
        [dbo].[WebAuthnCredential]
    WHERE
        [UserId] IN (@Ids)

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] IN (@Ids)

    -- Delete AuthRequest, must be before Device
    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE 
        [UserId] IN (@Ids)

    -- Delete devices
    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] IN (@Ids)

    -- Delete collection users
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[UserId] IN (@Ids)

    -- Delete group users
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE
        OU.[UserId] IN (@Ids)

    -- Delete AccessPolicy
    DELETE
        AP
    FROM
        [dbo].[AccessPolicy] AP
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = AP.[OrganizationUserId]
    WHERE
        [UserId] IN (@Ids)

    -- Delete organization users
    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UserId] IN (@Ids)

    -- Delete provider users
    DELETE
    FROM
        [dbo].[ProviderUser]
    WHERE
        [UserId] IN (@Ids)

    -- Delete SSO Users
    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [UserId] IN (@Ids)

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
        [UserId] IN (@Ids)

    -- Delete Notification Status
    DELETE
    FROM
        [dbo].[NotificationStatus]
    WHERE
        [UserId] IN (@Ids)

    -- Delete Notification
    DELETE
    FROM
        [dbo].[Notification]
    WHERE
        [UserId] IN (@Ids)
    
    -- Finally, delete the user
    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
