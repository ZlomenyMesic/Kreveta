/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

namespace Kreveta.search.pruning;

internal static class PruningOptions {
    internal static bool ALLOW_NULL_MOVE_PRUNING = true;

    internal static bool ALLOW_RAZORING = false; // false

    internal static bool ALLOW_FUTILITY_PRUNING = true;
    internal static bool ALLOW_REVERSE_FUTILITY_PRUNING = false; // false

    internal static bool ALLOW_LATE_MOVE_PRUNING = true;
    internal static bool ALLOW_LATE_MOVE_REDUCTIONS = true;

    internal static bool ALLOW_DELTA_PRUNING = true;

    // position startpos moves d2d4 d7d5 c1f4 c8f5 b1d2 b8d7 e2e3 e7e6 g2g4 f5g6 f1g2 g8f6 a1c1 f8b4 c2c3 b4e7 g1h3 e8g8 e1g1 a8c8 f4g3 f6e4 d2e4 g6e4 f2f3 e4g6 h3f4 e7g5 f1e1 f8e8 b2b4 g5f4 g3f4 e6e5 d4e5 d7e5 f4e5 e8e5 f3f4 e5e7 f4f5 e7d7 d1e2 c7c5 f5g6 h7g6 b4c5 c8c5 e3e4 d5e4 e2e4 c5c8 c3c4 d8b6 e4e3 b6e3 e1e3 d7d4 e3e4 d4d7 g4g5 d7c7 g2h3 c8d8 a2a4 c7c5 e4g4 d8d5 c4d5 c5c1 g1f2 c1c2 f2g3 c2c3 g3h4 b7b6 g4g3 c3c4 g3g4 c4c3 g4g3 c3c4 h3g4 c4d4 g3c3 d4d5 c3c8 g8h7 c8c7 d5a5 g4d1 a5d5 d1g4 d5a5 g4d1 a5d5 d1f3 d5d4 h4g3 d4a4 c7f7 b6b5 f7b7 b5b4 h2h4 a7a5 b7b8 a4a1 f3d5 a1g1 g3h3 g1a1 d5g8 h7h8 g8f7 h8h7 b8a8 a1h1 h3g2 h1h4 f7g8 h7h8
}
