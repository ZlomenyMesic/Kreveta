
using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.movegen.pieces;

using System;
// ReSharper disable InconsistentNaming

namespace Kreveta.moveorder;

internal static class SEE {
    // Returns material gain for side to move
    internal static int Evaluate(in Board board, Color col, Move move) {
        int targetSq   = move.End;
        int attackerSq = move.Start;
        
        // Value of the first capture (our capture)
        int gain0 = EvalTables.PieceValues[(byte)move.Capture];

        // If no one can recapture, return gain immediately
        ulong occ  = board.Occupied;
        int   gain = gain0; // accumulated result from both sides

        // Now remove our attacker from the occupancy
        occ ^= 1UL << attackerSq;

        // Side to move after our capture (opponent)
        Color stm = col == Color.WHITE 
            ? Color.BLACK : Color.WHITE;

        // the piece on the target square that can be recaptured
        PType curTarget = move.Piece;

        int prevGainAfterOppMove = 0;

        while (true) {
            // find least valuable attacker for current stm
            var nextAttacker = LeastValuableAttacker(in board, stm, (byte)targetSq, occ);
            if (nextAttacker.Square == -1)
                break;

            // remove that attacker temporarily
            occ ^= 1UL << nextAttacker.Square;
            
            int value = EvalTables.PieceValues[(byte)curTarget];
            curTarget = nextAttacker.Type;

            gain += value * (stm == col ? 1 : -1);

            if (stm != col) {
                if (gain < prevGainAfterOppMove) {
                    gain = prevGainAfterOppMove;
                    break;
                }
                
                prevGainAfterOppMove = gain;
            }

            // change side
            stm = stm == Color.WHITE 
                ? Color.BLACK : Color.WHITE;
        }

        return gain;
    }

    private static (int Square, PType Type) LeastValuableAttacker(in Board board, Color stm, byte targetSq, ulong occ) {
        Color opp = stm == Color.BLACK 
            ? Color.WHITE 
            : Color.BLACK;
        
        int colBase = stm == Color.WHITE ? 0 : 6;

        ulong occupied    = occ & board.Occupied;
        ulong ourOccupied = occ & (stm == Color.WHITE
            ? board.WOccupied : board.BOccupied);
        
        // pawn check
        ulong pawns = Pawn.GetPawnCaptureTargets(targetSq, 0, opp, ourOccupied);
        pawns &= board.Pieces[colBase];
        if (pawns != 0UL)
            return (BB.LS1B(pawns), PType.PAWN);
        
        // knight check
        ulong knightRays = Knight.GetKnightTargets(targetSq, ourOccupied);
        knightRays &= board.Pieces[colBase + 1];
        if (knightRays != 0UL)
            return (BB.LS1B(knightRays), PType.KNIGHT);

        // bishop check
        ulong bishopRays  = Pext.GetBishopTargets(targetSq, ourOccupied, occupied);
        ulong bishopRays2 = bishopRays & board.Pieces[colBase + 2];
        if (bishopRays2 != 0UL)
            return (BB.LS1B(bishopRays2), PType.BISHOP);

        // rook check
        ulong rookRays  = Pext.GetRookTargets(targetSq, ourOccupied, occupied);
        ulong rookRays2 = rookRays & board.Pieces[colBase + 3];
        if (rookRays2 != 0UL)
            return (BB.LS1B(rookRays2), PType.ROOK);

        // queen check - union of bishop and rook
        ulong queenRays = rookRays | bishopRays;
        queenRays &= board.Pieces[colBase + 4];
        if (queenRays != 0UL)
            return (BB.LS1B(queenRays), PType.QUEEN);

        return (-1, PType.NONE);
    }

}