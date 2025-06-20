CREATE PROCEDURE [dbo].[OrganizationApplication_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[OrganizationApplicationView]
    WHERE [OrganizationId] = @OrganizationId;
