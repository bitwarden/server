#![doc = include_str!("../README.md")]

mod b64;
mod b64url;
mod serde;

pub use b64::{NotB64EncodedError, B64};
pub use b64url::{B64Url, NotB64UrlEncodedError};
pub use serde::FromStrVisitor;
