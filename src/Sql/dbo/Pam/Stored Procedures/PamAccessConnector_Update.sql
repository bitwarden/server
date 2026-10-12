CREATE PROCEDURE [dbo].[PamAccessConnector_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Status TINYINT,
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- ApiKeyId and OrganizationId never change, and LastHeartbeatAt has its own conditional bump.
    UPDATE
        [dbo].[PamAccessConnector]
    SET
        [Name] = @Name,
        [Status] = @Status,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
