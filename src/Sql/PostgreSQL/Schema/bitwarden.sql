DROP SCHEMA bitwarden cascade;

CREATE SCHEMA bitwarden AUTHORIZATION bitwarden;

ALTER ROLE bitwarden SET search_path TO bitwarden;
