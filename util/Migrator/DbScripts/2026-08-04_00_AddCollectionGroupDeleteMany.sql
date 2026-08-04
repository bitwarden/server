CREATE OR ALTER PROCEDURE [dbo].[CollectionGroup_DeleteMany]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @GroupIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [CollectionId] IN (SELECT [Id] FROM @CollectionIds)
        AND [GroupId] IN (SELECT [Id] FROM @GroupIds)

    UPDATE
        [dbo].[Group]
    SET
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] IN (SELECT [Id] FROM @GroupIds)
END
