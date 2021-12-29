CREATE TABLE [dbo].[AuthRequest] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [UserId]            UNIQUEIDENTIFIER NOT NULL,
    [Type]              SMALLINT         NOT NULL,
    [RequestDeviceId]   UNIQUEIDENTIFIER NOT NULL,
    [ResponseDeviceId]  UNIQUEIDENTIFIER NULL,
    [PublicKey]         VARCHAR(MAX)     NOT NULL,
    [Key]               VARCHAR(MAX)     NULL,
    [CreationDate]      DATETIME2 (7)    NOT NULL,
    [ResponseDate]      DATETIME2 (7)    NULL,
    CONSTRAINT [PK_AuthRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AuthRequest_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
    CONSTRAINT [FK_AuthRequest_RequestDevice] FOREIGN KEY ([RequestDeviceId]) REFERENCES [dbo].[Device] ([Id]),
    CONSTRAINT [FK_AuthRequest_ResponseDevice] FOREIGN KEY ([ResponseDeviceId]) REFERENCES [dbo].[Device] ([Id])
);


GO
