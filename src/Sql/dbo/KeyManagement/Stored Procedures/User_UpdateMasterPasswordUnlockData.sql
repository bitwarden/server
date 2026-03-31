CREATE PROCEDURE [dbo].[User_UpdateMasterPasswordUnlockData]
    @Id UNIQUEIDENTIFIER,
    @Kdf TINYINT,
    @KdfIterations INT,
    @KdfMemory INT,
    @KdfParallelism INT,
    @Key VARCHAR(MAX),
    @MasterPasswordSalt NVARCHAR(256) = NULL,
    @RevisionDate DATETIME2(7),
    @AccountRevisionDate DATETIME2(7)
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
        [AccountRevisionDate] = @AccountRevisionDate
    WHERE
        [Id] = @Id
END
