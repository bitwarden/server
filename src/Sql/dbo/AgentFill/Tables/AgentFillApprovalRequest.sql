CREATE TABLE [dbo].[AgentFillApprovalRequest]
(
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [UserId]           UNIQUEIDENTIFIER NOT NULL,
    [RequestDeviceId]  UNIQUEIDENTIFIER NOT NULL,
    [SealedRequest]    VARCHAR(MAX)     NOT NULL,
    [SealedResponse]   VARCHAR(MAX)     NULL,
    [ResponseDeviceId] UNIQUEIDENTIFIER NULL,
    [CreationDate]     DATETIME2(7)     NOT NULL,
    [ExpirationDate]   DATETIME2(7)     NOT NULL,
    [ResponseDate]     DATETIME2(7)     NULL,
    CONSTRAINT [PK_AgentFillApprovalRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AgentFillApprovalRequest_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AgentFillApprovalRequest_Device] FOREIGN KEY ([RequestDeviceId]) REFERENCES [dbo].[Device] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_AgentFillApprovalRequest_ExpirationDate]
    ON [dbo].[AgentFillApprovalRequest]([ExpirationDate] ASC);
GO
