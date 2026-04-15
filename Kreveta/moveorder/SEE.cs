using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.movegen.pieces;

using System;
// ReSharper disable InconsistentNaming

namespace Kreveta.moveorder;

internal static class SEE {
    private const int MaxCaptures = 32;
    
    // this function takes a list of captures and orders it by SEE from best to
    // worst. pruning threshold may also be used to automatically prune all moves
    // of which the SEE score is below the provided threshold
    internal static Span<Move> OrderCaptures(in Board board, ReadOnlySpan<Move> capts, out int count, out int[] seeScores, int pruneThreshold) {
        count = capts.Length;
        
        // if the list is empty for some reason
        if (capts.Length == 0) {
            seeScores = [];
            return [];
        }
        
        // if there is a single capture
        if (capts.Length == 1) {
            seeScores = [GetCaptureScore(in board, board.SideToMove, capts[0])];
            
            // check if this move should be pruned
            if (seeScores[0] >= pruneThreshold)
                return new Span<Move>([capts[0]]);
            
            count     = 0;
            seeScores = [];
            return      [];
        }

        var scores = new (Move, int)[capts.Length];
        int cur    = 0;

        // add each capture and its score into a list
        for (int i = 0; i < capts.Length; i++) {
            int score = GetCaptureScore(in board, board.SideToMove, capts[i]);
            
            if (score >= pruneThreshold)
                scores[cur++] = (capts[i], score);
        }

        count = cur;

        // here we once again have a very naive and primitive sorting algorithm,
        // but it shouldn't slow anything down due to the usual low amount of captures
        bool sortsMade = true;
        while (sortsMade) {

            // once we haven't switched any moves, break the loop
            sortsMade = false;

            for (int i = 1; i < cur; i++) {

                // if the current item's SEE score is higher
                // than the previous one's, switch their places
                if (scores[i].Item2 > scores[i - 1].Item2) {
                    (scores[i], scores[i - 1]) = (scores[i - 1], scores[i]);
                    sortsMade = true;
                }
            }
        }

        //add the sorted captures to the final list
        var sorted = new Move[cur];
        seeScores  = new int[cur];
        
        for (int i = 0; i < cur; i++) {
            sorted[i]    = scores[i].Item1;
            seeScores[i] = scores[i].Item2;
        }

        return new Span<Move>(sorted);
    }

    // returns material gain/loss on a single square after evaluating captures
    // from both sides. positive score is a good capture for the specified color
    internal static int GetCaptureScore(in Board board, Color col, Move move) {
        if (move.Capture == PType.NONE) return 0;

        byte target = (byte)move.End;

        // occupancy after our move (we removed the attacker from its origin)
        ulong occ = board.Occupied & ~(1UL << move.Start);

        // copy piece bitboards, so we can remove attackers as we simulate captures
        Span<ulong> pieces = stackalloc ulong[12];
        board.Pieces.AsSpan().CopyTo(pieces);

        // captured piece values in order (no heap alloc)
        Span<int> captures = stackalloc int[MaxCaptures];
        int depth = 0;

        // initial capture
        captures[0] = EvalTables.PieceValues[(byte)move.Capture];

        // piece currently on the target square
        PType pieceOnTarget = move.Piece;

        // opponent moves next
        Color side = col == Color.WHITE
            ? Color.BLACK
            : Color.WHITE;

        // simulate recaptures
        while (true) {
            var attacker = FindLVA(pieces, occ, target, side, board);
            if (attacker.square == -1) break;

            depth++;
            if (depth >= MaxCaptures) break;

            // this attacker captures the current piece on target
            captures[depth] = EvalTables.PieceValues[(byte)pieceOnTarget];

            // remove attacker from origin square
            ulong mask = 1UL << attacker.square;
            occ &= ~mask;

            // remove attacker from its piece bitboard
            int idx = (side == Color.WHITE ? 0 : 6) + (int)attacker.type;
            pieces[idx] &= ~mask;

            // attacker becomes the piece on target
            pieceOnTarget = attacker.type;

            // alternate side
            side = side == Color.WHITE
                ? Color.BLACK
                : Color.WHITE;
        }

        // backwards minimax reduction (indexes > 0 only)
        for (int i = depth - 1; i > 0; --i)
            captures[i] = Math.Max(0, captures[i] - captures[i + 1]);

        // no recapture existed => we simply won material
        if (depth == 0)
            return captures[0];

        // initial capture is real and must be counted
        return captures[0] - captures[1];
    }
    
    private static (int square, PType type) FindLVA(Span<ulong> pieces, ulong occ, byte targetSq, Color stm, in Board board) {
        Color opp     = stm == Color.BLACK ? Color.WHITE : Color.BLACK;
        int   colBase = stm == Color.WHITE ? 0 : 6;

        ulong occupied    = occ & board.Occupied;
        ulong ourOccupied = occ & (stm == Color.WHITE ? board.WOccupied : board.BOccupied);

        ulong pawns = Pawn.GetPawnCaptureTargets(targetSq, 0, opp, ourOccupied);
        pawns      &= pieces[colBase + 0];
        if (pawns != 0UL) return (BB.LS1B(pawns), PType.PAWN);

        ulong knightRays = Knight.GetKnightTargets(targetSq, ourOccupied);
        knightRays      &= pieces[colBase + 1];
        if (knightRays != 0UL) return (BB.LS1B(knightRays), PType.KNIGHT);

        ulong bishopRays  = Pext.GetBishopTargets(targetSq, ourOccupied, occupied);
        ulong bishopRays2 = bishopRays & pieces[colBase + 2];
        if (bishopRays2 != 0UL) return (BB.LS1B(bishopRays2), PType.BISHOP);

        ulong rookRays  = Pext.GetRookTargets(targetSq, ourOccupied, occupied);
        ulong rookRays2 = rookRays & pieces[colBase + 3];
        if (rookRays2 != 0UL) return (BB.LS1B(rookRays2), PType.ROOK);

        ulong queenRays = (rookRays | bishopRays) & pieces[colBase + 4];
        if (queenRays != 0UL) return (BB.LS1B(queenRays), PType.QUEEN);

        ulong kings = King.GetKingTargets(targetSq, 0xFFFFFFFFFFFFFFFF) & pieces[colBase + 5];
        if (kings != 0UL) return (BB.LS1B(kings), PType.KING);

        return (-1, PType.NONE);
    }
}
