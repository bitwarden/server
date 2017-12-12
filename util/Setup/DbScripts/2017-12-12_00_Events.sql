IF OBJECT_ID('[dbo].[Event]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Event] (
        [Id]                    UNIQUEIDENTIFIER NOT NULL,
        [Type]                  INT              NOT NULL,
        [UserId]                UNIQUEIDENTIFIER NULL,
        [OrganizationId]        UNIQUEIDENTIFIER NULL,
        [CipherId]              UNIQUEIDENTIFIER NULL,
        [CollectionId]          UNIQUEIDENTIFIER NULL,
        [GroupId]               UNIQUEIDENTIFIER NULL,
        [OrganizationUserId]    UNIQUEIDENTIFIER NULL,
        [Date]                  DATETIME2 (7)    NOT NULL,
        CONSTRAINT [PK_Event] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_Event_Date]
        ON [dbo].[Event]([Date] ASC);
END
GO

IF OBJECT_ID('[dbo].[Event_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Event_Create]
END
GO

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
GO
