use macros::load_migrations;
use tiberius::{error};

use crate::{ManagedConnection};

type Result<T> = std::result::Result<T, MigrationError>;
const MIGRATIONS: &[Migration] = load_migrations!("./migrations");

#[derive(thiserror::Error, Debug)]
pub enum MigrationError {
    #[error("Database error: {0}")]
    DatabaseError(#[from] error::Error),
    // Other error variants can be added here
}

pub(crate) async fn pending_migrations(conn: &mut ManagedConnection) -> Result<Vec<Migration>> {
    // get applied migrations
    let applied = read_applied_migrations(conn).await?;

    // create list of migrations that haven't been applied, in order.
    let pending = MIGRATIONS
        .iter()
        .filter(|m| !applied.contains(&m.name.to_string()))
        .cloned()
        .collect();

    Ok(pending)
}

const CREATE_MIGRATIONS_TABLE_SQL: &str = r#"
IF OBJECT_ID('dbo.__migrations') IS NULL
BEGIN
    CREATE TABLE dbo.__migrations (
        version VARCHAR(50) PRIMARY KEY,
        run_on DATETIME NOT NULL DEFAULT GETDATE()
    );
END
"#;

async fn ensure_migrations_table_exists(conn: &mut ManagedConnection) -> Result<()> {
    // create the migrations table if it doesn't exist
    conn.simple_query(CREATE_MIGRATIONS_TABLE_SQL).await?;

    Ok(())
}

const READ_APPLIED_MIGRATIONS: &str = "SELECT version FROM dbo.__migrations ORDER BY version";

async fn read_applied_migrations(conn: &mut ManagedConnection) -> Result<Vec<String>> {
    let applied = conn.query(READ_APPLIED_MIGRATIONS, &[])
        .await?
        .into_first_result()
        .await?
        .into_iter()
        .map(|row| row.get::<&str,_>("version").expect("version column is present").to_owned())
        .collect();
    Ok(applied)
}

const RECORD_MIGRATION_SQL: &str = "INSERT INTO dbo.__migrations (version) VALUES (@P1)";

async fn record_migration(conn: &mut ManagedConnection, migration: &Migration) -> Result<()> {
    conn.execute(RECORD_MIGRATION_SQL, &[&migration.name]).await?;
    Ok(())
}

pub(crate) async fn run_migration(migration: &Migration, conn: &mut ManagedConnection) -> Result<()> {
    if migration.run_in_transaction {
        conn.simple_query("BEGIN TRANSACTION").await?;

        let result = async {
            conn.simple_query(&migration.up).await?;
            record_migration(conn, migration).await?;
            Ok::<_, MigrationError>(())
        }.await;

        match result {
            Ok(_) => {
                conn.simple_query("COMMIT").await?;
                Ok(())
            }
            Err(e) => {
                conn.simple_query("ROLLBACK").await?;
                Err(e)
            }
        }
    } else {
        conn.simple_query(&migration.up).await?;
        record_migration(conn, migration).await?;
        Ok(())
    }
}

pub async fn run_pending_migrations(conn: &mut ManagedConnection) -> Result<()> {
    ensure_migrations_table_exists(conn).await?;
    let pending = pending_migrations(conn).await?;
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
