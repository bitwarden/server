CREATE PROCEDURE [dbo].[User_TrySetUserKeyId]
    @Id UNIQUEIDENTIFIER,
    @UserKeyId VARCHAR(64),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The [UserKeyId] IS NULL predicate makes "only set when not already set" atomic,
    -- so two clients racing to backfill cannot both succeed.
    UPDATE
        [dbo].[User]
    SET
        [UserKeyId] = @UserKeyId,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
        AND [UserKeyId] IS NULL

    SELECT @@ROWCOUNT
END
