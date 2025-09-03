CREATE PROCEDURE [dbo].[SecurityTask_ReadByOrganizationIdStatus]
    @OrganizationId UNIQUEIDENTIFIER,
    @Status TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        ST.*
    FROM
        [dbo].[SecurityTaskView] ST
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = ST.[OrganizationId]
    WHERE
        ST.[OrganizationId] = @OrganizationId
        AND O.[Enabled] = 1
        AND ST.[Status] = COALESCE(@Status, ST.[Status])
    ORDER BY ST.[CreationDate] DESC
END
