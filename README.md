# 🦐 Kreveta Chess Engine

A high-performance UCI-compatible chess engine written in C#.
Designed to be fast and strong, with a main focus on optimizing C# to its limits.
Development started on **4/3/2025**.

---

## 📦 Features

- ✅ **UCI Protocol Support**
- ⚡ **High-speed move generation using Magic Bitboards**
- ♟️ **PV Search with various enhancements**
- 📊 **Performance benchmarking**

### ELO Ratings

No official ratings yet.

Rough estimates from playtesting using Cutechess-cli:

| Opponent        | Time Control* | Games  | ELO Estimate |
|-----------------|---------------|--------|--------------|
| Stockfish 17    | 40/100        | ~450   | 2382         |
| Stockfish 17    | 40/120        | ~2000  | 2366         |
| ...             | ...           | ...    | ...          |

*TC is in standard format moves/time with no per-move time increments. The clock resets when the specified number of moves is reached.

---

## 🔬 Benchmarks

### Regular search (initial position)

| Depth | Sel. Depth* | Time (s) | Nodes Searched | NPS       | Best Move |
|-------|-------------|----------|----------------|-----------|-----------|
| 5     | 15          | 0.013    | 11,022         | 918,500   | d4        |
| 10    | 22          | 0.342    | 605,805        | 1,771,360 | e4        |
| 15    | 27          | 8.274    | 16,113,096     | 1,947,437 | e4        |
| 20    | 32          | 207.9    | 419,837,373    | 2,019,148 | g3        |

*Depth is the full depth. Selective depth is the actual achieved depth via quiescence search.

### Perft results (initial position)

| Depth | Nodes           | Time (s)     | NPS           |
|-------|-----------------|--------------|---------------|
| 1     | 20              | 00.000003    | 7,629,307     |
| 2     | 400             | 00.000023    | 17,621,145    |
| 3     | 8,902           | 00.00025     | 36,098,945    |
| 4     | 197,281         | 00.0053      | 39,456,200    |
| 5     | 4,865,609       | 00.095       | 51,761,798    |
| 6     | 119,060,324     | 01.409       | 84,499,875    |
| 7     | 3,195,901,860   | 23.487       | 136,071,097   |

---

## 🧠 Engine Internals

### Move Generation

- Bitboard representation for fast operations
- Magic Bitboards for sliding piece attacks
- Precomputed move lookup tables for fast access

### Search Features

- Pricipal Variation Search with Alpha-Beta Pruning
- Quiescence search for leaf nodes
- Null Move Pruning (NMP)
- Late Move Pruning (LMP) and Reductions (LMR) relative to history
- Futility Pruning (FP) and Delta Pruning for QSearch
- Mate distance pruning (MDP)
- Move ordering based on TT, MVV-LVA and others
- Killer move table + Countermove Heuristics
- Quiet history and Pawn Corrections
- Improving search stack
- Resizable Transposition Table (1-1000 MB)

### Static Evaluation

- Piece-Square Tables with tapering evaluation
- Pawn structure eval - doubling, isolation, connection, blocking
- Bishop pairs and open-file rooks
- Tapering evaluation for knights and rooks
- King safety based on protecting pieces

---

## 🎮 UCI Protocol Support

Kreveta fully supports the **Universal Chess Interface (UCI)** protocol.
For a more detailed explanation, visit the online UCI protocol documentation.
Below are the implemented commands and their usage:

### `uci`

Sent by the GUI to ensure the engine uses the UCI protocol. The engine responds with `id name`, `id author`, a list of all modifiable options and eventually `uciok`, which lets the GUI know UCI is supported.

### `isready`

Used by the GUI to check whether the engine is ready to respond to further commands. If the engine is ready, it shall respond with `readyok`.

### `ucinewgame`

Signals that the engine will be playing a whole game of chess, instead of just analyzing a single position. This allows the engine to toggle various options on or off, depending on the current task. The engine doesn't need to respond with anything.

### `position [fen | startpos] moves ...`

Used to set up a chess position, which can be later searched. The starting position is set up using `position startpos`. Setting up a position using a FEN string is done so: `position fen <fen_string>`. The FEN string MUST include the position, side to move, en passant square and castling rights. Fullmove and halfmove clocks are optional. After both startpos and FEN strings may follow a list of moves played from the position. This list must begin with the 'moves' token, e.g. `position startpos moves e2e4 e7e5 g1f3`.

### `go [depth n | movetime t | wtime x btime y ...]`

Tells the engine it should start searching the current position. None of the arguments are mandatory, and if none are provided, the default time budget is used. `wtime` indicates white's time left, `btime` is black's time left, `winc` tells white's time increment after each move and `binc` is black's time increment. The search can also be run as `go movetime <x>`, which defines the maximum time the engine should spend on the search. `go depth <x>` runs a search with unlimited time, but a strictly set maximum search depth. `go infinite` starts a neverending search. All time arguments shall be passed in milliseconds.

### `stop`

Interrupts search immediately. Works both for perft and regular search.

### `setoption name [option] value [value]`

Changes engine configuration. Available options are OwnBook, Hash, NKLogs and PrintStats.

### `quit`

Exits the program.

### `perft` (non-UCI command)

Stands for PERFormance Test. This command is used to measure the move generation algorithm's speed and correctness. The syntax is `perft <x>`, which specifies the depth at which the test shall be performed. The output is the number of nodes found exactly at the specified depth and the total time spent to find this number.

### `d` (non-UCI command)

Prints the currently set position.

### `bench` (non-UCI command)

Runs current benchmarks.

---

## 🛠️ Build & Run
