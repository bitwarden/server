CREATE OR ALTER PROCEDURE [dbo].[CollectionCipher_ReadSharedByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CC.[CollectionId],
        CC.[CipherId]
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        [dbo].[Collection] C ON C.[Id] = CC.[CollectionId]
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND C.[Type] = 0 -- SharedCollections only
END
GO

-- Update [IX_Collection_OrganizationId_IncludeAll] index to include [Type] column
IF EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_Collection_OrganizationId_IncludeAll' AND object_id = OBJECT_ID('[dbo].[Collection]'))
BEGIN
    DROP INDEX [IX_Collection_OrganizationId_IncludeAll] ON [dbo].[Collection]
END
GO

CREATE NONCLUSTERED INDEX [IX_Collection_OrganizationId_IncludeAll]
    ON [dbo].[Collection]([OrganizationId] ASC)
    INCLUDE([CreationDate], [Name], [RevisionDate], [Type])
GO
