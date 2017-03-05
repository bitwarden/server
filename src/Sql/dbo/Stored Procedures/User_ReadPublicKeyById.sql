CREATE PROCEDURE [dbo].[User_ReadPublicKeyById]
    @Id NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [PublicKey]
    FROM
        [dbo].[User]
    WHERE
        [Id] = @Id
END