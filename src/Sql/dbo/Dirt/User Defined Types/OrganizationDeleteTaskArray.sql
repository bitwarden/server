CREATE TYPE [dbo].[OrganizationDeleteTaskArray] AS TABLE (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [TaskType]     TINYINT          NOT NULL,
    [CreationDate] DATETIME2(7)     NOT NULL);
