CREATE TABLE [dbo].[SubvaultUser] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [SubvaultId]   UNIQUEIDENTIFIER NOT NULL,
    [UserId]       UNIQUEIDENTIFIER NOT NULL,
    [Key]          VARCHAR (MAX)    NOT NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
    [RevisionDate] DATETIME2 (7)    NOT NULL,
    [Admin]        BIT              NOT NULL,
    [ReadOnly]     BIT              NOT NULL,
    CONSTRAINT [PK_SubvaultUser] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SubvaultUser_Subvault] FOREIGN KEY ([SubvaultId]) REFERENCES [dbo].[Subvault] ([Id]),
    CONSTRAINT [FK_SubvaultUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

