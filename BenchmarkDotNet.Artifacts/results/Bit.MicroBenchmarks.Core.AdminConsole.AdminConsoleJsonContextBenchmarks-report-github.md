```

BenchmarkDotNet v0.15.3, macOS 26.4.1 (25E253) [Darwin 25.4.0]
Apple M4 Max, 1 CPU, 14 logical and 14 physical cores
.NET SDK 10.0.107
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), Arm64 RyuJIT armv8.0-a
  DefaultJob : .NET 10.0.7 (10.0.7, 10.0.726.21808), Arm64 RyuJIT armv8.0-a


```
| Method                                          | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------ |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| Reflection_Serialize_ResetPasswordDataModel     |  47.86 ns | 0.400 ns | 0.355 ns |  1.00 |    0.01 | 0.0095 |      80 B |        1.00 |
| SourceGen_Serialize_ResetPasswordDataModel      |  39.08 ns | 0.460 ns | 0.430 ns |  0.82 |    0.01 | 0.0095 |      80 B |        1.00 |
| Reflection_Serialize_Permissions                | 171.04 ns | 0.876 ns | 0.684 ns |  3.57 |    0.03 | 0.0696 |     584 B |        7.30 |
| SourceGen_Serialize_Permissions                 | 146.17 ns | 2.522 ns | 2.359 ns |  3.05 |    0.05 | 0.0696 |     584 B |        7.30 |
| Reflection_Deserialize_Permissions              | 288.02 ns | 3.709 ns | 3.288 ns |  6.02 |    0.08 | 0.0038 |      32 B |        0.40 |
| SourceGen_Deserialize_Permissions               | 288.84 ns | 1.940 ns | 1.720 ns |  6.03 |    0.06 | 0.0038 |      32 B |        0.40 |
| Reflection_Deserialize_ResetPassword_CamelCase  |  72.94 ns | 0.848 ns | 0.752 ns |  1.52 |    0.02 | 0.0029 |      24 B |        0.30 |
| SourceGen_Deserialize_ResetPassword_CamelCase   |  65.78 ns | 0.869 ns | 0.678 ns |  1.37 |    0.02 | 0.0029 |      24 B |        0.30 |
| Reflection_Deserialize_ResetPassword_PascalCase |  72.45 ns | 1.446 ns | 1.353 ns |  1.51 |    0.03 | 0.0029 |      24 B |        0.30 |
| SourceGen_Deserialize_ResetPassword_PascalCase  |  65.64 ns | 0.999 ns | 0.934 ns |  1.37 |    0.02 | 0.0029 |      24 B |        0.30 |
