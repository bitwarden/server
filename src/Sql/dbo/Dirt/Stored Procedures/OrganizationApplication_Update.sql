CREATE PROCEDURE [dbo].[OrganizationApplication_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    
    UPDATE [dbo].[OrganizationApplication]
    SET
        [OrganizationId] = @OrganizationId,
        [Applications] = @Applications,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id;
    