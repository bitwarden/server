CREATE PROCEDURE [dbo].[Report_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(MAX),
    @GroupId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Parameters NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Report]
    (
        [Id],
        [OrganizationId],
        [Name],
        [GroupId],
        [Type],
        [Parameters],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @GroupId,
        @Type,
        @Parameters,
        @CreationDate,
        @RevisionDate
    )
END
