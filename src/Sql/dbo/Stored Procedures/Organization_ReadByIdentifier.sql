CREATE PROCEDURE [dbo].[Organization_ReadByIdentifier]
    @Identifier UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [Identifier] = @Identifier
END