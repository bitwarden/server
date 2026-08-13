CREATE PROCEDURE [dbo].[PlayItem_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @PlayId NVARCHAR(256),
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7),
    @ProviderId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PlayItem]
    (
        [Id],
        [PlayId],
        [UserId],
        [OrganizationId],
        [CreationDate],
        [ProviderId]
    )
    VALUES
    (
        @Id,
        @PlayId,
        @UserId,
        @OrganizationId,
        @CreationDate,
        @ProviderId
    )
END
