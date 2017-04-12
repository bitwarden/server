CREATE PROCEDURE [dbo].[SubvaultCipher_UpdateSubvaults]
    @CipherId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @SubvaultIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [AvailableSubvaultsCTE] AS(
        SELECT
            SU.SubvaultId
        FROM
            [dbo].[SubvaultUser] SU
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
        INNER JOIN
            [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
        WHERE
            OU.[UserId] = @UserId
            AND SU.[ReadOnly] = 0
            AND OU.[Status] = 2 -- Confirmed
            AND O.[Enabled] = 1
    )
    MERGE
        [dbo].[SubvaultCipher] AS [Target]
    USING 
        @SubvaultIds AS [Source]
    ON
        [Target].[SubvaultId] = [Source].[Id]
        AND [Target].[CipherId] = @CipherId
    WHEN NOT MATCHED BY TARGET THEN
        INSERT VALUES
        (
            [Source].[Id],
            @CipherId
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CipherId] = @CipherId
    AND [Target].[SubvaultId] IN (SELECT [SubvaultId] FROM [AvailableSubvaultsCTE]) THEN
        DELETE
    ;
END