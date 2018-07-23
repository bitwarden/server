CREATE TABLE [dbo].[Device] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Name]         NVARCHAR (50)    NOT NULL,
    [Type]         SMALLINT         NOT NULL,
    [Identifier]   NVARCHAR (50)    NOT NULL,
    [PushToken]    NVARCHAR (255)   NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_Device] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Device_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Device_UserId_Identifier]
    ON [dbo].[Device]([UserId] ASC, [Identifier] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Device_Identifier]
    ON [dbo].[Device]([Identifier] ASC);

