CREATE TABLE [dbo].[AuthRequest] (
    [Id]                        UNIQUEIDENTIFIER NOT NULL,
    [UserId]                    UNIQUEIDENTIFIER NOT NULL,
    [Type]                      SMALLINT         NOT NULL,
    [RequestDeviceIdentifier]   NVARCHAR(50)     NOT NULL,
    [RequestDeviceType]         SMALLINT         NOT NULL,
    [RequestIpAddress]          VARCHAR(50)      NOT NULL,
    [RequestFingerprint]        VARCHAR(MAX)     NOT NULL,
    [ResponseDeviceId]          UNIQUEIDENTIFIER NULL,
    [AccessCode]                VARCHAR(25)      NOT NULL,
    [PublicKey]                 VARCHAR(MAX)     NOT NULL,
    [Key]                       VARCHAR(MAX)     NULL,
    [MasterPasswordHash]        VARCHAR(MAX)     NULL,
    [Approved]                  BIT              NULL,
    [CreationDate]              DATETIME2 (7)    NOT NULL,
    [ResponseDate]              DATETIME2 (7)    NULL,
    [AuthenticationDate]        DATETIME2 (7)    NULL,
    CONSTRAINT [PK_AuthRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AuthRequest_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
    CONSTRAINT [FK_AuthRequest_ResponseDevice] FOREIGN KEY ([ResponseDeviceId]) REFERENCES [dbo].[Device] ([Id])
);


GO
