CREATE TABLE [dbo].[LeaseDecision] (
    [Id]                    UNIQUEIDENTIFIER    NOT NULL,
    [LeaseRequestId]        UNIQUEIDENTIFIER    NOT NULL,
    [DeciderKind]           TINYINT             NOT NULL,
    [ApproverId]            UNIQUEIDENTIFIER    NULL,
    [PolicyKind]            NVARCHAR(50)        NULL,
    [Decision]              TINYINT             NOT NULL,
    [Comment]               NVARCHAR(MAX)       NULL,
    [EvaluationContext]     NVARCHAR(MAX)       NULL,
    [CreationDate]          DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_LeaseDecision] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_LeaseDecision_LeaseRequest] FOREIGN KEY ([LeaseRequestId]) REFERENCES [dbo].[LeaseRequest] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_LeaseDecision_LeaseRequestId]
    ON [dbo].[LeaseDecision] ([LeaseRequestId] ASC);
GO
