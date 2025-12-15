use ms_database::ToSql;
use tracing::trace;

pub struct SqlParam {
    /// The parameter key (e.g., "@P1", "@P2")
    pub key: String,
    /// The column name this parameter maps to
    column: String,
    pub data: Box<dyn ToSql>,
}

impl std::fmt::Debug for SqlParam {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("SqlParam")
            .field("key", &self.key)
            .field("column", &self.column)
            .field("data", &"<opaque>")
            .finish()
    }
}

impl SqlParam {
    fn column(&self) -> String {
        SqlParam::wrap_in_brackets(&self.column)
    }

    fn wrap_in_brackets(s: &str) -> String {
        // ensure column names are wrapped in brackets for SQL Server
        let trimmed = s.trim();
        let starts_with_bracket = trimmed.starts_with('[');
        let ends_with_bracket = trimmed.ends_with(']');

        match (starts_with_bracket, ends_with_bracket) {
            (true, true) => trimmed.to_string(),
            (true, false) => format!("{trimmed}]"),
            (false, true) => format!("[{trimmed}"),
            (false, false) => format!("[{trimmed}]"),
        }
    }
}

#[derive(Debug)]
pub(crate) struct SqlParams {
    params: Vec<Box<SqlParam>>,
}

impl SqlParams {
    pub fn new() -> Self {
        Self { params: Vec::new() }
    }

    pub fn add(&mut self, column: impl Into<String>, value: Box<dyn ToSql>) {
        let column_name = column.into();
        let param_key = format!("@P{}", self.params.len() + 1);
        trace!("Adding SQL param: {} for column {}", param_key, column_name);
        self.params.push(Box::new(SqlParam {
            key: param_key,
            column: column_name,
            data: value,
        }));
    }

    pub fn keys(&self) -> Vec<String> {
        self.params.iter().map(|p| p.key.clone()).collect()
    }

    pub fn keys_as_columns(&self) -> Vec<String> {
        self.params
            .iter()
            .map(|p| format!("{} AS {}", p.key, p.column()))
            .collect()
    }

    pub fn key_for(&self, column: &str) -> Option<String> {
        self.params
            .iter()
            .find(|p| p.column == column)
            .map(|p| p.key.clone())
    }

    pub fn columns(&self) -> Vec<String> {
        self.params
            .iter()
            .map(|p| p.column())
            .collect()
    }

    pub fn set_columns_equal_except(
        &self,
        assign_prefix: &str,
        source_prefix: &str,
        excludes: Vec<&str>,
    ) -> Vec<String> {
        self.params
            .iter()
            .filter(|p| !excludes.contains(&p.column.as_str()))
            .map(|p| format!("{}{} = {}{}", assign_prefix, p.column(), source_prefix, p.column()))
            .collect()
    }

    pub fn values(&self) -> Vec<&dyn ToSql> {
        self.params
            .iter()
            .map(|b| b.data.as_ref() as &dyn ToSql)
            .collect()
    }
}
