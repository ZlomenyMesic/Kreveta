//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA5394

using Kreveta.consts;
using Kreveta.movegen.pieces;
using Kreveta.uci;
using Kreveta.uci.options;

using System;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.evaluation;

internal static class Eval {
    
    private const int Tempo        = 6;
    private const int KingInCheck  = -31;
    private const int BishopPair   = 7;
    private const int KPDistance   = 16;

    // pawn structure bonuses and maluses. all are scaled in mp
    // and later rescaled to centipawns to allow higher accuracy
    private const int DoubledPawn  = -25;
    private const int IsolatedPawn = -152;
    private const int PassedPawn   = 73;
    private const int BlockedPawn  = -67;
    private const int BackwardPawn = -42;
    
    private static readonly ulong[] AdjFiles = new ulong[8];
    private static readonly byte[]  Distance = new byte[64 * 64];

    // static evaluation noise
    internal static int NoiseAmplitude;

    internal static void Init() {
        // adjacent files for isolated pawn eval
        for (int i = 0; i < 8; i++) {
            AdjFiles[i] = Consts.RelevantFileMask[i]
              | (i != 0 ? Consts.RelevantFileMask[i - 1] : 0UL)
              | (i != 7 ? Consts.RelevantFileMask[i + 1] : 0UL);
        }
        
        // compute the chebyshev distance between any two squares
        for (int i = 0; i < 64; i++) {
            for (int j = 0; j < 64; j++) {
                int dx = Math.Abs((i  & 7) - (j  & 7));
                int dy = Math.Abs((i >> 3) - (j >> 3));
                
                Distance[i * 64 + j] = (byte)Math.Max(dx, dy);
            }
        }
    }

    // returns the static evaluation of a position. static eval is used mainly in the leaf
    // nodes of the search tree. it evaluates material, piece placement, pawn structure,
    // king safety, etc., but doesn't perform any search. the static eval is hybrid, which
    // means a classical eval is combined with a trained NNUE eval
    internal static short StaticEval(in Board board) {
        int nnue    = board.NNUEEval.Score;
        int classic = Classical(in board);
        
        int d  = Math.Abs(nnue) - Math.Abs(classic);
        int nw = 17 + (d > 0 ? d / 116 : 0);

        // both terms are carefully combined
        int combined = (nw * nnue + (32 - nw) * classic) / 32;

        // ideas taken from Stockfish. if the position is close to 50 move
        // draw, or the evaluation might be vague, eval is pulled toward zero
        combined -= combined * board.HalfMoveClock      / 199;
        combined -= combined * Math.Abs(nnue - classic) / 63_754;

        // when UCI_LimitStrength is on, shift all scores randomly
        // to produce worse play; lower UCI_Elo means more randomness
        if (Options.UCI_LimitStrength) {
            /*
             * some example ranges and the respective Elo:
             * 1500 Elo => +/- ~395
             * 1700 Elo => +/- ~270
             * 1915 Elo => +/- ~140
             */
            combined += Consts.RNG.Next(-NoiseAmplitude, NoiseAmplitude + 1);
        }
        
        return (short)combined;
    }
    
    // this is the classical/hand-crafted eval, that doesn't rely on NNUE. the reason
    // it's still here is that our NNUE understands positional advantage more than
    // material, so this kind of just makes sure that no material blindness occurs.
    private static short Classical(in Board board) {
        ulong wOccupied = board.WOccupied;
        ulong bOccupied = board.BOccupied;

        //byte pieceCount = (byte)ulong.PopCount(wOccupied | bOccupied);
        byte phase      = (byte)board.GamePhase();
        
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
                wEval += EvalTables.GetTableValue(i, Color.WHITE, sq, phase);
            }

