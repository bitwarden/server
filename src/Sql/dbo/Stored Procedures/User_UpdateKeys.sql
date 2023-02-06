CREATE PROCEDURE [dbo].[User_UpdateKeys]
    @Id UNIQUEIDENTIFIER,
    @SecurityStamp NVARCHAR(50),
    @Key NVARCHAR(MAX),
    @PrivateKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7),
    @AccountRevisionDate DATETIME2(7) = NULL,
    @LastKeyRotationDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [SecurityStamp] = @SecurityStamp,
        [Key] = @Key,
        [PrivateKey] = @PrivateKey,
        [RevisionDate] = @RevisionDate,
        [AccountRevisionDate] = ISNULL(@AccountRevisionDate, @RevisionDate),
        [LastKeyRotationDate] = @LastKeyRotationDate
    WHERE
        [Id] = @Id
END