CREATE PROCEDURE [dbo].[OrganizationIntegration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @Configuration VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationIntegration]
    SET
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Configuration] = @Configuration,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
