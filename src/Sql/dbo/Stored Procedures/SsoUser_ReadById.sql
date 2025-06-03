CREATE PROCEDURE [dbo].[SsoUser_ReadById]
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SsoUserView]
    WHERE
        [Id] = @Id
END