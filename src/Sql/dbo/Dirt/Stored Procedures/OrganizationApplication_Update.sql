CREATE PROCEDURE [dbo].[OrganizationApplication_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    
    UPDATE [dbo].[OrganizationApplication]
    SET
        [OrganizationId] = @OrganizationId,
        [Applications] = @Applications,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id;
