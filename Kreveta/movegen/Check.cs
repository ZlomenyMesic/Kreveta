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
        byte kingSq = BB.LS1B(board.Pieces[(byte)kingCol * 6 + 5]);
        int oppBase = kingCol == Color.WHITE ? 6 : 0;

        ulong occupied = board.Occupied;
        ulong oppOccupied = kingCol == Color.WHITE
            ? board.BOccupied : board.WOccupied;

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

        // knight check
        if ((Knight.GetKnightTargets(kingSq, ulong.MaxValue) & board.Pieces[oppBase + 1]) != 0UL)
            return true;

        // pawn check
        if ((Pawn.GetPawnCaptureTargets(kingSq, 0, kingCol, oppOccupied) & board.Pieces[oppBase]) != 0UL)
            return true;

        // opposing king
        return (King.GetKingTargets(kingSq, ulong.MaxValue) & board.Pieces[oppBase + 5]) != 0UL;
    }

    // experiments with only generating evasions in double check and only
    // landing pieces on the blocking ray in regular check. tests done so
    // far actually show this greatly slows down the engine. might work on
    // this more in the future
    /*internal static (byte Attackers, ulong BlockingRay) IsKingCheckedPrecise(in Board board, Color kingCol) {
        byte kingSq = BB.LS1B(board.Pieces[(byte)kingCol * 6 + 5]);
        int oppBase = kingCol == Color.WHITE ? 6 : 0;

        ulong occupied = board.Occupied;
        ulong oppOccupied = kingCol == Color.WHITE
            ? board.BOccupied : board.WOccupied;

        ulong targets = 0UL;

        ulong bishopRays = Pext.GetBishopTargets(kingSq, ulong.MaxValue, occupied);
        targets |= bishopRays & board.Pieces[oppBase + 2];

        ulong rookRays = Pext.GetRookTargets(kingSq, ulong.MaxValue, occupied);
        targets |= rookRays & board.Pieces[oppBase + 3];
        targets |= (bishopRays | rookRays) & board.Pieces[oppBase + 4];

        targets |= Pawn.GetPawnCaptureTargets(kingSq, 0, kingCol, oppOccupied) & board.Pieces[oppBase];

        // knights are handled separately, since no real
        // ray exists between the king, and the knight
        ulong knightTargets = Knight.GetKnightTargets(kingSq, ulong.MaxValue) & board.Pieces[oppBase + 1];

        int attackerCount = (int)ulong.PopCount(targets | knightTargets);

        // no blocking ray is needed in case of double check
        if (attackerCount == 2)
            return (Attackers: 2, BlockingRay: 0UL);

        if (attackerCount == 1) {
            if (knightTargets != 0UL)
                return (1, knightTargets);

            return (1, ConnectingRays[kingSq][BB.LS1B(targets)]);
        }

        return (0, 0UL);
    }

    private static readonly ulong[][] ConnectingRays = new ulong[64][];

    internal static void Init() {
        for (int i = 0; i < 64; i++) {
            ConnectingRays[i] = new ulong[64];
        }

        int[] directions = [1, -1, 8, -8, 7, -7, 9, -9];
        for (int from = 0; from < 64; from++) {
            foreach (int dir in directions) {
                ulong ray = 0UL;
                int sq = from;

                while (true) {
                    sq += dir;
                    if (LeftTheBoard(sq, dir))
                        break;

                    ray |= 1UL << sq;
                    ConnectingRays[from][sq] = ray;
                }
            }
        }
    }

    private static bool LeftTheBoard(int sq, int dir) {
        if (sq is < 0 or > 63)
            return true;

        // moving to the right
        if (dir is 1 or -7 or 9 && (1UL << sq & 0x0101010101010101) != 0UL)
            return true;

        // moving to the left
        if (dir is -1 or 7 or -9 && (1UL << sq & 0x8080808080808080) != 0UL)
            return true;

        return false;
    }*/
}