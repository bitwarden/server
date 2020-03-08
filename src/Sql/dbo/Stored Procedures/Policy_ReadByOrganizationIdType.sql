CREATE PROCEDURE [dbo].[Policy_ReadByOrganizationIdType]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[PolicyView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [Type] = @Type
END