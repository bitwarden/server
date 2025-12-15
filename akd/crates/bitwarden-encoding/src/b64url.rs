use std::str::FromStr;

use data_encoding::BASE64URL_NOPAD;
use serde::{Deserialize, Serialize};
use thiserror::Error;

/// Base64URL encoded data
///
/// Is indifferent about padding when decoding, but always produces padding when encoding.
#[derive(Debug, Serialize, Deserialize, Clone, Hash, PartialEq, Eq)]
#[serde(try_from = "&str", into = "String")]
pub struct B64Url(Vec<u8>);

impl B64Url {
    /// Returns a byte slice of the inner vector.
    pub fn as_bytes(&self) -> &[u8] {
        &self.0
    }

    /// Returns the inner byte vector.
    pub fn into_bytes(self) -> Vec<u8> {
        self.0
    }
}

impl From<Vec<u8>> for B64Url {
    fn from(src: Vec<u8>) -> Self {
        Self(src)
    }
}
impl From<&[u8]> for B64Url {
    fn from(src: &[u8]) -> Self {
        Self(src.to_vec())
    }
}

impl From<B64Url> for Vec<u8> {
    fn from(src: B64Url) -> Self {
        src.0
    }
}

impl From<B64Url> for String {
    fn from(src: B64Url) -> Self {
        String::from(&src)
    }
}

impl From<&B64Url> for String {
    fn from(src: &B64Url) -> Self {
        BASE64URL_NOPAD.encode(&src.0)
    }
}

impl std::fmt::Display for B64Url {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(String::from(self).as_str())
    }
}

/// An error returned when a string is not base64 decodable.
#[derive(Debug, Error)]
#[error("Data isn't base64url encoded")]
pub struct NotB64UrlEncodedError;

const BASE64URL_PERMISSIVE: data_encoding::Encoding = data_encoding_macro::new_encoding! {
    symbols: "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_",
    padding: None,
    check_trailing_bits: false,
};
const BASE64URL_PADDING: &str = "=";

impl TryFrom<String> for B64Url {
    type Error = NotB64UrlEncodedError;

    fn try_from(value: String) -> Result<Self, Self::Error> {
        Self::try_from(value.as_str())
    }
}

impl TryFrom<&str> for B64Url {
    type Error = NotB64UrlEncodedError;

    fn try_from(value: &str) -> Result<Self, Self::Error> {
        let sane_string = value.trim_end_matches(BASE64URL_PADDING);
        BASE64URL_PERMISSIVE
            .decode(sane_string.as_bytes())
            .map(Self)
            .map_err(|_| NotB64UrlEncodedError)
    }
}

impl FromStr for B64Url {
    type Err = NotB64UrlEncodedError;

    fn from_str(s: &str) -> Result<Self, Self::Err> {
        Self::try_from(s)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_b64url_from_vec() {
        let data = vec![72, 101, 108, 108, 111];
        let b64url = B64Url::from(data.clone());
        assert_eq!(Vec::<u8>::from(b64url), data);
    }

    #[test]
    fn test_b64url_from_slice() {
        let data = b"Hello";
        let b64url = B64Url::from(data.as_slice());
        assert_eq!(b64url.as_bytes(), data);
    }

    #[test]
    fn test_b64url_encoding_with_padding() {
        let data = b"Hello, World!";
        let b64url = B64Url::from(data.as_slice());
        let encoded = String::from(&b64url);
        assert_eq!(encoded, "SGVsbG8sIFdvcmxkIQ");
    }

    #[test]
    fn test_b64url_decoding_with_padding() {
        let encoded_with_padding = "SGVsbG8sIFdvcmxkIQ==";
        let b64url = B64Url::try_from(encoded_with_padding).unwrap();
        assert_eq!(b64url.as_bytes(), b"Hello, World!");
    }

    #[test]
    fn test_b64url_decoding_without_padding() {
        let encoded_without_padding = "SGVsbG8sIFdvcmxkIQ";
        let b64url = B64Url::try_from(encoded_without_padding).unwrap();
        assert_eq!(b64url.as_bytes(), b"Hello, World!");
    }

    #[test]
    fn test_b64url_round_trip_with_padding() {
        let original = b"Test data that requires padding!";
        let b64url = B64Url::from(original.as_slice());
        let encoded = String::from(&b64url);
        let decoded = B64Url::try_from(encoded.as_str()).unwrap();
        assert_eq!(decoded.as_bytes(), original);
    }

    #[test]
    fn test_b64url_round_trip_without_padding() {
        let original = b"Test data";
        let b64url = B64Url::from(original.as_slice());
        let encoded = String::from(&b64url);
        let decoded = B64Url::try_from(encoded.as_str()).unwrap();
        assert_eq!(decoded.as_bytes(), original);
    }

    #[test]
    fn test_b64url_display() {
        let data = b"Hello";
        let b64url = B64Url::from(data.as_slice());
        assert_eq!(b64url.to_string(), "SGVsbG8");
    }

    #[test]
    fn test_b64url_invalid_encoding() {
        let invalid_b64url = "This is not base64url!@#$";
        let result = B64Url::try_from(invalid_b64url);
        assert!(result.is_err());
    }

    #[test]
    fn test_b64url_empty_string() {
        let empty = "";
        let b64url = B64Url::try_from(empty).unwrap();
        assert_eq!(b64url.as_bytes().len(), 0);
    }

    #[test]
    fn test_b64url_padding_removal() {
        let encoded_with_padding = "SGVsbG8sIFdvcmxkIQ==";
        let b64url = B64Url::try_from(encoded_with_padding).unwrap();
        assert_eq!(b64url.as_bytes(), b"Hello, World!");
    }

    #[test]
    fn test_b64url_serialization() {
        let data = b"serialization test";
        let b64url = B64Url::from(data.as_slice());

        let serialized = serde_json::to_string(&b64url).unwrap();
        assert_eq!(serialized, "\"c2VyaWFsaXphdGlvbiB0ZXN0\"");

        let deserialized: B64Url = serde_json::from_str(&serialized).unwrap();
        assert_eq!(b64url.as_bytes(), deserialized.as_bytes());
    }

    #[test]
    fn test_not_b64url_encoded_error_display() {
        let error = NotB64UrlEncodedError;
        assert_eq!(error.to_string(), "Data isn't base64url encoded");
    }

    #[test]
    fn test_b64url_from_str() {
        let encoded = "SGVsbG8sIFdvcmxkIQ==";
        let b64url: B64Url = encoded.parse().unwrap();
        assert_eq!(b64url.as_bytes(), b"Hello, World!");
    }

    #[test]
    fn test_b64url_eq_and_hash() {
        let data1 = b"test data";
        let data2 = b"test data";
        let data3 = b"different data";

        let b64url_1 = B64Url::from(data1.as_slice());
        let b64url_2 = B64Url::from(data2.as_slice());
        let b64url_3 = B64Url::from(data3.as_slice());

        assert_eq!(b64url_1, b64url_2);
        assert_ne!(b64url_1, b64url_3);

        use std::{
            collections::hash_map::DefaultHasher,
            hash::{Hash, Hasher},
        };

        let mut hasher1 = DefaultHasher::new();
        let mut hasher2 = DefaultHasher::new();

        b64url_1.hash(&mut hasher1);
        b64url_2.hash(&mut hasher2);

        assert_eq!(hasher1.finish(), hasher2.finish());
    }
}
