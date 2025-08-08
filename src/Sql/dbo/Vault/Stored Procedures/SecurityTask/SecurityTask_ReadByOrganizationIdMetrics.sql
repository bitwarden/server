CREATE PROCEDURE [dbo].[SecurityTask_ReadByOrganizationIdMetrics]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(CASE WHEN ST.[Status] = 1 THEN 1 END) AS CompletedTasksCount,
        COUNT(*) AS TotalTasksCount
    FROM
        [dbo].[SecurityTaskView] ST
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = ST.[OrganizationId]
    WHERE
        ST.[OrganizationId] = @OrganizationId
        AND O.[Enabled] = 1
END
