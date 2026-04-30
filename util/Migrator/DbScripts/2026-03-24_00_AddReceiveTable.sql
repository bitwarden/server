-- Add Receive table
IF OBJECT_ID('[dbo].[Receive]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Receive]
    (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [UserId]         UNIQUEIDENTIFIER NOT NULL,
        [Data]           VARCHAR(MAX)     NOT NULL,
        [UserKeyWrappedSharedContentEncryptionKey] VARCHAR(MAX) NOT NULL,
        [UserKeyWrappedPrivateKey]                 VARCHAR(MAX) NOT NULL,
        [ScekWrappedPublicKey]                     VARCHAR(MAX) NOT NULL,
        [Secret]         NVARCHAR(300)    NOT NULL,
        [UploadCount]    INT              NOT NULL,
        [CreationDate]   DATETIME2(7)     NOT NULL,
        [RevisionDate]   DATETIME2(7)     NOT NULL,
        [ExpirationDate] DATETIME2(7)     NULL,
        CONSTRAINT [PK_Receive] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Receive_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
    );

    CREATE NONCLUSTERED INDEX [IX_Receive_ExpirationDate]
        ON [dbo].[Receive] ([ExpirationDate] ASC);

    CREATE NONCLUSTERED INDEX [IX_Receive_UserId]
        ON [dbo].[Receive] ([UserId] ASC);
END
GO
