CREATE PROCEDURE [dbo].[Site_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SELECT
        *
    FROM
        [dbo].[SiteView]
    WHERE
        [Id] = @Id
END
