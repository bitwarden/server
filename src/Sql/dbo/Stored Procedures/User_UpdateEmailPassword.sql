CREATE PROCEDURE [dbo].[User_UpdateEmailPassword]
    @Id UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @EmailVerified BIT,
    @MasterPassword NVARCHAR(300),
    @SecurityStamp NVARCHAR(50),
    @PrivateKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[User]
    SET
        [Email] = @Email,
        [EmailVerified] = @EmailVerified,
        [MasterPassword] = @MasterPassword,
        [SecurityStamp] = @SecurityStamp,
        [PrivateKey] = @PrivateKey,
        [RevisionDate] = @RevisionDate,
        [AccountRevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END