CREATE PROCEDURE [dbo].[PamDaemon_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @ApiKeyId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @LastHeartbeatAt DATETIME2(7) = NULL,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PamDaemon]
    (
        [Id],
        [OrganizationId],
        [Name],
        [ApiKeyId],
        [Status],
        [LastHeartbeatAt],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @ApiKeyId,
        @Status,
        @LastHeartbeatAt,
        @CreationDate,
        @RevisionDate
    )
END
