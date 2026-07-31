CREATE OR ALTER PROCEDURE [dbo].[User_UpdateMasterPassword]
    @Id UNIQUEIDENTIFIER,
    @MasterPassword NVARCHAR(300),
    @MasterPasswordHint NVARCHAR(50) = NULL,
    @Key VARCHAR(MAX),
    @Kdf TINYINT,
    @KdfIterations INT,
    @KdfMemory INT = NULL,
    @KdfParallelism INT = NULL,
    @RevisionDate DATETIME2(7),
    @AccountRevisionDate DATETIME2(7),
    @MasterPasswordSalt NVARCHAR(256) = NULL,
    @UserKeyId VARCHAR(32) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [MasterPassword] = @MasterPassword,
        [MasterPasswordHint] = @MasterPasswordHint,
        [Key] = @Key,
        [Kdf] = @Kdf,
        [KdfIterations] = @KdfIterations,
        [KdfMemory] = @KdfMemory,
        [KdfParallelism] = @KdfParallelism,
        [RevisionDate] = @RevisionDate,
        [AccountRevisionDate] = @AccountRevisionDate,
        [MasterPasswordSalt] = @MasterPasswordSalt,
        -- Fill-only: setting a master password re-wraps the existing user key rather than replacing
        -- it, so it may record a key id the account does not have yet but must never rename one it
        -- already has.
        [UserKeyId] = COALESCE([UserKeyId], @UserKeyId)
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_UpdateMasterPasswordUnlockData]
    @Id UNIQUEIDENTIFIER,
    @Kdf TINYINT,
    @KdfIterations INT,
    @KdfMemory INT,
    @KdfParallelism INT,
    @MasterPasswordSalt NVARCHAR(256) = NULL,
    @Key VARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @AccountRevisionDate DATETIME2(7),
    @UserKeyId VARCHAR(32) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [Key] = @Key,
        [Kdf] = @Kdf,
        [KdfIterations] = @KdfIterations,
        [KdfMemory] = @KdfMemory,
        [KdfParallelism] = @KdfParallelism,
        [MasterPasswordSalt] = @MasterPasswordSalt,
        [RevisionDate] = @RevisionDate,
        [AccountRevisionDate] = @AccountRevisionDate,
        [UserKeyId] = @UserKeyId
    WHERE
        [Id] = @Id
END
GO

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
