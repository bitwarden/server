CREATE PROCEDURE [dbo].[OrganizationIntegration_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT,
    @Configuration VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DisabledDate DATETIME2(7) = NULL,
    @DisabledReason INT = NULL
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
        [RevisionDate] = @RevisionDate,
        [DisabledDate] = @DisabledDate,
        [DisabledReason] = @DisabledReason
    WHERE
        [Id] = @Id
END
