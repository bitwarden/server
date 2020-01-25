CREATE OR REPLACE PROCEDURE vault_dbo.cipher_delete(par_ids vault_dbo.guididarray, par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_OrgId UUID;
    orgcursor NO SCROLL CURSOR FOR
    SELECT
        organizationid
        FROM "#Temp"
        WHERE organizationid IS NOT NULL
        GROUP BY organizationid;
    var_UserCiphersWithStorageCount NUMERIC(10, 0);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    CREATE TEMPORARY TABLE "#Temp"
    (id UUID NOT NULL,
        userid UUID NULL,
        organizationid UUID NULL,
        attachments NUMERIC(1, 0) NOT NULL);
    PERFORM vault_dbo.usercipherdetails(par_UserId);
    PERFORM vault_dbo.guididarray$aws$f('"par_Ids$aws$tmp"');
    INSERT INTO "par_Ids$aws$tmp"
    SELECT
        *
        FROM UNNEST(par_Ids);
    INSERT INTO "#Temp"
    SELECT
        id, userid, organizationid,
        CASE
            WHEN attachments IS NULL THEN 0
            ELSE 1
        END
        FROM vault_dbo.usercipherdetails$tmptbl
        WHERE Edit = 1 AND id IN (SELECT
            *
            FROM "par_Ids$aws$tmp");
    /* Delete ciphers */
    DELETE FROM vault_dbo.cipher
        WHERE id IN (SELECT
            id
            FROM "#Temp");
    /* Cleanup orgs */
    OPEN orgcursor;
    FETCH NEXT FROM orgcursor INTO var_OrgId;

    WHILE (CASE FOUND::INT
        WHEN 0 THEN - 1
        ELSE 0
    END) = 0 LOOP
        CALL vault_dbo.organization_updatestorage(var_OrgId);
        CALL vault_dbo.user_bumpaccountrevisiondatebyorganizationid(var_OrgId);
        FETCH NEXT FROM orgcursor INTO var_OrgId;
    END LOOP;
    CLOSE orgcursor;
    /* Cleanup user */
    SELECT
        COUNT(1)
        INTO var_UserCiphersWithStorageCount
        FROM "#Temp"
        WHERE userid IS NOT NULL AND attachments = 1;

    IF var_UserCiphersWithStorageCount > 0 THEN
        CALL vault_dbo.user_updatestorage(par_UserId);
    END IF;
    CALL vault_dbo.user_bumpaccountrevisiondate(par_UserId);
    DROP TABLE "#Temp";
    /*

    DROP TABLE IF EXISTS "#Temp";
    */
    /*

    Temporary table must be removed before end of the function.
    */
END;
$procedure$
;
