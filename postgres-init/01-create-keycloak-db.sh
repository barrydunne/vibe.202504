#!/bin/bash
# Use psql to execute SQL commands
# The environment variables POSTGRES_USER and POSTGRES_DB are available from the postgres container
set -e # Exit immediately if a command exits with a non-zero status.

# Create the keycloak database only if it doesn't already exist
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    SELECT 'CREATE DATABASE keycloak_db'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'keycloak_db')\gexec
EOSQL

echo "Checked/Created keycloak_db database"

# Optional: Grant privileges if Keycloak needs them before its own setup
# Usually Keycloak manages its schema with the user provided, but if needed:
# psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "keycloak_db" <<-EOSQL
#     GRANT ALL PRIVILEGES ON DATABASE keycloak_db TO $POSTGRES_USER;
# EOSQL
# echo "Granted privileges on keycloak_db to $POSTGRES_USER"