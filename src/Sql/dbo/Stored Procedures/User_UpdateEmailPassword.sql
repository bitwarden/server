CREATE PROCEDURE [dbo].[User_UpdateEmailPassword]
    @Id UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @MasterPassword NVARCHAR(300),
    @SecurityStamp NVARCHAR(50)
AS
BEGIN
    UPDATE
        [dbo].[User]
    SET
        [Email] = @Email,
        [MasterPassword] = @MasterPassword,
        [SecurityStamp] = @SecurityStamp
    WHERE
        [Id] = @Id
END
