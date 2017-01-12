CREATE PROCEDURE [dbo].[User_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Id] = @Id
END