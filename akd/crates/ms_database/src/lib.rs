mod migrate;
pub use migrate::{Migration, MigrationError, run_pending_migrations};

mod pool;
pub use pool::ConnectionManager as MsSqlConnectionManager;
pub use pool::{OnConnectError};

// re-expose tiberius types for convenience
pub use tiberius::{error::Error as MsDbError, Column, Row, FromSql, ToSql, IntoRow, TokenRow, ColumnData};

// re-expose bb8 types for convenience
pub type Pool = bb8::Pool<MsSqlConnectionManager>;
pub type PoolError = bb8::RunError<crate::pool::PoolError>;
pub type PooledConnection<'a> = bb8::PooledConnection<'a, MsSqlConnectionManager>;

// re-expose macros for convenience
pub use macros::{load_migrations, migration};

#[derive(thiserror::Error, Debug)]
pub enum MsDatabaseError {
    #[error("Pool error: {0}")]
    Pool(#[from] PoolError),
    #[error("On Connect error: {0}")]
    OnConnectError(#[from] OnConnectError),
    #[error("Migration error: {0}")]
    Migration(#[from] MigrationError),
    #[error("Database error: {0}")]
    Db(#[from] MsDbError),
}
