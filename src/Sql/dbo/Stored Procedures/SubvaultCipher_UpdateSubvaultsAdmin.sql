CREATE PROCEDURE [dbo].[SubvaultCipher_UpdateSubvaultsAdmin]
    @CipherId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @SubvaultIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [AvailableSubvaultsCTE] AS(
        SELECT
            Id
        FROM
            [dbo].[Subvault]
        WHERE
            OrganizationId = @OrganizationId
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
    AND [Target].[CipherId] = @CipherId THEN
        DELETE
    ;
END