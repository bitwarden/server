CREATE PROCEDURE [dbo].[AgentConnection_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @PartnerClientId NVARCHAR(200),
    @Name NVARCHAR(200),
    @Key VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @RevokedDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AgentConnection]
    (
        [Id],
        [UserId],
        [PartnerClientId],
        [Name],
        [Key],
        [CreationDate],
        [RevisionDate],
        [RevokedDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @PartnerClientId,
        @Name,
        @Key,
        @CreationDate,
        @RevisionDate,
        @RevokedDate
    )

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
