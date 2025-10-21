use ms_database::ToSql;

pub struct SqlParam {
    /// The parameter key (e.g., "@P1", "@P2")
    pub key: String,
    /// The column name this parameter maps to
    column: String,
    pub data: Box<dyn ToSql>,
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
            (true, false) => format!("{}]", trimmed),
            (false, true) => format!("[{}", trimmed),
            (false, false) => format!("[{}]", trimmed),
        }
    }
}

pub(crate) struct SqlParams {
    params: Vec<Box<SqlParam>>,
}

impl SqlParams {
    pub fn new() -> Self {
        Self { params: Vec::new() }
    }

    pub fn add(&mut self, column: impl Into<String>, value: Box<dyn ToSql>) {
        self.params.push(Box::new(SqlParam {
            key: format!("@P{}", self.params.len() + 1),
            column: column.into(),
            data: value,
        }));
    }

    pub fn keys(&self) -> Vec<String> {
        self.params.iter().map(|p| p.key.clone()).collect()
    }

    pub fn keys_as_columns(&self) -> Vec<String> {
        self.params
            .iter()
            .map(|p| format!("{} AS {}", p.key, p.column))
            .collect()
    }

    pub fn keys_except_columns(&self, excludes: Vec<&str>) -> Vec<String> {
        self.params
            .iter()
            .filter(|p| !excludes.contains(&p.column.as_str()))
            .map(|p| p.key.clone())
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

    pub fn columns_except(&self, excludes: Vec<&str>) -> Vec<String> {
        self.params
            .iter()
            .filter(|p| !excludes.contains(&p.column.as_str()))
            .map(|p| p.column())
            .collect()
    }

    pub fn columns_prefix_with(&self, prefix: &str) -> Vec<String> {
        self.params
            .iter()
            .map(|p| format!("{}{}", prefix, p.column()))
            .collect()
    }

    pub fn set_columns_equal(&self, assign_prefix: &str, source_prefix: &str) -> Vec<String> {
        self.params
            .iter()
            .map(|p| format!("{}{} = {}{}", assign_prefix, p.column(), source_prefix, p.column()))
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

    pub fn values(&self) -> Vec<&(dyn ToSql)> {
        self.params
            .iter()
            .map(|b| b.data.as_ref() as &(dyn ToSql))
            .collect()
    }
}

pub struct VecStringBuilder<'a> {
    strings: Vec<String>,
    ops: Vec<StringBuilderOperation<'a>>,
}

enum StringBuilderOperation<'a> {
    StringOperation(Box<dyn Fn(String) -> String + 'a>),
    VectorOperation(Box<dyn Fn(Vec<String>) -> Vec<String> + 'a>),
}

impl<'a> VecStringBuilder<'a> {
    pub fn new(strings: Vec<String>) -> Self {
        Self {
            strings,
            ops: Vec::new(),
        }
    }

    pub fn map<F>(&mut self, op: F)
    where
        F: Fn(String) -> String + 'static,
    {
        self.ops.push(StringBuilderOperation::StringOperation(Box::new(op)));
    }

    pub fn build(self) -> Vec<String> {
        let mut result = self.strings;
        for op in self.ops {
            match op {
                StringBuilderOperation::StringOperation(f) => {
                    result = result.into_iter().map(f.as_ref()).collect();
                }
                StringBuilderOperation::VectorOperation(f) => {
                    result = f(result);
                }
            }
        }
        result
    }

    pub fn join(self, sep: &str) -> String {
        self.build().join(sep)
    }

    pub fn except(&mut self, excludes: Vec<&'a str>) {
        self.ops.push(StringBuilderOperation::VectorOperation(Box::new(
            move |vec: Vec<String>| {
                vec.into_iter()
                    .filter(|s| !excludes.contains(&s.as_str()))
                    .collect()
            },
        )));
    }
}
