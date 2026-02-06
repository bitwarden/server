CREATE PROCEDURE [dbo].[SsoConfig_Update]
    @Id BIGINT,
    @Enabled BIT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Data NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[SsoConfig]
    SET
        [Enabled] = @Enabled,
        [OrganizationId] = @OrganizationId,
        [Data] = @Data,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
