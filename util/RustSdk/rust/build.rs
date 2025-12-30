fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("libsdk")
        .csharp_namespace("Bit.RustSDK")
        .csharp_class_accessibility("public")
        .generate_csharp_file("../NativeMethods.g.cs")
        .unwrap();
}