            while (bCopy != 0UL) {
                byte sq = BB.LS1BReset(ref bCopy);
                bEval += EvalTables.GetTableValue(i, Color.BLACK, sq, phase);
            }
        }

        short eval = (short)(wEval - bEval);

        // pawn structure
        eval += (short)((PawnEval(pieces[0], pieces[6], Color.WHITE, wOccupied, phase)
                       - PawnEval(pieces[6], pieces[0], Color.BLACK, bOccupied, phase)) / 10);

        // king safety
        eval += (short)(board.IsCheck ? board.SideToMove == Color.WHITE ? KingInCheck : -KingInCheck : 0);
        eval += KingEval(pieces, wOccupied, bOccupied);
        
        // bishops
        eval += Miscellaneous(pieces, phase);

        // side to move should also get a slight advantage
        eval += (short)(board.SideToMove == Color.WHITE ? Tempo : -Tempo);
        return eval;
    }
    
    // bonuses or penalties for pawn structure. the central evaluation function
    // calls this function twice, once for each color, and handles proper signs.
    // this means we don't need to evaluate color-relative here
    private static short PawnEval(ulong pawns, ulong enemyPawns, Color col, ulong friendlyPieces, int phase) {
        int eval = 0;
        int forw = col == Color.WHITE ? -8 : 8;
        
        ulong copy = pawns;
        while (copy != 0UL) {
            byte  sq      = BB.LS1BReset(ref copy);
            int   file_i  = sq & 7;
            ulong file    = Consts.RelevantFileMask[file_i];
            int   relRank = col == Color.BLACK ? sq >> 3 : 8 - (sq >> 3);
            
            bool supported  = Pawn.GetPawnCaptureTargets(sq, 64, 1 - col, pawns)                 != 0UL;
            bool canAdvance = Pawn.GetPawnCaptureTargets((byte)(sq + forw), 64, col, enemyPawns) == 0UL;
            bool phalanx    = Pawn.GetPawnCaptureTargets((byte)(sq - forw), 64, col, pawns)      != 0UL;
            bool opposed    = (enemyPawns & file) != 0UL; // this doesn't check whether the pawn is behind us
            
            // calculate the number of friendly and enemy pawns
            // on the two adjacent files (and the current one)
            ulong adjFiles  = AdjFiles[file_i];
            int   adjOcc    = (int)ulong.PopCount(adjFiles & pawns);
            int   oppAdjOcc = (int)ulong.PopCount(adjFiles & enemyPawns);
            
            // calculate the number of pawns on the current file,
            // and for each one more than 1 add a small penalty
            int fileOcc = (int)ulong.PopCount(file & pawns);
            eval += (short)((fileOcc - 1) * DoubledPawn);
            
            // if no friendly pawns are adjacent, the pawn is isolated
            // and penalized. similarly, if there are no enemy pawns,
            // the pawn is passed, and a bonus is added, scaled by rank
            eval += (short)(fileOcc   != adjOcc ? 0 : IsolatedPawn);
            eval += (short)(oppAdjOcc != 0      ? 0 : PassedPawn
                * relRank                      // bonus scales with rank
                * (supported ? 16 : 10) / 10); // increase bonus if the pawn is protected

            // also check if there's a friendly piece blocking the pawn
            if (((1UL << (sq + forw)) & friendlyPieces) != 0UL)
                eval += BlockedPawn;
            
            // backward pawns - a pawn that cannot advance due to enemy pawns
            // threatening capture, and whose adjacent friendly pawns have
            // either already advanced or had been captured
            if (!canAdvance && !supported) {
                ulong adj = (adjFiles ^ file) & pawns;
                
                // find the most behind adjacent pawn
                int minRank = 8;
                while (adj != 0UL) {
                    int nsq  = BB.LS1BReset(ref adj);
                    int rank = col == Color.WHITE ? 8 - (nsq >> 3) : nsq >> 3;
                    minRank  = Math.Min(minRank, rank);
                }

                if (relRank + 1 < minRank)
                    eval += BackwardPawn;
            }
            
            // overextended pawns in the opening/middlegame that cannot be protected
            if (!supported && relRank > 4 && phase > 35)
                eval -= 4 * relRank * phase * phase / 4900;

            // bonus for unopposed supported or phalanx pawns
            if (!opposed && (supported || phalanx))
                eval += 3 * relRank;
        }

        return (short)eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short Miscellaneous(ReadOnlySpan<ulong> pieces, int phase) {
        int eval = 0;
        
        // bishop pairs - evaluated less in endgames
        eval += ulong.PopCount(pieces[2]) < 2 ? 0 : BishopPair * phase / 70;
        eval -= ulong.PopCount(pieces[8]) < 2 ? 0 : BishopPair * phase / 70;
        
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
        
        // find the closest friendly pawn to each king
        int   wMinDist = 8;
        int   bMinDist = 8;
        ulong wPawns   = pieces[0];
        ulong bPawns   = pieces[6];

        while (wPawns != 0UL) {
            byte dist = Distance[wKing * 64 + BB.LS1BReset(ref wPawns)];
            wMinDist  = Math.Min(wMinDist, dist);
        }
        
        while (bPawns != 0UL) {
            byte dist = Distance[bKing * 64 + BB.LS1BReset(ref bPawns)];
            bMinDist  = Math.Min(bMinDist, dist);
        }
        
        // the penalty is quite large but works well
        eval += KPDistance * (bMinDist - wMinDist);
        
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
        if (matingPieces != 0UL) 
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
    internal static void Trace(in Board board) {
        UCI.Log($"info string NNUE evaluation using {Program.Network}");
        UCI.Log( "info string all scores are side-to-move-relative");
        
        //int pcount = (int)ulong.PopCount(board.Occupied);
        int phase  = board.GamePhase();
        
        // first calculate the material part using piece-square tables
        int material = 0;
        for (byte sq = 0; sq < 64; sq++) {
            PType piece = board.PieceAt(sq);
            Color color = (board.WOccupied & 1UL << sq) != 0 ? Color.WHITE : Color.BLACK;

            if (piece != PType.NONE)
                material += EvalTables.GetTableValue((byte)piece, color, sq, phase)
                            * (color == Color.WHITE ? 1 : -1);
        }

        // then pawn structure evaluation
        int pawns = (PawnEval(board.Pieces[0], board.Pieces[6], Color.WHITE, board.WOccupied, phase)
                   - PawnEval(board.Pieces[6], board.Pieces[0], Color.BLACK, board.BOccupied, phase)) / 10;

        // king safety evaluation
        int kings = KingEval(board.Pieces, board.WOccupied, board.BOccupied)
                    + (board.IsCheck ? KingInCheck * (board.SideToMove == Color.WHITE ? 1 : -1) : 0);

        // other factors
        int other = (board.SideToMove == Color.WHITE ? Tempo : -Tempo)
            + Miscellaneous(board.Pieces, phase);

        // and combined this makes the classical part of evaluation
        int total = material + pawns + kings + other;
        
        int colMult = Game.EngineColor == Color.WHITE ? 1 : -1;
        material *= colMult;
        pawns    *= colMult;
        kings    *= colMult;
        other    *= colMult;
        total    *= colMult;
        
        UCI.Log("\n +--------------------------+-------+");
        UCI.Log(  " | Component                | Value |");
        UCI.Log(  " +--------------------------+-------+");
        
        UCI.Log(  " | Classical (hand-crafted) | ", nl: false);
        UCI.Log($"{Score.ToRegular(total)} |\n"
               + $" |  ├─ Material (PSTs)      | {Score.ToRegular(material)} |\n"
               + $" |  ├─ Pawn structure       | {Score.ToRegular(pawns)} |\n"
               + $" |  ├─ King safety          | {Score.ToRegular(kings)} |\n"
               + $" |  └─ Other                | {Score.ToRegular(other)} |");
        
        // then there's the NNUE part, which is already calculated inside the board
        UCI.Log(  " |                          |       |", nl: true);
        UCI.Log(  " | NNUE (trained network):  | ",        nl: false);
        UCI.Log($"{Score.ToRegular(board.NNUEEval.Score * colMult)} |");
        UCI.Log(  " +--------------------------+-------+");
        
        // and for the final output we also use the eval stored in the board, as it uses the
        // same factors already mentioned, but scales them and applies 50-move rule diminishing
        UCI.Log(  " | Combined & scaled:       | ", nl: false);
        UCI.Log($"{Score.ToRegular(board.StaticEval * colMult)} |");
        UCI.Log(  " +--------------------------+-------+\n");
    }
}

#pragma warning restore CA5394