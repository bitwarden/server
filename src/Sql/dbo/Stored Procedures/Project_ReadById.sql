CREATE PROCEDURE [dbo].[Project_ReadById]
    @Id UNIQUEIDENTIFIER
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
        [dbo].[Project]
    WHERE
        [Id] = @Id
END
