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
Although originally developed as a hobby project, it now serves
as the basis for my SOČ paper. It is designed to be fast, strong
and reliable, while still aiming for code clarity and proper
documentation, making it ideal for others to study, experiment
with, or use as a foundation for future engines.
Development started on **March 3, 2025**.

---

### Elo Ratings

No official ratings yet.

Rough estimates from playtesting using [Cutechess-cli](https://github.com/cutechess/cutechess):

| Opponent        | Time Control | Games | Elo estimate |
|-----------------|--------------|-------|--------------|
| Stockfish 17    | 40/120       | ~1000 | 2450         |
| ...             | ...          | ...   | ...          |

> [!NOTE]
> Time Control is in format moves/time in seconds

---

## Benchmarks

All measurements were performed on a 12th Gen Intel(R) Core(TM) i7-12700H (2.30 GHz) processor.

### Regular search (initial position)

Default hash table size (64 MiB) was used.

| Full Depth | Sel. Depth | Time      | Nodes Searched | NPS       | Best Move |
|------------|------------|-----------|----------------|-----------|-----------|
| 5          | 15         | 00:00.013 | 10,742         | 895,167   | d4        |
| 10         | 22         | 00:00.286 | 593,955        | 2,076,766 | e4        |
| 15         | 27         | 00:06.329 | 14,827,467     | 2,343,152 | e4        |
| 20         | 32         | 01:06.297 | 161,188,842    | 2,431,314 | Nf3       |
| 25         | 37         | 35:42.661 | 5,077,397,129  | 2,369,669 | e4        |

> [!NOTE]
> Selective depth is the actual depth achieved via quiescence search.

### Perft results (initial position)

| Depth | Nodes           | Time (s)   | NPS         |
|-------|-----------------|------------|-------------|
| 1     | 20              | 00.00138   | 14,493      |
| 2     | 400             | 00.00157   | 254,777     |
| 3     | 8,902           | 00.00227   | 3,921,586   |
| 4     | 197,281         | 00.00867   | 22,757,065  |
| 5     | 4,865,609       | 00.06745   | 72,133,857  |
| 6     | 119,060,324     | 00.89998   | 132,291,615 |
| 7     | 3,195,901,860   | 13.14543   | 243,118,769 |

---

## Engine Internals

### Move Generation

- Bitboard representation of positions
- Precomputed slider tables with PEXT-based lookups

### Search Features

- Principal Variation Search with Alpha-Beta Pruning
- Quiescence search for leaf nodes
- Null Move Pruning (NMP)
- Late Move Pruning (LMP) and Reductions (LMR) relative to history
- Futility Pruning (FP) and Delta Pruning for QSearch
- Mate Distance Pruning (MDP)
- Move ordering based on TT, MVV-LVA and others
- Killer move table + countermove heuristics
- Quiet history and pawn corrections
- Improving search stack
- Resizable transposition table (1-1000 MiB)
- Rational time management

### Static Evaluation

- Piece-Square Tables with tapering evaluation
- Pawn structure eval (doubling, isolation, connection, blocking)
- Bishop pairs and open/semi-open file rooks
- Tapering evaluation for knights and rooks
- King safety based on friendly protection

### Others

- Generic Polyglot opening book support
- [NeoKolors](https://github.com/KryKomDev/NeoKolors) library for logging UCI communication
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
