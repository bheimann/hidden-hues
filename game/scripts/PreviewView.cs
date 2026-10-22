using Godot;
using HiddenHues.Core;

namespace HiddenHues;

/// <summary>Shows a level's hidden picture as flat cells, scaled to fit.</summary>
public partial class PreviewView : Control
{
    private Level? _level;

    public Level? Level
    {
        get => _level;
        set
        {
            _level = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), Color.Color8(0xD4, 0xD7, 0xE6));

        if (_level is null)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(0, Size.Y / 2f), "Choose a picture to see it as pixels",
                HorizontalAlignment.Center, Size.X, 18, Color.Color8(0x4A, 0x4E, 0x66));
            return;
        }

        var cell = Mathf.Floor(Mathf.Min(Size.X / _level.Width, Size.Y / _level.Height));
        var origin = (Size - new Vector2(_level.Width, _level.Height) * cell) / 2f;
        for (var y = 0; y < _level.Height; y++)
        for (var x = 0; x < _level.Width; x++)
        {
            var color = BoardView.ToColor(_level.Palette[_level.Target[y * _level.Width + x]]);
            DrawRect(new Rect2(origin + new Vector2(x, y) * cell, Vector2.One * cell), color);
        }
    }
}
