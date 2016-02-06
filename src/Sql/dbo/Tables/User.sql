CREATE TABLE [dbo].[User] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [Name]               NVARCHAR (50)    NULL,
    [Email]              NVARCHAR (50)    NOT NULL,
    [MasterPassword]     NVARCHAR (300)   NOT NULL,
    [MasterPasswordHint] NVARCHAR (50)    NULL,
    [Culture]            NVARCHAR (10)    NOT NULL,
    [SecurityStamp]      NVARCHAR (50)    NOT NULL,
    [TwoFactorEnabled]   BIT              NOT NULL,
    [TwoFactorProvider]  TINYINT          NULL,
    [AuthenticatorKey]   NVARCHAR (50)    NULL,
    [CreationDate]       DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_User_Email]
    ON [dbo].[User]([Email] ASC);

