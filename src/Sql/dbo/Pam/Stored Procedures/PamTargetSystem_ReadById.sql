CREATE PROCEDURE [dbo].[PamTargetSystem_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamTargetSystemView]
    WHERE
        [Id] = @Id
END
