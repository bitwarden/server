CREATE PROCEDURE [dbo].[SubvaultUser_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubvaultUserView]
    WHERE
        [Id] = @Id
END