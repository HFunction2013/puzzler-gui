using System.Collections.Generic;
using System.Text.Json.Serialization;
using PuzzlerGui.Models;

namespace PuzzlerGui.Services;

/// <summary>
/// Source-generated JSON metadata required by Native AOT and trimming.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(SolveReport))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}

/// <summary>
/// Compact source-generated JSON metadata for exported solver reports.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = false)]
[JsonSerializable(typeof(SolveReport))]
internal partial class AppJsonSerializerCompactContext : JsonSerializerContext
{
}
