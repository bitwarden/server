CREATE PROCEDURE [dbo].[OrganizationApplication_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Applications NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationApplication]
    (
        [Id],
        [OrganizationId],
        [Applications],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
        (
        @Id,
        @OrganizationId,
        @Applications,
        @CreationDate,
        @RevisionDate
        );
