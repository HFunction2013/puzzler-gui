using System.Text.Json;
using PuzzlerGui.Models;
using PuzzlerGui.Services;

namespace PuzzlerGui.Tests;

public sealed class PuzzleYamlTests
{
    private const string WrappedPuzzle = """
        puzzle:
          name: "four dominoes"
          all_use: true
          can_rotate: true
          can_flip: false
          board_shape: rectangle{4,2}
          pieces:
            - id: "domino"
              shape: |
                ##
              count: 4
        """;

    [Fact]
    public void Load_ReadsWrappedDocument()
    {
        var document = PuzzleYaml.Load(WrappedPuzzle);

        Assert.Equal("four dominoes", document.Name);
        Assert.Equal(4, document.Board.Width);
        Assert.Equal(2, document.Board.Height);
        Assert.Equal(8, document.Board.PlayableCells);
        Assert.True(document.AllUse);
        Assert.True(document.CanRotate);
        Assert.False(document.CanFlip);

        var piece = Assert.Single(document.Pieces);
        Assert.Equal("domino", piece.Id);
        Assert.Equal(4, piece.Count);
        Assert.Equal(2, piece.Area);
        Assert.Equal(8, document.TotalPieceCells);
    }

    [Fact]
    public void Load_AcceptsTopLevelMappingAndDashStylePieces()
    {
        const string text = """
            name: tray
            board_shape: |-
              ###
              #.#
              ###
            pieces:
              - id: c
                shape: "##"
                count: 4
            """;

        var document = PuzzleYaml.Load(text);

        Assert.Equal("tray", document.Name);
        Assert.Equal(3, document.Board.Width);
        Assert.Equal(3, document.Board.Height);
        Assert.Equal(8, document.Board.PlayableCells);
        Assert.Equal(8, document.TotalPieceCells);
        Assert.False(document.Board.AllPlayable);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsBoardAndPieces()
    {
        var original = PuzzleYaml.Load(WrappedPuzzle);
        var saved = PuzzleYaml.Save(original);
        var reloaded = PuzzleYaml.Load(saved);

        Assert.Equal(original.Name, reloaded.Name);
        Assert.Equal(original.Board.Width, reloaded.Board.Width);
        Assert.Equal(original.Board.Height, reloaded.Board.Height);
        Assert.Equal(original.Board.AllPlayable, reloaded.Board.AllPlayable);
        Assert.Equal(original.AllUse, reloaded.AllUse);
        Assert.Equal(original.CanRotate, reloaded.CanRotate);
        Assert.Equal(original.CanFlip, reloaded.CanFlip);
        Assert.Equal(original.Pieces.Count, reloaded.Pieces.Count);

        for (var i = 0; i < original.Pieces.Count; i++)
        {
            var before = original.Pieces[i];
            var after = reloaded.Pieces[i];
            Assert.Equal(before.Id, after.Id);
            Assert.Equal(before.Count, after.Count);
            Assert.Equal(before.Area, after.Area);
            Assert.Equal(before.CanRotate, after.CanRotate);
            Assert.Equal(before.CanFlip, after.CanFlip);
        }
    }

    [Fact]
    public void Save_EmitsRectangleForFullBoardsAndBlockScalarForHoles()
    {
        var full = PuzzleDocument.CreateDefault();
        var fullText = PuzzleYaml.Save(full);
        Assert.Contains("board_shape: rectangle{4,4}", fullText);

        var holey = new PuzzleDocument("holey", BoardModel.FromAscii(new[] { "###", "#.#", "###" }));
        holey.Pieces.Add(new PieceModel("domino", ShapeModel.FromText("##"), 4));
        var holeyText = PuzzleYaml.Save(holey);
        Assert.Contains("board_shape: |", holeyText);
    }

    [Fact]
    public void Load_RejectsMissingPuzzleSection()
    {
        const string text = "name: nothing\n";

        var error = Assert.Throws<PuzzleFormatException>(() => PuzzleYaml.Load(text));
        Assert.Contains("puzzle", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_RejectsMalformedRectangle()
    {
        const string text = """
            puzzle:
              board_shape: rectangle{4}
              pieces:
                - id: a
                  shape: "##"
            """;

        var error = Assert.Throws<PuzzleFormatException>(() => PuzzleYaml.Load(text));
        Assert.Contains("rectangle", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_HonoursPerPieceOverrides()
    {
        const string text = """
            puzzle:
              can_rotate: true
              can_flip: true
              board_shape: rectangle{4,1}
              pieces:
                - id: fixed
                  shape: "####"
                  can_rotate: false
                  can_flip: false
            """;

        var document = PuzzleYaml.Load(text);
        var piece = Assert.Single(document.Pieces);
        Assert.Equal(TriState.Forbid, piece.CanRotate);
        Assert.Equal(TriState.Forbid, piece.CanFlip);
    }
}

public sealed class ShapeAndBoardTests
{
    [Fact]
    public void Shape_FromText_ParsesSizeAndArea()
    {
        var shape = ShapeModel.FromText(".##.\n##..");

        Assert.Equal(3, shape.Width);
        Assert.Equal(2, shape.Height);
        Assert.Equal(4, shape.Area);
        Assert.False(shape.IsFilled(0, 0));
        Assert.True(shape.IsFilled(0, 1));
    }

    [Fact]
    public void Shape_RotateRight_PreservesAreaAndSwapsDimensions()
    {
        var shape = ShapeModel.FromText("###.\n#...");

        shape.RotateRight();
        shape.Trim();

        Assert.Equal(2, shape.Width);
        Assert.Equal(3, shape.Height);
        Assert.Equal(4, shape.Area);
        Assert.True(shape.IsConnected());
    }

    [Fact]
    public void Shape_DetectsDisconnectedCells()
    {
        var shape = ShapeModel.FromText("#.#");

        Assert.False(shape.IsConnected());
    }

    [Fact]
    public void Board_FromAscii_CountsPlayableCellsAndExtent()
    {
        var board = BoardModel.FromAscii(new[] { ".###.", ".#.#." });

        Assert.Equal(3, board.Width);
        Assert.Equal(2, board.Height);
        Assert.Equal(5, board.PlayableCells);
        Assert.False(board.AllPlayable);
    }

    [Fact]
    public void Board_Resize_KeepsOverlappingCells()
    {
        var board = BoardModel.FromAscii(new[] { "##", "#." });
        board.Resize(3, 3);

        Assert.Equal(3, board.Width);
        Assert.True(board.IsPlayable(0, 0));
        Assert.True(board.IsPlayable(0, 1));
        Assert.True(board.IsPlayable(1, 0));
        Assert.False(board.IsPlayable(1, 1));
        Assert.False(board.IsPlayable(2, 2));
    }

    [Fact]
    public void Document_Validation_FlagsAreaMismatchAndDuplicateIds()
    {
        LocalizationService.Instance.SetLanguage("en");
        var document = new PuzzleDocument("bad", BoardModel.Rectangle(4, 4));
        document.Pieces.Add(new PieceModel("a", ShapeModel.FromText("##"), 1));
        document.Pieces.Add(new PieceModel("a", ShapeModel.FromText("##"), 1));

        document.Validate(out var errors, out var warnings);

        Assert.Contains(errors, message => message.Contains("no arrangement", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(warnings, message => message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Document_Default_IsSolvableShaped()
    {
        var document = PuzzleDocument.CreateDefault();
        document.Validate(out var errors, out _);

        Assert.Empty(errors);
        Assert.Equal(16, document.Board.PlayableCells);
        Assert.Equal(16, document.TotalPieceCells);
    }
}

public sealed class SolveReportTests
{
    private const string FourDominoesJson = """
        {
          "generator": "puzzler",
          "puzzle": {
            "name": "four dominoes",
            "piece_count": 4,
            "all_use": true,
            "can_rotate": true,
            "can_flip": false,
            "board": {
              "width": 4,
              "height": 2,
              "cells": 8,
              "origin": [0, 0],
              "shape": ["####", "####"]
            },
            "pieces": [
              {
                "id": "domino",
                "cells": 2,
                "count": 4,
                "can_rotate": true,
                "can_flip": false,
                "orientations": 2,
                "shape": ["##"]
              }
            ]
          },
          "stats": {
            "nodes": 15,
            "raw_solutions": 5,
            "solutions": 1,
            "elapsed_ms": 0.29,
            "truncated": false,
            "symmetry_group": 4
          },
          "solutions": [
            {
              "index": 1,
              "grid": [[0, 0, 1, 1], [0, 0, 1, 1]],
              "placements": [
                {
                  "piece": "domino",
                  "piece_index": 0,
                  "rotation_degrees": 0,
                  "flipped": false,
                  "cells": [[0, 0], [0, 1]]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Deserialize_MapsCliJson()
    {
        var report = JsonSerializer.Deserialize<SolveReport>(FourDominoesJson);

        Assert.NotNull(report);
        Assert.Equal("puzzler", report!.Generator);
        Assert.Equal("four dominoes", report.Puzzle.Name);
        Assert.Equal(8, report.Puzzle.Board.Cells);
        Assert.Equal(15, report.Stats.Nodes);
        Assert.Equal(1, report.Stats.Solutions);
        Assert.Equal(4, report.Stats.SymmetryGroup);
        Assert.Single(report.Solutions);

        var solution = report.Solutions[0];
        Assert.Equal(2, solution.Grid.Length);
        Assert.Equal(4, solution.Grid[0].Length);
        Assert.Single(solution.Placements);
        Assert.Equal("domino", solution.Placements[0].Piece);
        Assert.Equal(2, solution.Placements[0].Cells.Length);
    }
}
