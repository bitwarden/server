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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_sequential_parameter_keys() {
        // Parameters must be sequential (@P1, @P2, @P3...) for SQL Server
        let mut params = SqlParams::new();
        params.add("label", Box::new("value1".to_string()));
        params.add("value", Box::new("value2".to_string()));
        params.add("epoch", Box::new(42i64));

        let keys = params.keys();
        assert_eq!(keys, vec!["@P1", "@P2", "@P3"]);
    }

    #[test]
    fn test_column_bracketing() {
        // SQL Server requires column names in brackets to handle reserved words
        let mut params = SqlParams::new();
        params.add("user", Box::new("test".to_string()));

        let columns = params.columns();
        assert_eq!(columns[0], "[user]");
    }

    #[test]
    fn test_column_bracketing_idempotent() {
        // Bracketing should be idempotent - don't double-bracket
        assert_eq!(SqlParam::wrap_in_brackets("[already_bracketed]"), "[already_bracketed]");
        assert_eq!(SqlParam::wrap_in_brackets("not_bracketed"), "[not_bracketed]");
        assert_eq!(SqlParam::wrap_in_brackets("[partial"), "[partial]");
        assert_eq!(SqlParam::wrap_in_brackets("partial]"), "[partial]");
    }

    #[test]
    fn test_column_bracketing_with_whitespace() {
        // Should trim whitespace before bracketing
        assert_eq!(SqlParam::wrap_in_brackets("  column  "), "[column]");
        assert_eq!(SqlParam::wrap_in_brackets("  [column]  "), "[column]");
    }

    #[test]
    fn test_keys_as_columns() {
        // Used in SELECT statements: @P1 AS [column1], @P2 AS [column2]
        let mut params = SqlParams::new();
        params.add("id", Box::new(1i32));
        params.add("name", Box::new("test".to_string()));

        let keys_as_cols = params.keys_as_columns();
        assert_eq!(keys_as_cols, vec!["@P1 AS [id]", "@P2 AS [name]"]);
    }

    #[test]
    fn test_key_for_column() {
        // Lookup parameter key by column name
        let mut params = SqlParams::new();
        params.add("label", Box::new("value".to_string()));
        params.add("epoch", Box::new(1i64));

        assert_eq!(params.key_for("label"), Some("@P1".to_string()));
        assert_eq!(params.key_for("epoch"), Some("@P2".to_string()));
        assert_eq!(params.key_for("nonexistent"), None);
    }

    #[test]
    fn test_set_columns_equal() {
        // Used in UPDATE statements: target.[col] = source.[col]
        let mut params = SqlParams::new();
        params.add("label", Box::new("val".to_string()));
        params.add("value", Box::new("val".to_string()));
        params.add("id", Box::new(1i32));

        let set_clause = params.set_columns_equal_except("target.", "source.", vec!["id"]);
        assert_eq!(set_clause, vec!["target.[label] = source.[label]", "target.[value] = source.[value]"]);
    }

    #[test]
    fn test_empty_params() {
        let params = SqlParams::new();
        assert!(params.keys().is_empty());
        assert!(params.columns().is_empty());
        assert_eq!(params.key_for("anything"), None);
    }

    #[test]
    fn test_param_count() {
        let mut params = SqlParams::new();
        assert_eq!(params.keys().len(), 0);

        params.add("col1", Box::new(1i32));
        assert_eq!(params.keys().len(), 1);

        params.add("col2", Box::new(2i32));
        assert_eq!(params.keys().len(), 2);
    }
}
