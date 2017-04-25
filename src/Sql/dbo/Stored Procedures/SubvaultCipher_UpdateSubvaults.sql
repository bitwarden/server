CREATE PROCEDURE [dbo].[SubvaultCipher_UpdateSubvaults]
    @CipherId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @SubvaultIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[Cipher]
        WHERE
            [Id] = @CipherId
    )

    ;WITH [AvailableSubvaultsCTE] AS(
        SELECT
            S.[Id]
        FROM
            [dbo].[Subvault] S
        INNER JOIN
            [Organization] O ON O.[Id] = S.[OrganizationId]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[SubvaultUser] SU ON OU.[AccessAllSubvaults] = 0 AND SU.[SubvaultId] = S.[Id] AND SU.[OrganizationUserId] = OU.[Id]
        WHERE
            O.[Id] = @OrgId
            AND O.[Enabled] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND (OU.[AccessAllSubvaults] = 1 OR SU.[ReadOnly] = 0)
    )
    MERGE
        [dbo].[SubvaultCipher] AS [Target]
    USING 
        @SubvaultIds AS [Source]
    ON
        [Target].[SubvaultId] = [Source].[Id]
        AND [Target].[CipherId] = @CipherId
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableSubvaultsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @CipherId
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CipherId] = @CipherId
    AND [Target].[SubvaultId] IN (SELECT [Id] FROM [AvailableSubvaultsCTE]) THEN
        DELETE
    ;

    IF @OrgId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
    END
END