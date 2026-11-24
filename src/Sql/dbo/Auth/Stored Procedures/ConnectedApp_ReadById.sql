CREATE PROCEDURE [dbo].[ConnectedApp_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ConnectedAppView]
    WHERE
        [Id] = @Id
END
