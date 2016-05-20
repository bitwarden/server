CREATE TABLE [dbo].[History] (
    [Id]       BIGINT           IDENTITY (1, 1) NOT NULL,
    [UserId]   UNIQUEIDENTIFIER NOT NULL,
    [CipherId] UNIQUEIDENTIFIER NOT NULL,
    [Event]    TINYINT          NOT NULL,
    [Date]     DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_CipherHistory] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_CipherHistory_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]),
    CONSTRAINT [FK_CipherHistory_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

