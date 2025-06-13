CREATE PROCEDURE [dbo].[ApiKeyDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyDetailsView]
    WHERE
        [Id] = @Id
END
