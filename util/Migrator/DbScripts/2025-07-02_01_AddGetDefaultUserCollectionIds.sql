CREATE PROCEDURE dbo.Collection_DefaultUserIds_ReadByOrganizationId
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id]
    FROM dbo.Collection
    WHERE [OrganizationId] = @OrganizationId
      AND [Type] = CAST(1 AS smallint);  -- DefaultUserCollection = 1
END
GO
