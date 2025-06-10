CREATE PROCEDURE [dbo].[OrganizationApplication_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @Id IS NULL
       THROW 50000, 'Id cannot be null', 1;

    SELECT
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[OrganizationApplication]
    WHERE [Id] = @Id;
