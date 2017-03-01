CREATE TABLE [dbo].[Favorite] (
    [UserId]   UNIQUEIDENTIFIER NOT NULL,
    [CipherId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_Favorite] PRIMARY KEY CLUSTERED ([UserId] ASC, [CipherId] ASC),
    CONSTRAINT [FK_Favorite_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher] ([Id]),
    CONSTRAINT [FK_Favorite_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

