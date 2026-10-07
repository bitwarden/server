CREATE PROCEDURE [dbo].[SecurityTask_MarkCompleteByCipherIds]
    @CipherIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[SecurityTask]
    SET
        [Status] = 1, -- completed
        [RevisionDate] = SYSUTCDATETIME()
    WHERE
        [CipherId] IN (SELECT [Id] FROM @CipherIds)
        AND [Status] <> 1 -- Not already completed
END
