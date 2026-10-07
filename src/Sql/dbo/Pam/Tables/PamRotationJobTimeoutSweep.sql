-- Journal of jobs PamRotationJob_TimeoutDue has already returned; a timeout is derived, not stored.
-- Makes the RotationJobTimedOut audit event and reschedule fire once per job, and the row ends the job's hold on
-- its config.
CREATE TABLE [dbo].[PamRotationJobTimeoutSweep] (
    [RotationJobId] UNIQUEIDENTIFIER    NOT NULL,
    [SweptDate]     DATETIME2(7)        NOT NULL,
    CONSTRAINT [PK_PamRotationJobTimeoutSweep] PRIMARY KEY CLUSTERED ([RotationJobId] ASC),
    CONSTRAINT [FK_PamRotationJobTimeoutSweep_PamRotationJob] FOREIGN KEY ([RotationJobId]) REFERENCES [dbo].[PamRotationJob] ([Id]) ON DELETE CASCADE
);
