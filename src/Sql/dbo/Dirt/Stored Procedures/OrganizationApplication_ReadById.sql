CREATE PROCEDURE [dbo].[OrganizationApplication_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[OrganizationApplicationView]
    WHERE [Id] = @Id;
