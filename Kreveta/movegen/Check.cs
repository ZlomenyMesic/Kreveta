//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen.pieces;

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ConvertIfStatementToSwitchStatement

namespace Kreveta.movegen;

internal static class Check {
    internal static bool IsKingChecked(in Board board, Color kingCol) {
        byte kingSq  = BB.LS1B(board.Pieces[(byte)kingCol * 6 + 5]);
        int  oppBase = 6 * (1 - (int)kingCol);

        ulong occupied    = board.Occupied;
        ulong oppOccupied = kingCol == Color.WHITE
            ? board.BOccupied : board.WOccupied;

        // knight check - should be the fastest, so it's worth checking first
        if ((Knight.GetKnightTargets(kingSq, ulong.MaxValue) & board.Pieces[oppBase + 1]) != 0UL)
            return true;

        // pawn check - also pretty fast
        if ((Pawn.GetPawnCaptureTargets(kingSq, 64, kingCol, oppOccupied) & board.Pieces[oppBase]) != 0UL)
            return true;

        // bishop check
        ulong bishopRays = Pext.GetBishopTargets(kingSq, ulong.MaxValue, occupied);
        if ((bishopRays & board.Pieces[oppBase + 2]) != 0UL)
            return true;

        // rook check
        ulong rookRays = Pext.GetRookTargets(kingSq, ulong.MaxValue, occupied);
        if ((rookRays & board.Pieces[oppBase + 3]) != 0UL)
            return true;

        // queen check - union of bishop and rook
        if (((bishopRays | rookRays) & board.Pieces[oppBase + 4]) != 0UL)
            return true;

        // opposing king
        return (King.GetKingTargets(kingSq, ulong.MaxValue) & board.Pieces[oppBase + 5]) != 0UL;
    }
}