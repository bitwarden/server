CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadManyByIds]
    @OrganizationIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT o.*
    FROM [dbo].[OrganizationView] o
    WHERE o.[Id] IN (SELECT [Id] FROM @OrganizationIds)

END
