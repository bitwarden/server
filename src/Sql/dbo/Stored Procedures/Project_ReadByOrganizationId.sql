CREATE PROCEDURE [dbo].[Project_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        Id,
        OrganizationId, 
        Name,
        CreationDate,
        RevisionDate,
        DeletedDate
    FROM
        [dbo].[Project] P
    WHERE
        [OrganizationId] = @OrganizationId
END