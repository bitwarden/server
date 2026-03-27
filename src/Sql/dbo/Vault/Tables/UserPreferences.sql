CREATE TABLE [dbo].[UserPreferences] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Data]         VARCHAR (MAX)    NOT NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_UserPreferences] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserPreferences_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_UserPreferences_UserId]
    ON [dbo].[UserPreferences]([UserId] ASC);
