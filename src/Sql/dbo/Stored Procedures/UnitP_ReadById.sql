CREATE PROCEDURE [dbo].[UnitP_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UnitPView]
    WHERE
        [Id] = @Id
END
