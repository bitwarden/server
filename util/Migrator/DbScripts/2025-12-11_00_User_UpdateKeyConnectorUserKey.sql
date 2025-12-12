IF OBJECT_ID('[dbo].[User_UpdateKeyConnectorUserKey]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_UpdateKeyConnectorUserKey]
END
GO

CREATE PROCEDURE [dbo].[User_UpdateKeyConnectorUserKey]
    @Id UNIQUEIDENTIFIER,
    @Key NVARCHAR(MAX),
    @Kdf TINYINT,
    @KdfIterations INT,
    @KdfMemory INT,
    @KdfParallelism INT,
    @UsesKeyConnector BIT,
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
    [UsesKeyConnector] = @UsesKeyConnector,
    [RevisionDate] = @RevisionDate,
    [AccountRevisionDate] = @AccountRevisionDate
WHERE
    [Id] = @Id
END
GO
