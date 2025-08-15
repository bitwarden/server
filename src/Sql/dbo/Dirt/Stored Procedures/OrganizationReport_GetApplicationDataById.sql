CREATE PROCEDURE [dbo].[OrganizationReport_GetApplicationDataById]
    @OrganizationId UNIQUEIDENTIFIER,
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId],
        [ApplicationData]
    FROM [dbo].[OrganizationReport]
    WHERE [OrganizationId] = @OrganizationId
    AND [Id] = @Id
END

