//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using Kreveta.consts;
using Kreveta.movegen.pieces;
using Kreveta.uci;

using System.Runtime.CompilerServices;
using Kreveta.movegen;

// ReSharper disable InconsistentNaming

namespace Kreveta.evaluation;

internal static class Eval {

    private const int SideToMoveBonus = 6;
    private const int InCheckMalus    = -30;

    // pawn structure bonuses and maluses. all are scaled in mp
    // and later rescaled to centipawns to allow higher accuracy
    private const int DoubledPawnMalus  = -33;
    private const int IsolatedPawnMalus = -149;
    private const int PassedPawnBonus   = 78;
    private const int BlockedPawnMalus  = -65;
    
    private static readonly ulong[] AdjFiles = new ulong[8];

    internal static void Init() {
        // adjacent files for isolated pawn eval
        for (int i = 0; i < 8; i++) {
            AdjFiles[i] = Consts.RelevantFileMask[i]
                | (i != 0 ? Consts.RelevantFileMask[i - 1] : 0UL)
                | (i != 7 ? Consts.RelevantFileMask[i + 1] : 0UL);
        }
    }

    // returns the static evaluation of a position. static eval is used mainly in the leaf
    // nodes of the search tree. it evaluates material, piece placement, pawn structure,
    // king safety, etc., but doesn't perform any search. the static eval is hybrid, which
    // means a classical eval is combined with a trained NNUE eval
    internal static short StaticEval(in Board board) {
        int nnue    = board.NNUEEval.Score;
        int classic = Classical(in board);

        // carefully combine the two terms
        int combined = (17 * nnue + 15 * classic) / 32;

        // an idea taken from stockfish: in order to avoid hallucinating wins
        // where 50-move draw is basically inevitable, the eval is gradually
        // pulled closer to zero as the counter increases
        combined -= combined * board.HalfMoveClock / 199;

        return (short)combined;
    }
    
    // this is the classical/hand-crafted eval, that doesn't rely on NNUE. the reason
    // it's still here is that our NNUE understands positional advantage more than
    // material, so this kind of just makes sure that no material blindness occurs.
    private static short Classical(in Board board) {
        ulong wOccupied = board.WOccupied;
        ulong bOccupied = board.BOccupied;

        byte pieceCount = (byte)ulong.PopCount(wOccupied | bOccupied);
        
        ReadOnlySpan<ulong> pieces = board.Pieces;
        
        short wEval = 0, bEval = 0;
        
        // loop over all piece types
        for (byte i = 0; i < 6; i++) {

            // copy the respective piece bitboards for both colors
            ulong wCopy = pieces[i];
            ulong bCopy = pieces[6 + i];

            // here for each color we add the table value of the piece. the tables
            // are in EvalTables.cs, and they give both material and position values.
            // although this code isn't really clean, it is much faster than putting
            // the color into a loop as well
            while (wCopy != 0UL) {
                byte sq = BB.LS1BReset(ref wCopy);
                wEval += EvalTables.GetTableValue(i, Color.WHITE, sq, pieceCount);
            }

            while (bCopy != 0UL) {
                byte sq = BB.LS1BReset(ref bCopy);
                bEval += EvalTables.GetTableValue(i, Color.BLACK, sq, pieceCount);
            }
        }

        short eval = (short)(wEval - bEval);

        eval += (short)((PawnEval(pieces[0], pieces[6], Color.WHITE, wOccupied)
                         - PawnEval(pieces[6], pieces[0], Color.BLACK, bOccupied)) / 10);

        eval += (short)(board.IsCheck ? InCheckMalus * (board.SideToMove == Color.WHITE ? 1 : -1) : 0);
        eval += KingEval(pieces, wOccupied, bOccupied);

        // side to move should also get a slight advantage
        eval += (short)(board.SideToMove == Color.WHITE ? SideToMoveBonus : -SideToMoveBonus);
        return eval;
    }
    
    // bonuses or penalties for pawn structure
    private static short PawnEval(ulong pawns, ulong enemyPawns, Color col, ulong friendlyPieces) {
        int eval = 0;
        
        ulong copy = pawns;
        while (copy != 0UL) {
            byte  sq   = BB.LS1BReset(ref copy);
            ulong file = Consts.RelevantFileMask[sq & 7];
            
            // calculate the number of pawns on the current file,
            // and for each one more than 1 add a small penalty
            int fileOcc = (int)ulong.PopCount(file & pawns);
            eval += (short)((fileOcc - 1) * DoubledPawnMalus);
            
            // calculate the number of friendly and enemy pawns
            // on the two adjacent files (and the current one)
            ulong adjFiles  = AdjFiles[sq & 7];
            int   adjOcc    = (int)ulong.PopCount(adjFiles & pawns);
            int   oppAdjOcc = (int)ulong.PopCount(adjFiles & enemyPawns);
            
            // if no friendly pawns are adjacent, the pawn is isolated
            // and penalized. similarly, if there are no enemy pawns,
            // the pawn is passed, and a bonus is added, scaled by rank
            eval += (short)(fileOcc   != adjOcc ? 0 : IsolatedPawnMalus);
            eval += (short)(oppAdjOcc != 0      ? 0 : PassedPawnBonus * (col == Color.WHITE ? 8 - (sq >> 3) : sq >> 3));

            // also check if there's a friendly piece blocking the pawn
            if (col == Color.WHITE && (1UL << sq - 8 & friendlyPieces) != 0UL)
                eval += BlockedPawnMalus;

            else if (col == Color.BLACK && (1UL << sq + 8 & friendlyPieces) != 0UL)
                eval += BlockedPawnMalus;
        }

        return (short)eval;
    }

