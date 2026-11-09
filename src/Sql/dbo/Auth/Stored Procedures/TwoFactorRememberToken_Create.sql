CREATE PROCEDURE [dbo].[TwoFactorRememberToken_Create]
    @Id             UNIQUEIDENTIFIER,
    @UserId         UNIQUEIDENTIFIER,
    @DeviceId       UNIQUEIDENTIFIER,
    @Stamp          NVARCHAR(50),
    @CreationDate   DATETIME2(7),
    @RevisionDate   DATETIME2(7),
    @ExpirationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[TwoFactorRememberToken]
    (
        [Id],
        [UserId],
        [DeviceId],
        [Stamp],
        [CreationDate],
        [RevisionDate],
        [ExpirationDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @DeviceId,
        @Stamp,
        @CreationDate,
        @RevisionDate,
        @ExpirationDate
    )
END
