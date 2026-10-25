-- Creates the database account the server runs with (task 00002, section 2.4).
--
-- The account can read and write the rows of the application, nothing more: it is not a
-- superuser, cannot create databases or roles, and cannot change the schema. The
-- migrations run under the account that owns the schema, through the separate
-- Database:MigrationConnectionString setting.
--
-- The administrator audit log is append-only for this account: it may add records, but
-- not change, remove or truncate them. A trigger of the migrations refuses the changes for
-- every other account as well.
--
-- Run it as the owner of the database, after the migrations, for example:
--
--   psql -d aetheris_dev -v app_role=aetheris_app -f server/scripts/db/create-app-role.sql
--
-- The password is not part of this script. Set it afterwards from the psql prompt with
-- \password aetheris_app, so that it never lands in a file or in the shell history.

\set ON_ERROR_STOP on

SELECT format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT', :'app_role')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'app_role')
\gexec

SELECT format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), :'app_role')
\gexec

GRANT USAGE ON SCHEMA public TO :"app_role";
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO :"app_role";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO :"app_role";

-- Tables created by later migrations get the same rights.
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app_role";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO :"app_role";

-- The audit log only grows.
REVOKE UPDATE, DELETE, TRUNCATE ON admin_audit_log FROM :"app_role";

-- The migration history belongs to the migration account.
REVOKE INSERT, UPDATE, DELETE ON "__EFMigrationsHistory" FROM :"app_role";
