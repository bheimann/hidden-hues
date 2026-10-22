using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HiddenHues.Core;
using HiddenHues.Core.Generation;
using HiddenHues.Core.Pictures;

namespace HiddenHues;

/// <summary>
/// Pick a picture (your own, or a random kid-safe one from Pixabay), choose the grid size, colors and capacity,
/// preview the result, then play it. Generated puzzles are saved to user://levels so they can be opened again.
/// </summary>
public partial class GeneratorScreen : Control
{
    private const string LevelsDir = "user://levels";
    private const string PixabayCacheDir = "user://cache/pixabay";

    // Large pictures are shrunk to this before reducing, which keeps slider changes quick.
    private const int MaxSourceSide = 512;

    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly Color Ink = Color.Color8(0x2C, 0x2F, 0x45);

    private PixelImage? _image;
    private string _pictureName = "Picture";
    private PictureHit? _hit;
    private RandomPictures? _random;

    private HSlider _size = null!;
    private HSlider _colors = null!;
    private HSlider _capacity = null!;
    private OptionButton _style = null!;
    private CheckBox _despeckle = null!;
    private TextureRect _original = null!;
    private PreviewView _preview = null!;
    private LinkButton _credit = null!;
    private Button _notThisOne = null!;
    private Label _status = null!;
    private Button _play = null!;
    private Button _randomButton = null!;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildUi();

