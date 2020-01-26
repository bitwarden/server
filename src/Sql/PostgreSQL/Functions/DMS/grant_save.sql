CREATE OR REPLACE PROCEDURE vault_dbo.grant_save(par_key character varying, par_type character varying, par_subjectid character varying, par_clientid character varying, par_creationdate timestamp without time zone, par_expirationdate timestamp without time zone, par_data text)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
-- Converted with error!
    -- BEGIN
    --    /*
    --    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    --    SET NOCOUNT ON
    --    */
    --    INSERT INTO [dbo]."Grant" ([Key], [Type], [SubjectId], [ClientId], [CreationDate], [ExpirationDate], [Data])
    --    VALUES (par_Key, par_Type, par_SubjectId, par_ClientId, par_CreationDate, par_ExpirationDate, par_Data)
    --    ON CONFLICT (Key) DO UPDATE SET [Type] = excluded.[Type], [SubjectId] = excluded.[SubjectId], [ClientId] = excluded.[ClientId], [CreationDate] = excluded.[CreationDate], [ExpirationDate] = excluded.[ExpirationDate], [Data] = excluded.[Data];
    -- END;
END;
$procedure$
;