    // king safety evaluation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short KingEval(ReadOnlySpan<ulong> pieces, ulong wOccupied, ulong bOccupied) {
        int  eval  = 0;
        byte wKing = BB.LS1B(pieces[5]);
        byte bKing = BB.LS1B(pieces[11]);

        // find all pieces adjacent to either king
        ulong wProtection = King.GetKingTargets(wKing, wOccupied);
        ulong bProtection = King.GetKingTargets(bKing, bOccupied);
        ulong wEvil       = King.GetKingTargets(wKing, bOccupied);
        ulong bEvil       = King.GetKingTargets(bKing, wOccupied);

        // reward friendly pieces next to the king - works as protection, also helps in endgames
        // where the king is supposed to guard his pawns. the other way, enemy pieces in contact
        // with the king are penalized, as they form a threat
        eval += (int)ulong.PopCount(wProtection) * 2;
        eval -= (int)ulong.PopCount(bProtection) * 2;
        eval -= (int)ulong.PopCount(wEvil)       * 4;
        eval += (int)ulong.PopCount(bEvil)       * 4;

        return (short)eval;
    }

    // in certain endgames, insufficient material draw happens when
    // there aren't enough pieces of either color for checkmate.
    // the general rules may vary, this is from 
    /*
     * If both sides have any one of the following, and there are no pawns on the board: 
     *
     * a lone king 
     * a king and bishop
     * a king and knight
     */
    internal static bool IsInsufficientMaterialDraw(ReadOnlySpan<ulong> pieces, ulong pieceCount) {
        
        // pieces that prevent inssuficient material draw - pawns, rooks and queens
        ulong matingPieces = pieces[0] | pieces[3] | pieces[4] | pieces[6] | pieces[9] | pieces[10];
        if (ulong.PopCount(matingPieces) != 0UL)
            return false;
        
        // two kings
        if (pieceCount == 2) 
            return true;

        // both white and black have 0 or 1 knights or bishops
        return ulong.PopCount(pieces[1] | pieces[2]) < 2UL
               && ulong.PopCount(pieces[7] | pieces[8]) < 2UL;
    }
    
    // this is used to print the static evaluation using the "eval" command. the actual static eval
    // computation is heavily optimized, so adding this logic directly into it would be difficult,
    // and would also hurt performance. the logic is obviously kept the same, but written in a less
    // optimized and more readable way
    internal static void PrintAnalysis(in Board board) {
        int pcount = (int)ulong.PopCount(board.Occupied);
        
        // first calculate the material part using piece-square tables
        int material = 0;
        for (byte sq = 0; sq < 64; sq++) {
            PType piece = board.PieceAt(sq);
            Color color = (board.WOccupied & 1UL << sq) != 0 ? Color.WHITE : Color.BLACK;

            if (piece != PType.NONE)
                material += EvalTables.GetTableValue((byte)piece, color, sq, pcount)
                            * (color == Color.WHITE ? 1 : -1);
        }

        // then pawn structure evaluation
        int pawns = (PawnEval(board.Pieces[0], board.Pieces[6], Color.WHITE, board.WOccupied)
                     - PawnEval(board.Pieces[6], board.Pieces[0], Color.BLACK, board.BOccupied)) / 10;

        // king safety evaluation
        int kings = KingEval(board.Pieces, board.WOccupied, board.BOccupied)
                    + (board.IsCheck ? InCheckMalus * (board.SideToMove == Color.WHITE ? 1 : -1) : 0);

        // and combined this makes the classical part of evaluation
        int total = material + pawns + kings + (board.SideToMove == Color.WHITE ? SideToMoveBonus : -SideToMoveBonus);
        
        UCI.Log("\nClassical (hand-crafted): ", nl: false);
        UCI.Log($"{Score.ToRegular(total)}\n"
                + $"  Material (PST): {Score.ToRegular(material)}\n"
                + $"  Pawn structure: {Score.ToRegular(pawns)}\n"
                + $"  King safety:    {Score.ToRegular(kings)}\n");
        
        // then there's the NNUE part, which is already calculated inside the board
        UCI.Log("NNUE (trained network):   ", nl: false);
        UCI.Log($"{Score.ToRegular(board.NNUEEval.Score)}\n");
        
        // and for the final output we also use the eval stored in the board, as it uses the
        // same factors already mentioned, but scales them and applies 50-move rule diminishing
        UCI.Log("Combined & scaled:        ", nl: false);
        UCI.Log($"{Score.ToRegular(board.StaticEval)}\n");
    }
}