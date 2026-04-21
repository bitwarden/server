CREATE PROCEDURE [dbo].[Event_DeleteByOrganizationIdBatch]
    @OrganizationId UNIQUEIDENTIFIER,
    @BatchSize INT
AS
BEGIN
    SET NOCOUNT ON

    DELETE TOP (@BatchSize)
    FROM
        [dbo].[Event]
    WHERE
        [OrganizationId] = @OrganizationId

    SELECT @@ROWCOUNT
END
