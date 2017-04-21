CREATE TABLE [dbo].[OrganizationUser] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]        UNIQUEIDENTIFIER NOT NULL,
    [UserId]                UNIQUEIDENTIFIER NULL,
    [Email]                 NVARCHAR (50)    NULL,
    [Key]                   VARCHAR (MAX)    NULL,
    [Status]                TINYINT          NOT NULL,
    [Type]                  TINYINT          NOT NULL,
    [AccessAllSubvaults]    BIT              NOT NULL,
    [CreationDate]          DATETIME2 (7)    NOT NULL,
    [RevisionDate]          DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_OrganizationUser] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationUser_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrganizationUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

