CREATE OR ALTER PROCEDURE [dbo].[User_DeleteByIds]
    @Ids NVARCHAR(MAX)
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    -- Declare a table variable to hold the parsed JSON data
    DECLARE @ParsedIds TABLE (Id UNIQUEIDENTIFIER);

    -- Parse the JSON input into the table variable
    INSERT INTO @ParsedIds (Id)
    SELECT value
    FROM OPENJSON(@Ids);

    -- Check if the input table is empty
    IF (SELECT COUNT(1) FROM @ParsedIds) < 1
    BEGIN
        RETURN(-1);
    END

    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION User_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IN (SELECT * FROM @ParsedIds)

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION User_DeleteById_Ciphers
    END

    BEGIN TRANSACTION User_DeleteById

    -- Delete WebAuthnCredentials
    DELETE
    FROM
        [dbo].[WebAuthnCredential]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete folders
    DELETE
    FROM
        [dbo].[Folder]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete AuthRequest, must be before Device
    DELETE
    FROM
        [dbo].[AuthRequest]
    WHERE 
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete devices
    DELETE
    FROM
        [dbo].[Device]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete collection users
    DELETE
        CU
    FROM
        [dbo].[CollectionUser] CU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
    WHERE
        OU.[UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete group users
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
    WHERE
        OU.[UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete AccessPolicy
    DELETE
        AP
    FROM
        [dbo].[AccessPolicy] AP
        INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[Id] = AP.[OrganizationUserId]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete organization users
    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete provider users
    DELETE
    FROM
        [dbo].[ProviderUser]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete SSO Users
    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete Emergency Accesses
    DELETE
    FROM
        [dbo].[EmergencyAccess]
    WHERE
        [GrantorId] IN (SELECT * FROM @ParsedIds)
        OR
        [GranteeId] IN (SELECT * FROM @ParsedIds)

    -- Delete Sends
    DELETE
    FROM
        [dbo].[Send]
    WHERE 
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete Notification Status
    DELETE
    FROM
        [dbo].[NotificationStatus]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)

    -- Delete Notification
    DELETE
    FROM
        [dbo].[Notification]
    WHERE
        [UserId] IN (SELECT * FROM @ParsedIds)
    
    -- Finally, delete the user
    DELETE
    FROM
        [dbo].[User]
    WHERE
        [Id] IN (SELECT * FROM @ParsedIds)

    COMMIT TRANSACTION User_DeleteById
END
