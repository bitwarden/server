CREATE TABLE [dbo].[Receive]
(
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [Data]           VARCHAR(MAX)     NOT NULL,  -- can contain multiple files
    [Key]            VARCHAR(MAX)     NOT NULL,
    [Secret]         NVARCHAR(300)    NULL,      -- in lieu of password
    [MaxUploadCount] INT              NULL,
    [UploadCount]    INT              NOT NULL,
    [CreationDate]   DATETIME2(7)     NOT NULL,
    [RevisionDate]   DATETIME2(7)     NOT NULL,
    [ExpirationDate] DATETIME2(7)     NULL,
    CONSTRAINT [PK_Receive] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Receive_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
);

GO
CREATE NONCLUSTERED INDEX [IX_Receive_DeletionDate]
    ON [dbo].[Receive] ([DeletionDate] ASC);

