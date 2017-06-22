CREATE TABLE [dbo].[U2f] (
    [Id]           INT              IDENTITY (1, 1) NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [KeyHandle]    VARCHAR (MAX)    NULL,
    [Challenge]    VARCHAR (MAX)    NOT NULL,
    [AppId]        VARCHAR (50)     NOT NULL,
    [Version]      VARCHAR (20)     NOT NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_U2f] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_U2f_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

