<div align="center">

<img src="docs/images/KrevetaLogo.png" alt="Logo" width="25%">

<br/>

# 🦐 Kreveta Chess Engine

[![.NET](https://img.shields.io/badge/.NET-9.0-lightgreen?style=for-the-badge)](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
[![License](https://img.shields.io/github/license/ZlomenyMesic/Kreveta?style=for-the-badge&label=license&color=lightblue)](https://github.com/ZlomenyMesic/Kreveta/blob/master/LICENSE)
[![Commits](https://img.shields.io/github/commit-activity/m/ZlomenyMesic/Kreveta?style=for-the-badge&logo=github&color=lightsalmon&label=commits)](https://github.com/ZlomenyMesic/Kreveta/graphs/commit-activity)

</div>

## About

An amateur, UCI-compatible chess engine written entirely in C#.
Originally developed as a hobby project, but now also serves as the
basis for my SOČ thesis. Designed to balance strength, speed and
reliability, while still aiming for code clarity and proper
documentation, making it ideal for others to study, experiment
with, or use as a foundation for future engines.
Development started on **March 3, 2025**.

---

### Elo Ratings

| Version | Release      | Site    | Time Control | Games | Elo rating |
|---------|--------------|---------|--------------|-------|------------|
| 1.2.3   | Oct 31, 2025 | CCRL    | Blitz 2+1    | 934   | 1737       |
| 2.0.0   | Dec 1, 2025  | CCRL    | Blitz 2+1    | 939   | 1945       |
| ...     | ...          | ...     | ...          | ...   | ...        |
| 2.2.2   | Jan 4, 2026  | Lichess | Bullet       | 701   | 2225       |
| 2.2.2   | Jan 4, 2026  | Lichess | Blitz        | 467   | 2195       |
| 2.2.2   | Jan 4, 2026  | Lichess | Rapid        | 151   | 2241       |
| 2.2.2   | Jan 4, 2026  | Lichess | Classical    | 12    | 2276       |

---

## Engine Internals

### Move Generation

- Bitboard representation of positions
- Precomputed slider tables with PEXT-based lookups
- King star logic for check situations

### Search Features

- Principal Variation Search with Alpha-Beta Pruning
- Quiescence search for leaf nodes
- NMP, RFP, Razoring, FP and LMP
- Delta Pruning and SEE Pruning for QSearch
- Mate Distance Pruning (MDP)
- Fractional reductions
- Lazy move ordering
- Killers, capture killers and countermoves
- Quiet, capture and 2-ply continuation histories
- Pawn, king, minor piece and major piece corrections
- Resizable transposition table (1-1000 MiB)
- Rational time management

### Static Evaluation

- **Classical part**
  - PST with tapering evaluation
  - Pawn structure (doubling, isolation, blocking, passed pawns)
  - King safety based on friendly protection
- **NNUE**
  - 128->16->16->1 architecture
  - 8 subnets/buckets based on piece count
  - Trained on ~143M self-play positions

### Others

- Generic Polyglot opening book support
- Fancy statistics printing
- Custom [parameter tuning](https://github.com/ZlomenyMesic/Kreveta/tree/master/KrevetaTuning) project

---

## UCI Protocol Support

Kreveta fully supports the **Universal Chess Interface (UCI)** protocol.
For a more detailed explanation, visit the [documentation](https://gist.github.com/DOBRO/2592c6dad754ba67e6dcaec8c90165bf).
Below are the implemented commands and their usage:

### `uci`

Sent by the GUI to ensure the engine uses the UCI protocol. The engine responds with `id name`, `id author`, a list of all supported options and eventually `uciok`, which lets the GUI know UCI is supported.

### `isready`

Used by the GUI to check whether the engine is ready to respond to further commands. If the engine is ready, it shall respond with `readyok`.

### `ucinewgame`

Signals that the engine will be playing a whole game of chess, instead of just analyzing a single position. This allows the engine to toggle various options, depending on the current task. The engine doesn't respond with anything.

### `position [fen | startpos] moves ...`

Used to set up a chess position, which must be done prior to any search. The starting position is set up using `position startpos`. Set up a position using a FEN string so: `position fen <fen_string>`. The FEN string must include the position, side to move, en passant square and castling rights. Fullmove and halfmove clocks are optional. After both startpos and FEN strings may follow a list of moves played from the position. This list must begin with the `moves` token, e.g. `position startpos moves e2e4 e7e5 g1f3`.

### `go [depth n | movetime t | wtime x btime y ...]`

Starts searching the current position. None of the arguments are mandatory, and if none are provided, the default time budget is used. `wtime` indicates white's time left, `btime` is black's time left, `winc` tells white's time increment after each move and `binc` is black's time increment. The search can also be run as `go movetime <x>`, which precisely specifies the time the search should take. `go depth <x>` runs a search with unrestricted time, but a strict maximum search depth. `go infinite` starts a neverending search. All time arguments shall be passed in milliseconds.

### `stop`

Interrupts search immediately. Works both for perft and regular search.

### `setoption name [option] value [value]`

Changes engine configuration. Available options:

- **PolyglotUseBook** (check): enables/disables retrieving and playing moves from the specified Polyglot book
- **PolyglotBook** (string): sets the path to the Polyglot book
- **PolyglotRisk** (spin): decides how risky the engine acts when choosing from multiple differently weighted moves in the Polyglot book
- **Hash** (spin): size of the TT in MiB (other tables are not affected)
- **NKLogs** (check): enables/disables logging all UCI communication into an external log file
- **PrintStats** (check): enables/disables printing fancy statistics at the end of regular and perft searches

### `quit`

Shuts down the engine immediately.

### `perft` (non-UCI)

Stands for PERFormance Test. This command is used to measure the move generation algorithm's speed and correctness. The syntax is `perft <x>`, which specifies the depth at which the test shall be performed. The output is the number of nodes found exactly at the specified depth, and the total time spent to find this number.

### `d` (non-UCI)

Prints the currently set position.

### `cls` (non UCI)

Clears the console window.

### `bench` (non-UCI)

Runs current benchmarks.

### `test` (non-UCI)

Performs currently set up tests.

### `tune <p1 p2 p3 ...>` (non-UCI)

Tunes internal parameters. Don't worry, you'll figure it out.

### `help` (non-UCI)

Redirects you here.

---

## Build & Run
