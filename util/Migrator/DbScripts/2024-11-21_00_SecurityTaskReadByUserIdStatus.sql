-- Security Task Read By UserId Status

-- Create SecurityTaskStatusArray Type
IF NOT EXISTS (
    SELECT
        *
    FROM
        sys.types
    WHERE
        [Name] = 'SecurityTaskStatusArray' AND
        is_user_defined = 1
)
CREATE TYPE [dbo].[SecurityTaskStatusArray] AS TABLE (
    [Value] TINYINT NOT NULL
);
GO

-- Stored Procedure: ReadByUserIdStatus
CREATE OR ALTER PROCEDURE [dbo].[SecurityTask_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status AS [dbo].[SecurityTaskStatusArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        ST.*
    FROM
        [dbo].[SecurityTaskView] ST
    INNER JOIN
        [dbo].[OrganizationUserView] OU ON OU.[OrganizationId] = ST.[OrganizationId]
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = ST.[OrganizationId]
    LEFT JOIN
        [dbo].[CipherView] C ON C.[Id] = ST.[CipherId]
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON CC.[CipherId] = C.[Id] AND C.[Id] IS NOT NULL
    LEFT JOIN
        [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id] AND C.[Id] IS NOT NULL
    LEFT JOIN
        [dbo].[GroupUser] GU ON GU.[OrganizationUserId] = OU.[Id] AND CU.[CollectionId] IS NULL AND C.[Id] IS NOT NULL
    LEFT JOIN
        [dbo].[CollectionGroup] CG ON CG.[GroupId] = GU.[GroupId] AND CG.[CollectionId] = CC.[CollectionId]
    WHERE
        OU.[UserId] = @UserId
        AND OU.[Status] = 2 -- Ensure user is confirmed
        AND O.[Enabled] = 1
        AND (
            ST.[CipherId] IS NULL
            OR (
                C.[Id] IS NOT NULL
                AND (
                    CU.[Manage] = 1
                    OR CG.[Manage] = 1
                )
            )
        )
        AND (
            NOT EXISTS (SELECT 1 FROM @Status)
            OR ST.[Status] IN (SELECT [Value] FROM @Status)
        )
    ORDER BY ST.[CreationDate] DESC
END
GO
