CREATE PROCEDURE [dbo].[TwoFactorRememberToken_Update]
    @Id             UNIQUEIDENTIFIER,
    @Stamp          NVARCHAR(50),
    @RevisionDate   DATETIME2(7),
    @ExpirationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- CreationDate is deliberately not updated: it records when the device was first remembered.
    UPDATE
        [dbo].[TwoFactorRememberToken]
    SET
        [Stamp]          = @Stamp,
        [RevisionDate]   = @RevisionDate,
        [ExpirationDate] = @ExpirationDate
    WHERE
        [Id] = @Id

    -- Zero when the row no longer exists, e.g. the expiry sweep removed it after the caller read it.
    SELECT @@ROWCOUNT
END
