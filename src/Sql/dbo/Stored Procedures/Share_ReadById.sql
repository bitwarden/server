CREATE PROCEDURE [dbo].[Share_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ShareView]
    WHERE
        [Id] = @Id
END