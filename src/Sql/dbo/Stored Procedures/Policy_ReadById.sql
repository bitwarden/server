CREATE PROCEDURE [dbo].[Policy_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PolicyView]
    WHERE
        [Id] = @Id
END