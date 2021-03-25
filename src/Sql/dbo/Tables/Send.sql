CREATE TABLE [dbo].[Send] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [OrganizationId] UNIQUEIDENTIFIER NULL,
    [Type]           TINYINT          NOT NULL,
    [Data]           VARCHAR(MAX)     NOT NULL,
    [Key]            VARCHAR (MAX)    NOT NULL,
    [Password]       NVARCHAR (300)   NULL,
    [MaxAccessCount] INT              NULL,
    [AccessCount]    INT              NOT NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    [RevisionDate]   DATETIME2 (7)    NOT NULL,
    [ExpirationDate] DATETIME2 (7)    NULL,
    [DeletionDate]   DATETIME2 (7)    NOT NULL,
    [Disabled]       BIT              NOT NULL,
    [HideEmail]      BIT              NULL,
    CONSTRAINT [PK_Send] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Send_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_Send_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_Send_UserId_OrganizationId]
    ON [dbo].[Send]([UserId] ASC, [OrganizationId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_Send_DeletionDate]
    ON [dbo].[Send]([DeletionDate] ASC);

