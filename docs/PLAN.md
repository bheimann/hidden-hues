# Hidden Hues: Build Plan

The plan has three phases:

1. **Prototype:** one hard-coded level that proves the mechanics feel right.
2. **Generator:** levels built from any picture.
3. **Random pictures:** kid-safe images pulled from the web.

Each phase ends with something playable.

## Decisions so far

- **Engine:** Godot 4.7.2 (.NET build) with C#. The editor is at `C:\Dev\tools\Godot\Godot_v4.7.2-stable_mono_win64`.
- **Levels:** generated from higher-resolution pictures. You choose the grid size and the number of colors.
- **Scramble:** the game shuffles the top layer and checks that the level can be solved.
- **Picture sources:** your own image files first, random kid-safe web pictures later.
- **Layout:** one grid with the conveyor around it. There are no extra trays and no help tools.

## Game rules (the spec the code follows)

**Board**
- The board is a W×H grid of cells. Each cell has a target color (the hidden picture) and holds either one pixel or nothing.
- At the start every cell holds a pixel. The pixels are a shuffle of the target colors, so every color appears exactly as often as it's needed.

**Belt and feeder**
- The belt is a loop of tiles running clockwise around the grid. There's one tile beside each edge cell, plus the corners.
- The feeder sits at the top of the loop.
- The capacity (30 in the reference level) limits the pixels in the feeder and on the belt combined.

**Picking**
- Clicking a pixel takes that pixel and every pixel of the same color connected to it (up, down, left or right). Pixels already sitting in a matching slot can still be taken.
- Pixels are taken in breadth-first order from the clicked cell, until the region runs out or the capacity is reached. Anything left over stays on the grid.
- You can pick again at any time, as long as there's room.
- An empty cell shows its target color in a darker shade, so you can see what belongs there.

**Movement**
- Each tick, the belt advances one tile.
- **Speeds:** 1×, 2× and 4×. At 1× a tick is 1 second (one tile per second), so 2× is one tile every 0.5 s and 4× one every 0.25 s. This is a starting value that's easy to tune later.
- The feeder releases one pixel onto its entry tile whenever that tile is free.

**Placement**
- **Home tile:** every cell has one, the belt tile directly across from it on the nearest side of the grid.
- **Equal distances:** a cell that is the same distance from two or more sides has a home tile on each of them. Whichever of those tiles a matching pixel reaches first fills it.
- **When a pixel drops:** when it's on a tile that is home to one or more empty cells of its color, it drops into one of them and leaves the belt.
- **If several match:** the pixel takes the one closest to the belt.
- **Blocking:** nothing blocks the path. Other pixels on the grid don't stop it.

**Hints**
- Each segment of the grid's outline shows the hidden slot color (the target color) of the edge cell beside it.

**Winning and losing**
- **Win:** every cell holds a pixel of its target color.
- **Lose:** the feeder and belt are at capacity, and no pixel in either has an empty matching cell left. At that point nothing can ever drop and you can't pick, so the game is stuck.

## Project layout

```
hidden-hues/
  HiddenHues.sln
  src/HiddenHues.Core/          pure C# game logic, no Godot references
  src/HiddenHues.Core.Tests/    xUnit tests for the rules above
  game/                         Godot project (project.godot, HiddenHues.csproj, scenes, scripts)
```

All the rules go in `HiddenHues.Core`. The Godot project draws the board, handles input and calls into Core each tick. This keeps the rules testable with `dotnet test`, so you don't need to launch Godot to check them. The phase 2 solver will also run the same simulation with no rendering.

`HiddenHues.Core` targets the same .NET version as the Godot-generated csproj, and the game project references it with a `ProjectReference`.

## Phase 1: prototype

Goal: one hard-coded level that's playable from start to win or loss.

1. **Project setup**
   - Create the Godot C# project in `game/` using the editor.
   - Add the Core and test projects and the solution file.
   - Confirm a C# script runs in the game.
