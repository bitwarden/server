CREATE PROCEDURE [dbo].[SubvaultUser_Create]
    @Id UNIQUEIDENTIFIER,
    @SubvaultId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @ReadOnly BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SubvaultUser]
    (
        [Id],
        [SubvaultId],
        [OrganizationUserId],
        [ReadOnly],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @SubvaultId,
        @OrganizationUserId,
        @ReadOnly,
        @CreationDate,
        @RevisionDate
    )
END