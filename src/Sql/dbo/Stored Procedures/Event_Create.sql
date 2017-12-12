CREATE PROCEDURE [dbo].[Event_Create]
    @Id UNIQUEIDENTIFIER,
    @Type INT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @Date DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Event]
    (
        [Id],
        [Type],
        [UserId],
        [OrganizationId],
        [CipherId],
        [CollectionId],
        [GroupId],
        [OrganizationUserId],
        [Date]
    )
    VALUES
    (
        @Id,
        @Type,
        @UserId,
        @OrganizationId,
        @CipherId,
        @CollectionId,
        @GroupId,
        @OrganizationUserId,
        @Date
    )
END