2. **Core model**
   - Add `Board` (cells, target colors, occupants) and `RegionPicker` (flood fill capped by available room).
   - Add `Belt` (tile loop, cell-to-home-tile map, feeder queue) and `Game` (a tick-based simulation with pick, tick and win/lose status).
3. **Tests**
   - Flood fill: connectivity, the capacity cap and breadth-first order.
   - Placement: the home-tile mapping (including cells with more than one home tile), the closest-cell rule, and picking while the belt is moving.
   - Win and lose: both detected correctly.
4. **Hard-coded level**
   - Store an 11×14 picture as rows of palette characters in code, based on the reference screenshot.
   - Shuffle it with a fixed seed. There's no solvability check yet.
5. **Rendering**
   - Use one `Node2D` with custom `_Draw()`. That's simpler than TileMaps for a prototype.
   - Draw the pixels as studded squares and the empty cells as darker slots.
   - Draw the belt as a rounded track. Pixels slide smoothly between tiles: the logic ticks on a fixed timer and the drawing interpolates between ticks.
   - Show the feeder counter (`n/30`) and the colored outline hints.
   - Turn the outline red when the belt is nearly full.
6. **Input and states**
   - Clicking a cell picks it.
   - Show a win or lose overlay with a restart button.
   - Add a 1×/2×/4× speed toggle. It also speeds up testing.

Done when: you can play the level to a win, deliberately overfill the belt to lose, and all Core tests pass.

## Phase 2: levels from pictures

1. **Image to grid**
   - Load a PNG or JPG and crop it to the grid's aspect ratio.
   - Downscale by averaging each block of source pixels into one cell, which gives cleaner results than resampling for pixel art.
   - Grid size is a setting, for example a long side of 10 to 24 cells.
2. **Color reduction**
   - Run k-means in CIELAB color space, seeded with k-means++ for repeatable results, with N = the chosen color count.
   - Merge palette colors that are too similar, so every color is easy to tell apart.
   - Optionally clean up lone specks with a majority filter.
   - Show a preview before playing.
3. **Solvable scramble**
   - Shuffle the pixels, then run a solver on the Core simulation.
   - The solver uses a "settled" model: it waits for the belt to clear before each pick. Any level solvable that way can also be solved by a real player, who can always wait.
   - Search over the distinct regions you can pick, using depth-first search, memory of states already seen, and pruning.
   - Reshuffle until a solution is found.
   - Difficulty settings: capacity, grid size and color count.
4. **Level files**
   - Save levels as JSON in Godot's `user://` folder: palette, target grid, scrambled grid, capacity and seed.
5. **Generator screen**
   - A file picker, sliders for size and colors, a preview, then Play.

## Phase 3: random kid-safe pictures

- **Recommended source:** the Pixabay API.
  - It's free with an API key and has a `safesearch=true` filter.
  - It also has `image_type=illustration` or `vector`. Flat illustrations reduce to a few colors much better than photos.
  - Combine those with kid-friendly categories such as animals, food and nature.
  - Check their current terms. Last I knew they asked apps to say images come from Pixabay and limited how long results can be cached.
- **Alternatives:**
  - Openverse has a filter that leaves out mature content.
  - Lorem Picsum needs no key but has no safety filter, so I don't recommend it on its own.
- **Safety:**
  - No filter is perfect, so show the reduced preview before play. Optionally, add a "parent approves" step, or only use pictures someone has already approved and cached.
  - Bundle a few local pictures as an offline fallback.
- **API key:** store it in a user settings file that's never committed.
- **Downloads:** use Godot's `HTTPRequest`. A medium-size image (around 640 px) is plenty, because it gets reduced to about 20 cells across.

## Later

- **Menus and progress:** a main menu, a level list and saved progress.
- **Feel:** sound and animation polish.
- **Design scrambles:** for example stripes, rings or colors sorted by hue, instead of a random shuffle.

- **Difficulty setting:** an in-game option that changes capacity. The default is 30, and a smaller capacity makes a level harder.
