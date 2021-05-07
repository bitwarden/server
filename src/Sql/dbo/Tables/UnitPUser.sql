CREATE TABLE [dbo].[UnitPUser] (
    [Id]                            UNIQUEIDENTIFIER    NOT NULL,
    [UnitPId]                       UNIQUEIDENTIFIER    NOT NULL,
    [UserId]                        UNIQUEIDENTIFIER    NULL,
    [Email]                         NVARCHAR (256)      NULL,
    [Key]                           VARCHAR (MAX)       NULL,
    [Status]                        TINYINT             NOT NULL,
    [Type]                          TINYINT             NOT NULL,
    [CreationDate]                  DATETIME2 (7)       NOT NULL,
    [RevisionDate]                  DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_UnitPUser] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UnitPUser_UnitP] FOREIGN KEY ([UnitPId]) REFERENCES [dbo].[UnitP] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UnitPUser_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);
