CREATE PROCEDURE [dbo].[RiskInsightCriticalApplication_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    UPDATE [dbo].[RiskInsightCriticalApplication]
    SET
        [OrganizationId] = @OrganizationId,
        [Applications] = @Applications,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id;
