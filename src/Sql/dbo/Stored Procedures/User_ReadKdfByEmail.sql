CREATE PROCEDURE [dbo].[User_ReadKdfByEmail]
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Kdf],
        [KdfIterations],
        [KdfMemory],
        [KdfParallelism],
        [MasterPasswordSalt] AS [Salt]
    FROM
        [dbo].[User]
    WHERE
        [Email] = @Email
END