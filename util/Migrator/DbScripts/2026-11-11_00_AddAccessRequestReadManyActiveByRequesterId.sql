-- The caller's open and activatable requests across ciphers, for the batch access-state read.

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyActiveByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's requests across ciphers that AccessRequest_ReadActivePendingByRequesterIdCipherId and
    -- AccessRequest_ReadActiveApprovedByRequesterIdCipherId would return: open ones, and approved ones that have not
    -- produced a lease and are not extensions. Lapsed windows are excluded from both.
    SELECT
        AR.*
    FROM
        [dbo].[AccessRequest] AR
    WHERE
        AR.[RequesterId] = @RequesterId
        AND AR.[NotAfter] > @Now
        AND (
            AR.[Action] = 0 -- None (open)
            OR (
                AR.[Action] = 1 -- Approved
                AND AR.[ExtensionOfLeaseId] IS NULL
                AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])
            )
        )
END
GO
