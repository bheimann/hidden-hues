using System.Collections.Generic;
using Godot;
using HiddenHues.Core;

namespace HiddenHues;

/// <summary>
/// Draws the prototype level and drives the Core simulation: one tick per second at 1x speed,
/// with belt pixels sliding smoothly between tiles.
/// </summary>
public partial class BoardView : Node2D
{
    private const float Cell = 36f;
    private const float Gap = 14f;
    private const float HintWidth = 5f;
    private const float FeederHeight = 44f;
    private static readonly int[] Speeds = { 1, 2, 4 };

    private static readonly Color Background = Color.Color8(0xE6, 0xE8, 0xF2);
    private static readonly Color Track = Color.Color8(0x4A, 0x4E, 0x66);
    private static readonly Color TrackTile = Color.Color8(0x3C, 0x40, 0x55);
    private static readonly Color Warning = Color.Color8(0xE5, 0x39, 0x46);

    private Level _level = null!;
    private Game _game = null!;
    private int _seed = 1;
    private int _speedIndex;
    private double _sinceTick;
    private Vector2 _origin;

    // Where each belt pixel was drawn at the last tick, so it can slide to its current tile.
    private readonly Dictionary<int, Vector2> _from = new();

    private Button _speedButton = null!;

    public override void _Ready()
    {
        _level = PrototypeLevel.Create();
        BuildHud();
        StartGame();

        // `-- --capture=<file.png>` picks a region, lets the belt run, saves a screenshot and quits.
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--capture="))
                CaptureAsync(arg["--capture=".Length..]);
    }

    private async void CaptureAsync(string path)
    {
        _speedIndex = Speeds.Length - 1;
        UpdateSpeedLabel();
        _game.Pick(new GridPos(_game.Board.Width - 1, _game.Board.Height / 2));

        await ToSignal(GetTree().CreateTimer(2.6), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
        GetTree().Quit();
    }

    public override void _Process(double delta)
    {
        _sinceTick += delta * Speeds[_speedIndex];
        while (_sinceTick >= 1.0)
        {
            _sinceTick -= 1.0;
            Step();
        }

        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;

        var local = (GetLocalMousePosition() - _origin) / Cell;
        var cell = new GridPos(Mathf.FloorToInt(local.X), Mathf.FloorToInt(local.Y));
        if (_game.Board.Contains(cell) && _game.Pick(cell).Count > 0)
            QueueRedraw();
    }

    public override void _Draw()
    {
        var viewport = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, viewport), Background);

