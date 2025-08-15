CREATE PROCEDURE [dbo].[OrganizationReport_UpdateApplicationData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ApplicationData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[OrganizationReport]
    SET
        [ApplicationData] = @ApplicationData,
        [RevisionDate] = GETUTCDATE()
    WHERE [Id] = @Id
      AND [OrganizationId] = @OrganizationId;
END
