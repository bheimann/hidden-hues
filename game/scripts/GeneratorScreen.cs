using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using HiddenHues.Core;
using HiddenHues.Core.Generation;

namespace HiddenHues;

/// <summary>
/// Pick a picture, choose the grid size, colors and capacity, preview the result, then play it.
/// Generated puzzles are saved to user://levels so they can be opened again.
/// </summary>
public partial class GeneratorScreen : Control
{
    private const string LevelsDir = "user://levels";

    // Large pictures are shrunk to this before reducing, which keeps slider changes quick.
    private const int MaxSourceSide = 512;

    private PixelImage? _image;
    private string _pictureName = "Picture";

    private HSlider _size = null!;
    private HSlider _colors = null!;
    private HSlider _capacity = null!;
    private OptionButton _style = null!;
    private CheckBox _despeckle = null!;
    private PreviewView _preview = null!;
    private Label _status = null!;
    private Button _play = null!;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildUi();

        string? capture = null;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            // `-- --image=<file>` loads a picture on start, `--play` then presses Play,
            // and `--capture-generator=<file.png>` screenshots this screen.
            if (arg.StartsWith("--image="))
                LoadPicture(arg["--image=".Length..]);
            else if (arg.StartsWith("--capture-generator="))
                capture = arg["--capture-generator=".Length..];
        }

        if (capture is not null)
            CaptureAsync(capture);
        else if (_image is not null && Array.IndexOf(OS.GetCmdlineUserArgs(), "--play") >= 0)
            PlayGenerated();
    }

    private async void CaptureAsync(string path)
    {
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(path);
        GetTree().Quit();
    }

    private void BuildUi()
    {
        AddChild(new ColorRect { Color = Color.Color8(0xE6, 0xE8, 0xF2), AnchorRight = 1, AnchorBottom = 1 });

        var margin = new MarginContainer { AnchorRight = 1, AnchorBottom = 1 };
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 20);
        AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);

        var title = new Label { Text = "Hidden Hues" };
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", Color.Color8(0x2C, 0x2F, 0x45));
        column.AddChild(title);

        var sources = new HBoxContainer();
        sources.AddThemeConstantOverride("separation", 8);
        column.AddChild(sources);
        sources.AddChild(MakeButton("Choose picture…", ChoosePicture));
        sources.AddChild(MakeButton("Open saved level…", OpenSavedLevel));
        sources.AddChild(MakeButton("Play sample", PlaySample));

        _preview = new PreviewView { CustomMinimumSize = new Vector2(0, 340), SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(_preview);

        _size = AddSlider(column, "Grid size", 8, 32, 16, v => $"{v} cells");
        _colors = AddSlider(column, "Colors", 2, 12, 6, v => $"up to {v}");
        _capacity = AddSlider(column, "Capacity", 10, 60, Game.DefaultCapacity, v => $"{v} pixels");

        var options = new HBoxContainer();
        options.AddThemeConstantOverride("separation", 16);
        column.AddChild(options);
        options.AddChild(MakeLabel("Shuffle"));
        _style = new OptionButton();
        _style.AddItem("Clumps");
        _style.AddItem("Random");
        options.AddChild(_style);
        _despeckle = new CheckBox { Text = "Clean up specks", ButtonPressed = true };
        foreach (var state in new[] { "font_color", "font_pressed_color", "font_hover_color", "font_hover_pressed_color", "font_focus_color" })
            _despeckle.AddThemeColorOverride(state, Color.Color8(0x2C, 0x2F, 0x45));
        _despeckle.Toggled += _ => UpdatePreview();
        options.AddChild(_despeckle);

        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 12);
        column.AddChild(bottom);
        _play = MakeButton("Play", PlayGenerated);
        _play.CustomMinimumSize = new Vector2(120, 40);
        _play.Disabled = true;
        bottom.AddChild(_play);
        _status = MakeLabel("");
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bottom.AddChild(_status);
    }

    private HSlider AddSlider(Container parent, string name, int min, int max, int value, Func<int, string> describe)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        parent.AddChild(row);

        var label = MakeLabel(name);
        label.CustomMinimumSize = new Vector2(90, 0);
        row.AddChild(label);

        var slider = new HSlider
        {
            MinValue = min, MaxValue = max, Step = 1, Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        row.AddChild(slider);

        var shown = MakeLabel(describe(value));
        shown.CustomMinimumSize = new Vector2(90, 0);
        row.AddChild(shown);

        slider.ValueChanged += v =>
        {
            shown.Text = describe((int)v);
            UpdatePreview();
        };
        return slider;
    }

    private void ChoosePicture()
    {
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Filters = new[] { "*.png, *.jpg, *.jpeg, *.webp ; Pictures" },
        };
        dialog.FileSelected += LoadPicture;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(700, 500));
    }

    private void LoadPicture(string path)
    {
        var image = Image.LoadFromFile(path);
        if (image is null || image.IsEmpty())
        {
            _status.Text = $"Couldn't open {Path.GetFileName(path)}.";
            return;
        }

        var longest = Math.Max(image.GetWidth(), image.GetHeight());
        if (longest > MaxSourceSide)
        {
            var scale = (float)MaxSourceSide / longest;
            image.Resize(Math.Max(1, (int)(image.GetWidth() * scale)), Math.Max(1, (int)(image.GetHeight() * scale)),
                Image.Interpolation.Cubic);
        }

        image.Convert(Image.Format.Rgba8);
        _image = new PixelImage(image.GetWidth(), image.GetHeight(), image.GetData());
        _pictureName = Path.GetFileNameWithoutExtension(path);
        _play.Disabled = false;
        UpdatePreview();
    }

    private Level? Reduce() => _image is null
        ? null
        : PictureReducer.Reduce(_image, new ReduceOptions
        {
            LongSide = (int)_size.Value,
            Colors = (int)_colors.Value,
            Despeckle = _despeckle.ButtonPressed,
        });

    private void UpdatePreview()
    {
        var level = Reduce();
        _preview.Level = level;
        if (level is not null)
            _status.Text = $"{_pictureName}: {level.Width}×{level.Height} grid, {level.Palette.Count} colors";
    }

    private async void PlayGenerated()
    {
        if (Reduce() is not { } level)
            return;

        var style = (ShuffleStyle)_style.Selected;
        var capacity = (int)_capacity.Value;
        var seed = (int)(Time.GetUnixTimeFromSystem() % 100_000);
        var puzzle = await GenerateInBackground(() => Puzzle.Generate(level, style, capacity, seed));
        if (puzzle is null)
        {
            _status.Text = "Couldn't find a solvable shuffle. Try more capacity, fewer colors or a smaller grid.";
            return;
        }

        SaveLevel(puzzle, _pictureName);
        Play(puzzle, _pictureName, style);
    }

    private async void PlaySample()
    {
        var capacity = (int)_capacity.Value;
        var puzzle = await GenerateInBackground(() => Puzzle.Generate(PrototypeLevel.Create(), ShuffleStyle.Clumps, capacity));
        if (puzzle is null)
        {
            _status.Text = "Couldn't find a solvable shuffle for the sample at this capacity.";
            return;
        }

        Play(puzzle, "Sample", ShuffleStyle.Clumps);
    }

    /// <summary>Runs the solver off the main thread so the window stays responsive. Resumes on the main thread.</summary>
    private async Task<Puzzle?> GenerateInBackground(Func<Puzzle?> generate)
    {
        _play.Disabled = true;
        _status.Text = "Finding a solvable shuffle…";
        try
        {
            return await Task.Run(generate);
        }
        finally
        {
            _play.Disabled = _image is null;
        }
    }

    private void OpenSavedLevel()
    {
        DirAccess.MakeDirRecursiveAbsolute(LevelsDir);
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Userdata,
            CurrentDir = LevelsDir,
            Filters = new[] { "*.json ; Saved levels" },
        };
        dialog.FileSelected += path =>
        {
            try
            {
                var (puzzle, name) = PuzzleFile.FromJson(File.ReadAllText(ProjectSettings.GlobalizePath(path)));
                Play(puzzle, name, ShuffleStyle.Clumps);
            }
            catch (Exception e) when (e is FormatException or IOException or System.Text.Json.JsonException)
            {
                _status.Text = $"Couldn't open that level: {e.Message}";
            }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(600, 420));
    }

    private static void SaveLevel(Puzzle puzzle, string name)
    {
        var dir = ProjectSettings.GlobalizePath(LevelsDir);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{name}-{puzzle.Level.Width}x{puzzle.Level.Height}-{puzzle.Seed}.json");
        File.WriteAllText(file, PuzzleFile.ToJson(puzzle, name));
    }

    private void Play(Puzzle puzzle, string name, ShuffleStyle style)
    {
        Session.Puzzle = puzzle;
        Session.Name = name;
        Session.Style = style;
        GetTree().ChangeSceneToFile("res://scenes/Play.tscn");
    }

    private Button MakeButton(string text, Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        return button;
    }

    private static Label MakeLabel(string text)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", Color.Color8(0x2C, 0x2F, 0x45));
        return label;
    }
}
