CREATE TABLE [dbo].[WebAuthnCredential] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [UserId]            UNIQUEIDENTIFIER NOT NULL,
    [Name]              NVARCHAR (50)    NOT NULL,
    [PublicKey]         VARCHAR (256)    NOT NULL,
    [DescriptorId]      VARCHAR (256)    NOT NULL,
    [Counter]           INT              NOT NULL,
    [Type]              VARCHAR (20)     NULL,
    [AaGuid]            UNIQUEIDENTIFIER NOT NULL,
    [UserKey]           VARCHAR (MAX)    NULL,
    [CreationDate]      DATETIME2 (7)    NOT NULL,
    [RevisionDate]      DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_WebAuthnCredential] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_WebAuthnCredential_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_WebAuthnCredential_UserId]
    ON [dbo].[WebAuthnCredential]([UserId] ASC);

