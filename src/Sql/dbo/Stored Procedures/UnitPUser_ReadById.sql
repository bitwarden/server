CREATE PROCEDURE [dbo].[UnitPUser_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UnitPUserView]
    WHERE
        [Id] = @Id
END
