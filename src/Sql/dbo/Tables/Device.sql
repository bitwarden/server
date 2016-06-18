CREATE TABLE [dbo].[Device] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Name]         NVARCHAR (50)    NOT NULL,
    [Type]         SMALLINT         NOT NULL,
    [PushToken]    NVARCHAR (255)   NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Device] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Device_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Device_UserId]
    ON [dbo].[Device]([UserId] ASC);

