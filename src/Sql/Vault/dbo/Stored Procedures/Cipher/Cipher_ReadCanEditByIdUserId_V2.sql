﻿CREATE PROCEDURE [dbo].[Cipher_ReadCanEditByIdUserId_V2]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @CanEdit BIT

    ;WITH [CTE] AS (
        SELECT
            CASE
                WHEN C.[UserId] IS NOT NULL OR OU.[AccessAll] = 1 OR CU.[ReadOnly] = 0 OR G.[AccessAll] = 1 OR CG.[ReadOnly] = 0 THEN 1
                ELSE 0
            END [Edit]
        FROM
            [dbo].[Cipher] C
        LEFT JOIN
            [dbo].[Organization] O ON C.[UserId] IS NULL AND O.[Id] = C.[OrganizationId]
        LEFT JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[CollectionCipher] CC ON C.[UserId] IS NULL AND OU.[AccessAll] = 0 AND CC.[CipherId] = C.[Id]
        LEFT JOIN
            [dbo].[CollectionUser] CU ON CU.[CollectionId] = CC.[CollectionId] AND CU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[GroupUser] GU ON C.[UserId] IS NULL AND CU.[CollectionId] IS NULL AND OU.[AccessAll] = 0 AND GU.[OrganizationUserId] = OU.[Id]
        LEFT JOIN
            [dbo].[Group] G ON G.[Id] = GU.[GroupId]
        LEFT JOIN
            [dbo].[CollectionGroup] CG ON G.[AccessAll] = 0 AND CG.[CollectionId] = CC.[CollectionId] AND CG.[GroupId] = GU.[GroupId]
        WHERE
            C.Id = @Id
            AND (
                C.[UserId] = @UserId
                OR (
                    C.[UserId] IS NULL
                    AND OU.[Status] = 2 -- 2 = Confirmed
                    AND O.[Enabled] = 1
                    AND (
                        OU.[AccessAll] = 1
                        OR CU.[CollectionId] IS NOT NULL
                        OR G.[AccessAll] = 1
                        OR CG.[CollectionId] IS NOT NULL
                    )
                )
            )
    )
    SELECT
        @CanEdit = CASE
            WHEN COUNT(1) > 0 THEN 1
            ELSE 0
        END
    FROM
        [CTE]
    WHERE
        [Edit] = 1

    SELECT @CanEdit
END
