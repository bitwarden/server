CREATE TABLE [dbo].[EmergencyAccess]
(
	[Id]                    UNIQUEIDENTIFIER NOT NULL,
    [GrantorId]             UNIQUEIDENTIFIER NOT NULL,
    [GranteeId]             UNIQUEIDENTIFIER NULL,
    [Email]                 NVARCHAR (256)   NULL,
    [KeyEncrypted]          VARCHAR (MAX)    NULL,
    [WaitTimeDays]          SMALLINT         NULL,
    [Type]                  TINYINT          NOT NULL,
    [Status]                TINYINT          NOT NULL,
    [RecoveryInitiatedDate] DATETIME2 (7)    NULL,
    [LastNotificationDate]  DATETIME2 (7)    NULL,
    [CreationDate]          DATETIME2 (7)    NOT NULL,
    [RevisionDate]          DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_EmergencyAccess] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_EmergencyAccess_GrantorId] FOREIGN KEY ([GrantorId]) REFERENCES [dbo].[User] ([Id]),
    CONSTRAINT [FK_EmergencyAccess_GranteeId] FOREIGN KEY ([GranteeId]) REFERENCES [dbo].[User] ([Id])
)
