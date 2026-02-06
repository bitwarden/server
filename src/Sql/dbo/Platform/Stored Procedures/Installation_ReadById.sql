CREATE PROCEDURE [dbo].[Installation_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[InstallationView]
    WHERE
        [Id] = @Id
END