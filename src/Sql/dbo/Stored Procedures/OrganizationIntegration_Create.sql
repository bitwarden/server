CREATE PROCEDURE [dbo].[OrganizationIntegration_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @Configuration VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationIntegration]
        (
        [Id],
        [OrganizationId],
        [Type],
        [Configuration],
        [CreationDate],
        [RevisionDate]
        )
    VALUES
        (
            @Id,
            @OrganizationId,
            @Type,
            @Configuration,
            @CreationDate,
            @RevisionDate
        )
END
