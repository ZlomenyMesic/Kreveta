# Kreveta

Kreveta is a free open-source chess engine written in C#.

## Features

Kreveta implements various modern move generation and search techniques, such as:
* 8*8 bitboard-based board representation
* Magic bitboards for fast move generation
* Move ordering based on MVV-LVA and quiet history
* Killer move heuristics
* PVS with NMP, FP, LMP, and optionally razoring, LMR and RFP
* Quiescence search with delta pruning
* Transposition table with custom entries
* Tapered static evaluation based on piece position, pawn structure, king safety and other factors

## UCI Protocol

In order to allow proper communication with GUIs, Kreveta uses the UCI protocol. Using the engine directly via the console is also possible, although some knowledge of UCI commands is required. Let all supported commands be noted down in the following list:

### `uci`
Sent by the GUI to ensure the engine uses the UCI protocol. The engine responds with its own name, the author's name, a list of all modifiable options and eventually `uciok`, which lets the GUI know UCI is supported.

### `isready`
Used by the GUI to check whether the engine is ready to respond to further commands. If the engine is ready, it shall respond with `readyok`.

### `ucinewgame`
Lets the engine know it will be playing a whole game of chess, not just analyzing a single position. This allows the engine to toggle various options on or off, depending on the current task. The engine doesn't need to respond with anything.

### `position`
Used to set up a chess position, which can be later searched. The general syntax is: `position [startpos | fen <fen_string> ]  moves <move1 ... moven>`. The starting position is set up using `position startpos`. Setting up a position using a FEN string is done so: `position fen <fen_string>`. The FEN string MUST include the position, side to move, en passant square and castling rights. Fullmove and halfmove clocks are optional. After both startpos and FEN strings may follow a list of moves played from the position. This list must begin with the 'moves' keyword, e.g. `position startpos moves e2e4 e7e5 g1f3`.

### `go`
Tells the engine it should start searching the current position. The syntax is `go wtime <a> btime <b> winc <c> binc <d> movestogo <e>`. None of the arguments are mandatory, and if none are provided, the default time budget is used. `wtime` indicates white's time left, `btime` is black's time left, `winc` tells white's time increment after a move and `binc` is black's time increment. The search can also be run as `go movetime <x>`, which sets exactly the time the search should take. `go depth <x>` runs a search with unlimited time, but a strictly set maximum search depth. `go infinite` starts a neverending search. All time arguments shall be passed in milliseconds.

### `stop`

### `quit`

### `perft`
Stands for PERFormance Test. This command is used to measure the move generation algorithm's speed and correctness. The syntax is `perft <x>`, which specifies the depth at which the test shall be performed. The output is the number of nodes found exactly at the specified depth and the total time spent to find this number.
