CREATE PROCEDURE [dbo].[PamAccessConnector_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamAccessConnectorView]
    WHERE
        [Id] = @Id
END
