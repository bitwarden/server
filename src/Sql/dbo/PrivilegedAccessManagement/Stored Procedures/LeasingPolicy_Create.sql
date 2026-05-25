CREATE PROCEDURE [dbo].[LeasingPolicy_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(256),
    @Description NVARCHAR(MAX) = NULL,
    @Policy NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[LeasingPolicy]
    (
        [Id],
        [OrganizationId],
        [Name],
        [Description],
        [Policy],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @Description,
        @Policy,
        @CreationDate,
        @RevisionDate
    )
END
