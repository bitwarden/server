use akd::errors::StorageError;
use serde::{Deserialize, Serialize};

use crate::ms_sql::MsSql;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum DbConfig {
    MsSql {
        connection_string: String,
        pool_size: u32,
    },
}

/// Enum to represent different database types supported by the storage layer.
/// Each variant is cheap to clone for reuse across threads.
#[derive(Debug, Clone)]
pub enum DatabaseType {
    MsSql(MsSql),
}

impl DbConfig {
    pub async fn connect(&self) -> Result<DatabaseType, StorageError> {
        let db = match self {
            DbConfig::MsSql {
                connection_string,
                pool_size,
            } => {
                let db = crate::ms_sql::MsSql::builder(connection_string.clone())
                    .pool_size(*pool_size)
                    .build()
                    .await?;
                DatabaseType::MsSql(db)
            }
        };

        Ok(db)
    }
}
