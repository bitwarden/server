internal static unsafe partial class NativeMethods
{
    // https://docs.microsoft.com/en-us/dotnet/standard/native-interop/cross-platform
    // Library path will search
    // win => __DllName, __DllName.dll
    // linux, osx => __DllName.so, __DllName.dylib

    static NativeMethods()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, DllImportResolver);
    }

    static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == __DllName)
        {
            var path = "runtimes/";
            var extension = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path += "win-";
                extension = ".dll";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                path += "osx-";
                extension = ".dylib";
            }
            else
            {
                path += "linux-";
                extension = ".so";
            }

            if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                path += "x86";
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                path += "x64";
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                path += "arm64";
            }

            path += "/native/" + __DllName + extension;

            return NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, path), assembly, searchPath);
        }

        return IntPtr.Zero;
    }
}
