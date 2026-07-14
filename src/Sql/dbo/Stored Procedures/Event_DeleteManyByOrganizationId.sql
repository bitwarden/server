CREATE PROCEDURE [dbo].[Event_DeleteManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Total INT = 0
    DECLARE @BatchSize INT = 1000

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Event]
        WHERE
            [OrganizationId] = @OrganizationId

        SET @BatchSize = @@ROWCOUNT
        SET @Total = @Total + @BatchSize
    END

    SELECT @Total
END
