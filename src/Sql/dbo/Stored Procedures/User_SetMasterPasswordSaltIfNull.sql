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
    --
    -- @MasterPasswordSalt is written verbatim; this procedure does no normalization and does not
    -- verify the value against [Email]. Deriving the salt — lowercasing and trimming the email — is
    -- deliberately the caller's job, so that normalization lives in exactly one place
    -- (UpdateMasterPasswordSaltCommand) rather than being restated in T-SQL and in EF LINQ where the
    -- three could drift. Do not reintroduce a LOWER/LTRIM/RTRIM comparison here.
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
END
