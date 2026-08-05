CREATE PROCEDURE [dbo].[User_SetMasterPasswordSaltIfNull]
    @Id UNIQUEIDENTIFIER,
    @MasterPasswordSalt NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    -- Backfills [MasterPasswordSalt] for users who predate the column, so the salt becomes a stored
    -- value rather than one derived from the email at read time (see User.GetMasterPasswordSalt).
    --
    -- Every guard lives in the WHERE clause rather than the caller, so the write is idempotent and
    -- safe under concurrency: this runs on token refresh, and the same user can have several
    -- refreshes in flight at once. A caller that read a stale "salt is null" cannot clobber a salt
    -- another request just wrote.
    --
    --   [MasterPasswordSalt] IS NULL  — only ever fill a blank; never overwrite an existing salt.
    --   [MasterPassword] IS NOT NULL  — Key Connector / TDE users have no master password, so there
    --                                   is no salt to prefill.
    --   LOWER(LTRIM(RTRIM([Email]))) = @MasterPasswordSalt
    --                                 — the value written must genuinely be this user's normalized
    --                                   email. This is what makes the backfill unobservable to
    --                                   clients (it matches what they already derive) and stops a
    --                                   caller passing an arbitrary salt for someone else's row.
    --
    -- RevisionDate / AccountRevisionDate are deliberately not bumped: the stored value equals what
    -- clients already derive, so there is nothing for them to re-sync.
    UPDATE
        [dbo].[User]
    SET
        [MasterPasswordSalt] = @MasterPasswordSalt
    WHERE
        [Id] = @Id
        AND [MasterPasswordSalt] IS NULL
        AND [MasterPassword] IS NOT NULL
        AND LOWER(LTRIM(RTRIM([Email]))) = @MasterPasswordSalt
END
