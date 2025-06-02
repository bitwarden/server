CREATE TABLE [dbo].[UserSigningKey] (
    [Id]                        UNIQUEIDENTIFIER NOT NULL,
    [UserId]                    UNIQUEIDENTIFIER,
    [KeyType]                   TINYINT NOT NULL,
    [VerifyingKey]              VARCHAR(MAX) NOT NULL,
    [SigningKey]                VARCHAR(MAX) NOT NULL,
    [CreationDate]              DATETIME2 (7) NOT NULL,
    [RevisionDate]              DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_UserSigningKeys] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserSigningKeys_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
);