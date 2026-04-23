CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT = NULL,
    @Status TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND (@Type IS NULL OR [Type] = @Type)
        AND (@Status IS NULL OR [Status] = @Status)
END
