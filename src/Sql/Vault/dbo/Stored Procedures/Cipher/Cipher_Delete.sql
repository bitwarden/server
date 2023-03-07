CREATE PROCEDURE [dbo].[Cipher_Delete]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [OrganizationId] UNIQUEIDENTIFIER NULL,
        [Attachments] BIT NOT NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId],
        [OrganizationId],
        CASE WHEN [Attachments] IS NULL THEN 0 ELSE 1 END
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
        AND [Id] IN (SELECT * FROM @Ids)

    -- Delete ciphers
    DELETE
    FROM
        [dbo].[Cipher]
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
        EXEC [dbo].[Organization_UpdateStorage] @OrgId
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
        FETCH NEXT FROM [OrgCursor] INTO @OrgId
    END
    CLOSE [OrgCursor]
    DEALLOCATE [OrgCursor]

    -- Cleanup user
    DECLARE @UserCiphersWithStorageCount INT
    SELECT
        @UserCiphersWithStorageCount = COUNT(1)
    FROM
        #Temp
    WHERE
        [UserId] IS NOT NULL
        AND [Attachments] = 1

    IF @UserCiphersWithStorageCount > 0
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
    END
    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp
END