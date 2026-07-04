CREATE PROCEDURE [dbo].[AccessRule_ReadDetailsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [OrganizationId] = @OrganizationId

    SELECT
        [AccessRuleId],
        [Id] AS [CollectionId]
    FROM [dbo].[Collection]
    WHERE [OrganizationId] = @OrganizationId
        AND [AccessRuleId] IS NOT NULL
END
