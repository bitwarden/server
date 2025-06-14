CREATE PROCEDURE [dbo].[OrganizationApplication_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @OrganizationId IS NULL
       THROW 50000, 'OrganizationId cannot be null', 1;

    SELECT
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[OrganizationApplication]
    WHERE [OrganizationId] = @OrganizationId;
