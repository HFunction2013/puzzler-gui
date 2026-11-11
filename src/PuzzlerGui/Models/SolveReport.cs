using System.Text.Json;
using System.Text.Json.Serialization;

namespace PuzzlerGui.Models;

public sealed class SolveReport
{
    [JsonPropertyName("generator")]
    public string Generator { get; set; } = string.Empty;

    [JsonPropertyName("puzzle")]
    public PuzzleInfo Puzzle { get; set; } = new();

    [JsonPropertyName("stats")]
    public SolveStats Stats { get; set; } = new();

    [JsonPropertyName("solutions")]
    public List<SolutionInfo> Solutions { get; set; } = new();
}

public sealed class PuzzleInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("piece_count")]
    public int PieceCount { get; set; }

    [JsonPropertyName("all_use")]
    public bool AllUse { get; set; }

    [JsonPropertyName("can_rotate")]
    public bool CanRotate { get; set; }

    [JsonPropertyName("can_flip")]
    public bool CanFlip { get; set; }

    [JsonPropertyName("board")]
    public BoardInfo Board { get; set; } = new();

    [JsonPropertyName("pieces")]
    public List<PieceInfo> Pieces { get; set; } = new();
}

public sealed class BoardInfo
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("cells")]
    public int Cells { get; set; }

    [JsonPropertyName("origin")]
    public int[] Origin { get; set; } = Array.Empty<int>();

    [JsonPropertyName("shape")]
    public List<string> Shape { get; set; } = new();
}

public sealed class PieceInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("cells")]
    public int Cells { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("can_rotate")]
    public bool CanRotate { get; set; }

    [JsonPropertyName("can_flip")]
    public bool CanFlip { get; set; }

    [JsonPropertyName("orientations")]
    public int Orientations { get; set; }

    [JsonPropertyName("shape")]
    public List<string> Shape { get; set; } = new();
}

public sealed class SolveStats
{
    [JsonPropertyName("nodes")]
    public long Nodes { get; set; }

    [JsonPropertyName("raw_solutions")]
    public long RawSolutions { get; set; }

    [JsonPropertyName("solutions")]
    public int Solutions { get; set; }

    [JsonPropertyName("elapsed_ms")]
    public double ElapsedMs { get; set; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }

    [JsonPropertyName("symmetry_group")]
    public int SymmetryGroup { get; set; }
}

public sealed class SolutionInfo
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("symmetry_multiplicity")]
    public long SymmetryMultiplicity { get; set; }

    [JsonPropertyName("grid")]
    public JsonElement[][] Grid { get; set; } = Array.Empty<JsonElement[]>();

    [JsonPropertyName("placements")]
    public List<PlacementInfo> Placements { get; set; } = new();
}

public sealed class PlacementInfo
{
    [JsonPropertyName("piece")]
    public string Piece { get; set; } = string.Empty;

    [JsonPropertyName("piece_index")]
    public int PieceIndex { get; set; }

    [JsonPropertyName("rotation_degrees")]
    public int RotationDegrees { get; set; }

    [JsonPropertyName("flipped")]
    public bool Flipped { get; set; }

    [JsonPropertyName("cells")]
    public int[][] Cells { get; set; } = Array.Empty<int[]>();
}
