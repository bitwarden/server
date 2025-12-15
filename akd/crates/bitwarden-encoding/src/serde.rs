use std::str::FromStr;

/// A serde visitor that converts a string to a type that implements `FromStr`.
pub struct FromStrVisitor<T>(std::marker::PhantomData<T>);
impl<T> FromStrVisitor<T> {
    /// Create a new `FromStrVisitor` for the given type.
    pub fn new() -> Self {
        Self::default()
    }
}
impl<T> Default for FromStrVisitor<T> {
    fn default() -> Self {
        Self(Default::default())
    }
}
impl<T: FromStr> serde::de::Visitor<'_> for FromStrVisitor<T>
where
    T::Err: std::fmt::Debug,
{
    type Value = T;

    fn expecting(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
        write!(f, "a valid string")
    }

    fn visit_str<E>(self, v: &str) -> Result<Self::Value, E>
    where
        E: serde::de::Error,
    {
        T::from_str(v).map_err(|e| E::custom(format!("{e:?}")))
    }
}