        string? capture = null;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            // `-- --image=<file>` loads a picture on start, `--random` fetches a random one, `--play` then
            // presses Play, and `--capture-generator=<file.png>` screenshots this screen.
            if (arg.StartsWith("--image="))
                LoadPictureFile(arg["--image=".Length..]);
            else if (arg.StartsWith("--capture-generator="))
                capture = arg["--capture-generator=".Length..];
        }

        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--random"))
            NextRandomPicture(then: capture is null && args.Contains("--play") ? PlayGenerated : null,
                captureTo: capture);
        else if (capture is not null)
            CaptureAsync(capture);
        else if (_image is not null && args.Contains("--play"))
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
        title.AddThemeColorOverride("font_color", Ink);
        column.AddChild(title);

        var sources = new HFlowContainer();
        sources.AddThemeConstantOverride("h_separation", 8);
        sources.AddThemeConstantOverride("v_separation", 8);
        column.AddChild(sources);
        _randomButton = MakeButton("Random picture", () => NextRandomPicture());
        sources.AddChild(_randomButton);
        sources.AddChild(MakeButton("Choose picture…", ChoosePicture));
        sources.AddChild(MakeButton("Open saved level…", OpenSavedLevel));
        sources.AddChild(MakeButton("Play sample", PlaySample));
        sources.AddChild(MakeButton("Pixabay key…", () => AskForApiKey()));

        var pictures = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        pictures.AddThemeConstantOverride("separation", 8);
        column.AddChild(pictures);
        _original = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false,
        };
        pictures.AddChild(_original);
        _preview = new PreviewView
        {
            CustomMinimumSize = new Vector2(0, 300),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        pictures.AddChild(_preview);

        var creditRow = new HBoxContainer();
        creditRow.AddThemeConstantOverride("separation", 12);
        column.AddChild(creditRow);
        _credit = new LinkButton { Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _credit.Pressed += () =>
        {
            if (_hit is not null)
                OS.ShellOpen(_hit.PageUrl);
        };
        creditRow.AddChild(_credit);
        _notThisOne = MakeButton("Not this one", HideCurrentPicture);
        _notThisOne.TooltipText = "Never show this picture again, and get another";
        _notThisOne.Visible = false;
        creditRow.AddChild(_notThisOne);

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
            _despeckle.AddThemeColorOverride(state, Ink);
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

    // ---- Random pictures from Pixabay ----

    private async void NextRandomPicture(Action? then = null, string? captureTo = null)
    {
        if (Settings.ApiKey.Length == 0)
        {
            AskForApiKey(afterSave: () => NextRandomPicture(then, captureTo));
            if (captureTo is not null)
                CaptureAsync(captureTo);
            return;
        }

        _random ??= new RandomPictures(
            new PixabayClient(Http, Settings.ApiKey, ProjectSettings.GlobalizePath(PixabayCacheDir)),
            Settings.HiddenPictureIds);

        _randomButton.Disabled = true;
        _status.Text = "Finding a picture…";
        try
        {
            var next = await _random.NextAsync();
            if (next is not { } picture)
            {
                _status.Text = "No new pictures right now. Try again in a moment.";
                return;
            }

            var image = DecodeImage(picture.Bytes, Path.GetExtension(new Uri(picture.Hit.WebformatUrl).AbsolutePath));
            if (image is null)
            {
                _status.Text = "That picture couldn't be opened. Try another.";
                return;
            }

            ShowPicture(image, picture.Hit.Tags.FirstOrDefault() ?? "Picture", picture.Hit);
            then?.Invoke();
        }
        catch (PixabayException e)
        {
            _status.Text = $"{e.Message} Play sample works offline.";
        }
        catch (TaskCanceledException)
        {
            _status.Text = "Pixabay took too long to answer. Play sample works offline.";
        }
        finally
        {
            _randomButton.Disabled = false;
            if (captureTo is not null)
                CaptureAsync(captureTo);
        }
    }

    private void HideCurrentPicture()
    {
        if (_hit is null || _random is null)
            return;

        _random.Hide(_hit.Id);
        Settings.HiddenPictureIds = _random.HiddenIds.ToList();
        NextRandomPicture();
    }

    private void AskForApiKey(Action? afterSave = null)
    {
        var dialog = new ConfirmationDialog { Title = "Pixabay API key", OkButtonText = "Save" };
        var box = new VBoxContainer();
        box.AddChild(new Label
        {
            Text = "Random pictures come from Pixabay. Get a free key at pixabay.com/api/docs\n" +
                   "(sign in, and the key is shown on that page), then paste it here.",
        });
        var entry = new LineEdit { Text = Settings.ApiKey, Secret = true, PlaceholderText = "API key" };
        box.AddChild(entry);
        dialog.AddChild(box);
        dialog.Confirmed += () =>
        {
            Settings.ApiKey = entry.Text;
            _random = null; // rebuilt with the new key
            if (entry.Text.Trim().Length > 0)
                afterSave?.Invoke();
        };
        dialog.VisibilityChanged += () =>
        {
            if (!dialog.Visible)
                dialog.QueueFree();
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(520, 160));
        entry.GrabFocus();
    }

    // ---- Your own pictures ----

    private void ChoosePicture()
    {
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Filters = new[] { "*.png, *.jpg, *.jpeg, *.webp ; Pictures" },
        };
        dialog.FileSelected += LoadPictureFile;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(700, 500));
    }

    private void LoadPictureFile(string path)
    {
        var image = Image.LoadFromFile(path);
        if (image is null || image.IsEmpty())
        {
            _status.Text = $"Couldn't open {Path.GetFileName(path)}.";
            return;
        }

        ShowPicture(image, Path.GetFileNameWithoutExtension(path), hit: null);
    }

    private static Image? DecodeImage(byte[] bytes, string extension)
    {
        var image = new Image();
        var result = extension.ToLowerInvariant() switch
        {
            ".png" => image.LoadPngFromBuffer(bytes),
            ".jpg" or ".jpeg" => image.LoadJpgFromBuffer(bytes),
            ".webp" => image.LoadWebpFromBuffer(bytes),
            _ => Error.FileUnrecognized,
        };
        return result == Error.Ok && !image.IsEmpty() ? image : null;
    }

    private void ShowPicture(Image image, string name, PictureHit? hit)
    {
        var longest = Math.Max(image.GetWidth(), image.GetHeight());
        if (longest > MaxSourceSide)
        {
            var scale = (float)MaxSourceSide / longest;
            image.Resize(Math.Max(1, (int)(image.GetWidth() * scale)), Math.Max(1, (int)(image.GetHeight() * scale)),
                Image.Interpolation.Cubic);
        }

        image.Convert(Image.Format.Rgba8);
        _image = new PixelImage(image.GetWidth(), image.GetHeight(), image.GetData());
        _pictureName = name;
        _hit = hit;

        _original.Texture = ImageTexture.CreateFromImage(image);
        _original.Visible = true;
        _credit.Visible = hit is not null;
        _credit.Text = hit?.Credit ?? "";
        _notThisOne.Visible = hit is not null;
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

    // ---- Playing ----

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

        var saved = new SavedPuzzle(puzzle, _pictureName, _hit?.Credit, _hit?.PageUrl);
        SaveLevel(saved);
        Play(saved, style);
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

        Play(new SavedPuzzle(puzzle, "Sample"), ShuffleStyle.Clumps);
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
                Play(PuzzleFile.FromJson(File.ReadAllText(ProjectSettings.GlobalizePath(path))), ShuffleStyle.Clumps);
            }
            catch (Exception e) when (e is FormatException or IOException or System.Text.Json.JsonException)
            {
                _status.Text = $"Couldn't open that level: {e.Message}";
            }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(600, 420));
    }

    private static void SaveLevel(SavedPuzzle saved)
    {
        var dir = ProjectSettings.GlobalizePath(LevelsDir);
        Directory.CreateDirectory(dir);
        var safeName = string.Concat(saved.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var level = saved.Puzzle.Level;
        var file = Path.Combine(dir, $"{safeName}-{level.Width}x{level.Height}-{saved.Puzzle.Seed}.json");
        File.WriteAllText(file, PuzzleFile.ToJson(saved.Puzzle, saved.Name, saved.Credit, saved.CreditUrl));
    }

    private void Play(SavedPuzzle saved, ShuffleStyle style)
    {
        Session.Puzzle = saved.Puzzle;
        Session.Name = saved.Name;
        Session.Credit = saved.Credit;
        Session.CreditUrl = saved.CreditUrl;
        Session.Style = style;
        GetTree().ChangeSceneToFile("res://scenes/Play.tscn");
    }

    private static Button MakeButton(string text, Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        return button;
    }

    private static Label MakeLabel(string text)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", Ink);
        return label;
    }
}
