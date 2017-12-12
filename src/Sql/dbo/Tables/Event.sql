CREATE TABLE [dbo].[Event] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL,
    [Type]                  INT              NOT NULL,
    [UserId]                UNIQUEIDENTIFIER NULL,
    [OrganizationId]        UNIQUEIDENTIFIER NULL,
    [CipherId]              UNIQUEIDENTIFIER NULL,
    [CollectionId]          UNIQUEIDENTIFIER NULL,
    [GroupId]               UNIQUEIDENTIFIER NULL,
    [OrganizationUserId]    UNIQUEIDENTIFIER NULL,
    [ActingUserId]          UNIQUEIDENTIFIER NULL,
    [Date]                  DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Event] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Event_DateOrganizationIdUserId]
    ON [dbo].[Event]([Date] ASC, [OrganizationId] ASC, [UserId] ASC);

