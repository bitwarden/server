-- Denormalizes the target's name/method and computes HasActiveJob, so list and header reads take one round trip.
-- A timed-out job stays active until the timeout sweep records it.
CREATE VIEW [dbo].[PamRotationConfigDetailsView]
AS
SELECT
    C.[Id],
    C.[OrganizationId],
    C.[CipherId],
    C.[TargetSystemId],
    C.[ScheduleCron],
    C.[RotateOnAccessEnd],
    C.[NextRotationDate],
    C.[Enabled],
    C.[LastRotationDate],
    C.[CreationDate],
    C.[RevisionDate],
    C.[SealedData],
    C.[WrappedKey],
    T.[Name] AS [TargetSystemName],
    T.[Method] AS [TargetSystemMethod],
    CASE
        WHEN EXISTS (
            SELECT
                1
            FROM
                [dbo].[PamRotationJob] J
            WHERE
                J.[RotationConfigId] = C.[Id]
                AND J.[Action] IN (0,1) -- None, Claimed
                AND NOT EXISTS (
                    SELECT
                        1
                    FROM
                        [dbo].[PamRotationJobTimeoutSweep] S
                    WHERE
                        S.[RotationJobId] = J.[Id]
                )
        ) THEN 1
        ELSE 0
    END AS [HasActiveJob]
FROM
    [dbo].[PamRotationConfig] C
INNER JOIN
    [dbo].[PamTargetSystem] T ON T.[Id] = C.[TargetSystemId]
