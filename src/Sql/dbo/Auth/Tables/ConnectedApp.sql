CREATE TABLE [dbo].[ConnectedApp] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [UserId]          UNIQUEIDENTIFIER NOT NULL,
    [PartnerClientId] NVARCHAR(200)    NOT NULL,
    [Name]            NVARCHAR(200)    NOT NULL,
    [Key]             VARCHAR(MAX)     NOT NULL,
    [CreationDate]    DATETIME2(7)     NOT NULL,
    [RevisionDate]    DATETIME2(7)     NOT NULL,
    [RevokedDate]     DATETIME2(7)     NULL,
    CONSTRAINT [PK_ConnectedApp] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ConnectedApp_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_ConnectedApp_UserId]
    ON [dbo].[ConnectedApp]([UserId] ASC);
