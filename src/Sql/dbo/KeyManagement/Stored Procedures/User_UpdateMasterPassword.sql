CREATE PROCEDURE [dbo].[User_UpdateMasterPassword]
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
