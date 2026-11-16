CREATE TABLE [dbo].[AgentConnection] (
    [Id]              UNIQUEIDENTIFIER NOT NULL,
    [UserId]          UNIQUEIDENTIFIER NOT NULL,
    [PartnerClientId] NVARCHAR(200)    NOT NULL,
    [Name]            NVARCHAR(200)    NOT NULL,
    [Key]             VARCHAR(MAX)     NOT NULL,
    [CreationDate]    DATETIME2(7)     NOT NULL,
    [RevisionDate]    DATETIME2(7)     NOT NULL,
    [RevokedDate]     DATETIME2(7)     NULL,
    CONSTRAINT [PK_AgentConnection] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AgentConnection_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);


GO
CREATE NONCLUSTERED INDEX [IX_AgentConnection_UserId]
    ON [dbo].[AgentConnection]([UserId] ASC);
