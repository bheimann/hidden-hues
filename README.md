# Hidden Hues

A pixel-art puzzle game for PC, built with Godot. A scrambled pixel-art picture sits on top of a hidden one. Pixels ride a conveyor and drop into the spots that match their color, revealing the picture underneath. If the conveyor fills up, you lose.

It's inspired by the iOS game "Brick Factory," but without the pay-to-progress walls. The aim is to keep the focus on revealing pictures and real strategy.

## Gameplay

- **The board:** a grid of pixels sits on top of a hidden picture, and a conveyor loops around it. The pixels on top might be a picture, a pretty design, or just a random shuffle.
- **Picking:** click a pixel. It and every connected pixel of the same color go into the feeder, up to the feeder's capacity (30 in the example level).
- **Revealing:** taking pixels away uncovers slots underneath, and each slot's color shows what belongs there.
- **Riding:** the feeder puts pixels on the conveyor one at a time. Each one rides around the loop until it passes an open slot of its own color, then drops in.
- **Hints:** the grid's outline is colored to match the pixel on that edge.
- **Winning:** you win when every pixel sits in a slot of its own color and the hidden picture is complete.
- **Losing:** if the conveyor fills up with pixels that have nowhere to go, you lose.
- **No help tools:** there are no boosters, extra slots or undo. Every level has to be solvable through planning alone.

The core skill is choosing which region to pick next. A big region is safe when its matching slots are already exposed, and risky when its pixels will have to circle the belt waiting for a home.

## Running it

- **Play:** open `game/project.godot` in Godot 4.7 (.NET build) and press Play.
- **Tests:** run `dotnet test` from the repo root. They cover the game rules in `src/HiddenHues.Core`.

See [docs/PLAN.md](docs/PLAN.md) for the build plan.