        DrawBelt();
        DrawHints();
        DrawCells();
        DrawFeeder();
        DrawBeltPixels();
        DrawStatus(viewport);
    }

    private void StartGame()
    {
        _game = new Game(_level.CreateScrambledBoard(_seed));
        _from.Clear();
        _sinceTick = 0;

        var viewport = GetViewportRect().Size;
        var boardSize = new Vector2(_game.Board.Width, _game.Board.Height) * Cell;
        _origin = new Vector2((viewport.X - boardSize.X) / 2f, 70f + FeederHeight + Cell + Gap);
    }

    private void Step()
    {
        _from.Clear();
        for (var tile = 0; tile < _game.Belt.Count; tile++)
            if (_game.Belt[tile] is { } pixel)
                _from[pixel.Id] = TileCenter(_game.Layout.Tiles[tile]);

        _game.Tick();
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var bar = new HBoxContainer { Position = new Vector2(16, 16) };
        bar.AddThemeConstantOverride("separation", 8);
        layer.AddChild(bar);

        _speedButton = new Button();
        _speedButton.Pressed += () =>
        {
            _speedIndex = (_speedIndex + 1) % Speeds.Length;
            UpdateSpeedLabel();
        };
        UpdateSpeedLabel();
        bar.AddChild(_speedButton);

        var restart = new Button { Text = "Restart" };
        restart.Pressed += StartGame;
        bar.AddChild(restart);

        var shuffle = new Button { Text = "New shuffle" };
        shuffle.Pressed += () =>
        {
            _seed++;
            StartGame();
        };
        bar.AddChild(shuffle);
    }

    private void UpdateSpeedLabel() => _speedButton.Text = $"Speed {Speeds[_speedIndex]}x";

    private void DrawBelt()
    {
        var board = _game.Board;
        var nearlyFull = _game.InTransit >= _game.Capacity * 0.8f;

        var outer = new Rect2(
            _origin - new Vector2(Gap + Cell, Gap + Cell),
            new Vector2(board.Width, board.Height) * Cell + new Vector2(Gap + Cell, Gap + Cell) * 2);
        DrawRect(outer.Grow(4), nearlyFull ? Warning : Track);
        DrawRect(outer, Track);
        DrawRect(outer.Grow(-Cell), nearlyFull ? Warning : Track);
        DrawRect(outer.Grow(-Cell - 4), Background);

        foreach (var tile in _game.Layout.Tiles)
        {
            var c = TileCenter(tile);
            DrawRect(new Rect2(c - new Vector2(Cell, Cell) * 0.3f, new Vector2(Cell, Cell) * 0.6f), TrackTile);
        }
    }

    private void DrawHints()
    {
        var board = _game.Board;
        var right = _origin.X + board.Width * Cell;
        var bottom = _origin.Y + board.Height * Cell;

        for (var x = 0; x < board.Width; x++)
        {
            var left = _origin.X + x * Cell;
            DrawRect(new Rect2(left, _origin.Y - HintWidth, Cell, HintWidth), TargetColor(new GridPos(x, 0)));
            DrawRect(new Rect2(left, bottom, Cell, HintWidth), TargetColor(new GridPos(x, board.Height - 1)));
        }

        for (var y = 0; y < board.Height; y++)
        {
            var top = _origin.Y + y * Cell;
            DrawRect(new Rect2(_origin.X - HintWidth, top, HintWidth, Cell), TargetColor(new GridPos(0, y)));
            DrawRect(new Rect2(right, top, HintWidth, Cell), TargetColor(new GridPos(board.Width - 1, y)));
        }
    }

    private void DrawCells()
    {
        var board = _game.Board;
        for (var y = 0; y < board.Height; y++)
        for (var x = 0; x < board.Width; x++)
        {
            var cell = new GridPos(x, y);
            var center = _origin + new Vector2(x + 0.5f, y + 0.5f) * Cell;
            if (board.IsEmpty(cell))
            {
                var slot = TargetColor(cell).Darkened(0.45f);
                DrawRect(new Rect2(center - Vector2.One * Cell / 2f, Vector2.One * Cell), slot);
                DrawArc(center, Cell * 0.26f, 0, Mathf.Tau, 24, slot.Lightened(0.12f), 2f);
            }
            else
            {
                DrawBrick(center, ToColor(_level.Palette[board.OccupantAt(cell)]));
            }
        }
    }

    private void DrawFeeder()
    {
        var entry = TileCenter(_game.Layout.Tiles[0]);
        var box = new Rect2(entry.X - Cell * 0.8f, entry.Y - Cell / 2f - FeederHeight, Cell * 1.6f, FeederHeight);
        var full = _game.InTransit >= _game.Capacity * 0.8f;

        DrawRect(box, Color.Color8(0xB8, 0xBC, 0xD6));
        DrawRect(box, Track, false, 2f);
        DrawString(ThemeDB.FallbackFont, new Vector2(box.Position.X, box.End.Y - 8),
            $"{_game.InTransit}/{_game.Capacity}", HorizontalAlignment.Center, box.Size.X, 16,
            full ? Warning : Track);
    }

    private void DrawBeltPixels()
    {
        var t = (float)_sinceTick;
        var feederMouth = TileCenter(_game.Layout.Tiles[0]) - new Vector2(0, Cell);

        for (var tile = 0; tile < _game.Belt.Count; tile++)
        {
            if (_game.Belt[tile] is not { } pixel)
                continue;

            var to = TileCenter(_game.Layout.Tiles[tile]);
            var from = _from.TryGetValue(pixel.Id, out var f) ? f : feederMouth;
            DrawBrick(from.Lerp(to, t), ToColor(_level.Palette[pixel.Color]), 0.85f);
        }
    }

    private void DrawStatus(Vector2 viewport)
    {
        if (_game.Status == GameStatus.Playing)
            return;

        var message = _game.Status == GameStatus.Won ? "Picture complete!" : "The belt jammed";
        DrawRect(new Rect2(0, viewport.Y / 2f - 60, viewport.X, 120), new Color(0, 0, 0, 0.6f));
        DrawString(ThemeDB.FallbackFont, new Vector2(0, viewport.Y / 2f + 6), message,
            HorizontalAlignment.Center, viewport.X, 36, Colors.White);
        DrawString(ThemeDB.FallbackFont, new Vector2(0, viewport.Y / 2f + 40), "Restart or try a new shuffle",
            HorizontalAlignment.Center, viewport.X, 18, Colors.White);
    }

    private void DrawBrick(Vector2 center, Color color, float scale = 1f)
    {
        var size = Cell * scale - 2f;
        DrawRect(new Rect2(center - Vector2.One * size / 2f, Vector2.One * size), color);
        DrawCircle(center, size * 0.27f, color.Lightened(0.15f));
        DrawArc(center, size * 0.27f, 0, Mathf.Tau, 24, color.Darkened(0.25f), 1.5f);
    }

    private Vector2 TileCenter(GridPos tile)
    {
        var board = _game.Board;
        return new Vector2(Axis(tile.X, board.Width, _origin.X), Axis(tile.Y, board.Height, _origin.Y));

        static float Axis(int i, int count, float start) =>
            i < 0 ? start - Gap - Cell / 2f
            : i >= count ? start + count * Cell + Gap + Cell / 2f
            : start + (i + 0.5f) * Cell;
    }

    private Color TargetColor(GridPos cell) => ToColor(_level.Palette[_game.Board.TargetAt(cell)]);

    private static Color ToColor(Rgb c) => Color.Color8(c.R, c.G, c.B);
}
