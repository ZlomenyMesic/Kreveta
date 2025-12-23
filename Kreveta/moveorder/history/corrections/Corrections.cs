//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

namespace Kreveta.moveorder.history.corrections;

internal static class Corrections {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Update(in Board board, short score, int depth) {
        PawnCorrections.Update(board, score, depth);
        KingCorrections.Update(board, score, depth);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short Get(in Board board) {
        short pawn = PawnCorrections.Get(in board);
        short king = KingCorrections.Get(in board);

        return (short)((pawn * 4 + king) / 5);
    }
}