CREATE PROCEDURE [dbo].[Organization_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [Id] = @Id
END