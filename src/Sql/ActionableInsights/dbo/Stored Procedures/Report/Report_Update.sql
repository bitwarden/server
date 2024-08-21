CREATE PROCEDURE [dbo].[Report_Update]
    @Id UNIQUEIDENTIFIER,
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

    UPDATE
        [dbo].[Report]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [GroupId] = @GroupId,
        [Type] = @Type,
        [Parameters] = @Parameters,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
