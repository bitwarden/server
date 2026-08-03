CREATE OR ALTER PROCEDURE [dbo].[User_TrySetUserKeyId]
    @Id UNIQUEIDENTIFIER,
    @UserKeyId VARCHAR(32),
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
GO

CREATE OR ALTER PROCEDURE [dbo].[User_SetUserKeyId]
    @Id UNIQUEIDENTIFIER,
    @UserKeyId VARCHAR(32),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Unconditional, unlike [User_TrySetUserKeyId]. This is for flows that establish the user key
    -- itself, where the supplied key id is authoritative rather than a backfill guess.
    UPDATE
        [dbo].[User]
    SET
        [UserKeyId] = @UserKeyId,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO
