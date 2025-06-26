CREATE PROCEDURE [dbo].[OrganizationApplication_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ReportKey NVARCHAR(MAX)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationApplication]
    (
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate],
        [ReportKey]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Applications,
        @CreationDate,
        @RevisionDate,
        @ReportKey
    );
