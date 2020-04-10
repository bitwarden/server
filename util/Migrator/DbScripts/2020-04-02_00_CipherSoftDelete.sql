/**
 * Fix bulk Restore/Soft Delete sprocs for DateTime assignment
 */
IF OBJECT_ID('[dbo].[Cipher_Restore]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Restore];
END
GO
CREATE PROCEDURE [dbo].[Cipher_Restore]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId],
        [OrganizationId]
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
        AND [Id] IN (SELECT * FROM @Ids)

    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();
    UPDATE
        [dbo].[Cipher]
    SET
        [DeletedDate] = NULL,
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    -- Bump orgs
    DECLARE @OrgId UNIQUEIDENTIFIER
    DECLARE [OrgCursor] CURSOR FORWARD_ONLY FOR
        SELECT
            [OrganizationId]
        FROM
            #Temp
        WHERE
            [OrganizationId] IS NOT NULL
        GROUP BY
            [OrganizationId]
    OPEN [OrgCursor]
    FETCH NEXT FROM [OrgCursor] INTO @OrgId
    WHILE @@FETCH_STATUS = 0 BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
        FETCH NEXT FROM [OrgCursor] INTO @OrgId
    END
    CLOSE [OrgCursor]
    DEALLOCATE [OrgCursor]

    -- Bump user
    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp
END
GO

IF OBJECT_ID('[dbo].[Cipher_SoftDelete]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_SoftDelete];
END
GO
CREATE PROCEDURE [dbo].[Cipher_SoftDelete]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId],
        [OrganizationId]
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
        AND [Id] IN (SELECT * FROM @Ids)

    -- Delete ciphers
    DECLARE @UtcNow DATETIME2(7) = GETUTCDATE();
    UPDATE
        [dbo].[Cipher]
    SET
        [DeletedDate] = @UtcNow,
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    -- Cleanup orgs
    DECLARE @OrgId UNIQUEIDENTIFIER
    DECLARE [OrgCursor] CURSOR FORWARD_ONLY FOR
        SELECT
            [OrganizationId]
        FROM
            #Temp
        WHERE
            [OrganizationId] IS NOT NULL
        GROUP BY
            [OrganizationId]
    OPEN [OrgCursor]
    FETCH NEXT FROM [OrgCursor] INTO @OrgId
    WHILE @@FETCH_STATUS = 0 BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
        FETCH NEXT FROM [OrgCursor] INTO @OrgId
    END
    CLOSE [OrgCursor]
    DEALLOCATE [OrgCursor]

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp
END
GO