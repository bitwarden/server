use tiberius::{error};
use tracing::{debug, info, instrument, warn};

use crate::pool::ManagedConnection;
use crate::TABLE_MIGRATIONS;

type Result<T> = std::result::Result<T, MigrationError>;

#[derive(thiserror::Error, Debug)]
pub enum MigrationError {
    #[error("Database error: {0}")]
    DatabaseError(#[from] error::Error),
    // Other error variants can be added here
}

pub(crate) async fn pending_migrations(conn: &mut ManagedConnection, all_migrations: &[Migration]) -> Result<Vec<Migration>> {
    // get applied migrations
    let applied = read_applied_migrations(conn).await?;

    // create list of migrations that haven't been applied, in order.
    let pending = all_migrations
        .iter()
        .filter(|m| !applied.contains(&m.name.to_string()))
        .cloned()
        .collect();

    Ok(pending)
}

async fn ensure_migrations_table_exists(conn: &mut ManagedConnection) -> Result<()> {
    let create_migrations_table = format!(
        "IF OBJECT_ID('dbo.{TABLE_MIGRATIONS}') IS NULL
        BEGIN
            CREATE TABLE dbo.{TABLE_MIGRATIONS} (
                version VARCHAR(50) PRIMARY KEY,
                run_on DATETIME NOT NULL DEFAULT GETDATE()
            );
        END"
    );
    // create the migrations table if it doesn't exist
    conn.simple_query(&create_migrations_table).await?;

    Ok(())
}


async fn read_applied_migrations(conn: &mut ManagedConnection) -> Result<Vec<String>> {
    let read_applied_migrations = format!("SELECT version FROM dbo.{TABLE_MIGRATIONS} ORDER BY version");
    let applied = conn.query(&read_applied_migrations, &[])
        .await?
        .into_first_result()
        .await?
        .into_iter()
        .map(|row| row.get::<&str,_>("version").expect("version column is present").to_owned())
        .collect();
    Ok(applied)
}

#[instrument(skip(conn, migration), fields(migration_name = migration.name), level = "debug")]
async fn record_migration(conn: &mut ManagedConnection, migration: &Migration) -> Result<()> {
    debug!("Recording migration");
    let record_migration_sql = format!("INSERT INTO dbo.{TABLE_MIGRATIONS} (version) VALUES (@P1)");
    conn.execute(&record_migration_sql, &[&migration.name]).await?;
    Ok(())
}

#[instrument(skip(conn, migration), fields(migration_name = migration.name))]
pub(crate) async fn run_migration(migration: &Migration, conn: &mut ManagedConnection) -> Result<()> {
    if migration.run_in_transaction {
        conn.simple_query("BEGIN TRANSACTION").await?;

        let result = async {
            conn.simple_query(migration.up).await?;
            record_migration(conn, migration).await?;
            Ok::<_, MigrationError>(())
        }.await;

        match result {
            Ok(_) => {
                info!("Committing migration");
                conn.simple_query("COMMIT").await?;
                Ok(())
            }
            Err(e) => {
                warn!(error = ?e, "Rolling back migration due to error");
                conn.simple_query("ROLLBACK").await?;
                Err(e)
            }
        }
    } else {
        conn.simple_query(migration.up).await?;
        record_migration(conn, migration).await?;
        Ok(())
    }
}

pub async fn run_pending_migrations(conn: &mut ManagedConnection, all_migrations: &[Migration]) -> Result<()> {
    ensure_migrations_table_exists(conn).await?;
    let pending = pending_migrations(conn, all_migrations).await?;
    info!(num_pending = pending.len(), "Running pending migrations");
    for migration in pending {
        run_migration(&migration, conn).await?;
    }
    Ok(())
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Migration {
    /// The name of the migration, derived from the directory name
    pub name: &'static str,
    /// The SQL to execute when applying the migration
    pub up: &'static str,
    /// The SQL to execute when rolling back the migration (if provided)
    pub down: Option<&'static str>,
    /// Whether to run this migration in a transaction
    pub run_in_transaction: bool,
}

impl Migration {
    /// Creates a new migration with the given properties.
    pub const fn new(
        name: &'static str,
        up: &'static str,
        down: Option<&'static str>,
        run_in_transaction: bool,
    ) -> Self {
        Self {
            name,
            up,
            down,
            run_in_transaction,
        }
    }
}
