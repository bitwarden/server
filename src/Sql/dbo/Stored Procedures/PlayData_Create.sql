CREATE PROCEDURE [dbo].[PlayData_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @PlayId NVARCHAR(256),
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PlayData]
    (
        [Id],
        [PlayId],
        [UserId],
        [OrganizationId],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @PlayId,
        @UserId,
        @OrganizationId,
        @CreationDate
    )
END
