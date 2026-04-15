//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

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
            seeScores = [GetMoveScore(in board, board.SideToMove, capts[0])];
            
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
            int score = GetMoveScore(in board, board.SideToMove, capts[i]);
            
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
    internal static int GetMoveScore(in Board board, Color col, Move move) {
        
        // the square at which the exchange chain is to be evaluated
        byte target    = (byte)move.End;
        bool enPassant = move.Promotion == PType.PAWN;

        // copy piece bitboards, so attackers may be removed
        Span<ulong> pieces = stackalloc ulong[12];
        board.Pieces.AsSpan().CopyTo(pieces);
        
        // occupancy after the first move (attacker removed from its origin)
        ulong occupied = board.Occupied & ~(1UL << move.Start);
        pieces[(int)col * 6 + (int)move.Piece] &= ~(1UL << move.Start);
        
        // in the special case of en passant, the capture square isn't the
        // same as landing square, so although we still use the ending square
        // as the target, the initial captured pawn must be handled separately
        if (enPassant) {
            ulong captureSq = col == Color.WHITE
                ? 1UL << target << 8
                : 1UL << target >> 8;
            
            occupied             ^= captureSq;
            pieces[(int)col * 6] ^= captureSq;
        }

        // captured piece values in order
        Span<int> captures = stackalloc int[MaxCaptures];

        // value of the initial captured piece
        captures[0] = enPassant ? EvalTables.PieceValues[0]
                                : EvalTables.PieceValues[(byte)move.Capture];

        // set up targeted piece and side to move for the next capture
        PType pieceOnTarget = move.Piece;
        Color side          = 1 - col;
        int   depth         = 0;

        while (true) {
            
            // find the next least valuable attacker, and if none exist, break the loop
            (int Square, PType Type) attacker = FindLVA(pieces, occupied, target, side, board);
            if (attacker.Square == -1) break;

            // this probably won't ever happen
            if (++depth >= MaxCaptures) break;

            // add the value of the currently captured piece
            captures[depth] = EvalTables.PieceValues[(byte)pieceOnTarget];

            // remove attacker from origin square in the occupancy
            // bitboard and also in the piece bitboard array
            occupied                                  &= ~(1UL << attacker.Square);
            pieces[(int)col * 6 + (int)attacker.Type] &= ~(1UL << attacker.Square);

            // attacker becomes the new piece on target
            pieceOnTarget = attacker.Type;

            // swap the side to move
            side = 1 - side;
        }

        // no recapture existed => we simply won material
        if (depth == 0)
            return captures[0];
        
        // backwards minimax - not all captures had to be played so
        // make sure we account for exchange chains terminating early
        for (int i = depth - 1; i > 0; --i)
            captures[i] = Math.Max(0, captures[i] - captures[i + 1]);

        // initial capture is real and must be counted
        return captures[0] - captures[1];
    }
    
    // during the exchange chain evaluation loop, we constantly look
    // for new attackers on the target square. an important thing is,
    // that we always look for the least valuable attacker
    private static (int Square, PType Type) FindLVA(Span<ulong> pieces, ulong occupied, byte targetSq, Color col, in Board board) {
        int colBase = (int)col * 6;

        // our pieces except for the ones we have already used in the chain
        ulong ourOccupied = occupied & (col == Color.WHITE 
            ? board.WOccupied : board.BOccupied);

        // go from the least valuable to most valuable piece type, and check
        // whether we have such a piece that attacks the current target square
        ulong pawns = Pawn.GetPawnCaptureTargets(targetSq, 64, 1 - col, ourOccupied) & pieces[colBase];
        if (pawns != 0UL) return (BB.LS1B(pawns), PType.PAWN);

        ulong knights = Knight.GetKnightTargets(targetSq, ourOccupied) & pieces[colBase + 1];
        if (knights != 0UL) return (BB.LS1B(knights), PType.KNIGHT);

        // bishop and rook rays are also used for the queen, so we cannot
        // directly check with AND, instead we must create a separate BB.
        ulong bishops  = Pext.GetBishopTargets(targetSq, ourOccupied, occupied);
        ulong bishops2 = bishops & pieces[colBase + 2];
        if (bishops2 != 0UL) return (BB.LS1B(bishops2), PType.BISHOP);

        ulong rooks  = Pext.GetRookTargets(targetSq, ourOccupied, occupied);
        ulong rooks2 = rooks & pieces[colBase + 3];
        if (rooks2 != 0UL) return (BB.LS1B(rooks2), PType.ROOK);

        // as already said, queen simply reuses bishop and rook rays
        ulong queens = (rooks | bishops) & pieces[colBase + 4];
        if (queens != 0UL) return (BB.LS1B(queens), PType.QUEEN);

        // kings are also taken into account, but we must ensure
        // no recapture exists, otherwise such move would be illegal
        ulong kings = King.GetKingTargets(targetSq, pieces[colBase + 5]);
        return kings != 0UL 
            ? (BB.LS1B(kings), PType.KING) 
            : (-1,             PType.NONE);
    }
}