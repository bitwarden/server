CREATE PROCEDURE [dbo].[User_ReadKdfByEmail]
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Kdf],
        [KdfIterations]
    FROM
        [dbo].[User]
    WHERE
        [Email] = @Email
END