//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using Kreveta.consts;
using Kreveta.movegen.pieces;
using Kreveta.moveorder.historyheuristics;

using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.evaluation;

internal static class Eval {
    
    // the side to play gets a small bonus
    private const sbyte SideToMoveBonus = 5;

    // POSITION STRUCTURE BONUSES & MALUSES
    private const sbyte DoubledPawnPenalty       = -6;
    private const sbyte IsolatedPawnPenalty      = -21;
    private const sbyte IsolaniAddPenalty        = -4;
    private const sbyte ConnectedPawnBonus = 9;
    private const sbyte BlockedPawnPenalty       = -4;
    //private const int OpenPawnBonus            = 5;

    private const sbyte BishopPairBonus = 35;

    private const sbyte OpenFileRookBonus     = 18;
    //private const sbyte SemiOpenFileRookBonus = 7;
    //private const int SeventhRankRookBonus     = 3;

    //internal const int KingInCheckPenalty      = 72;
    
    private static readonly ulong[] AdjFiles = new ulong[8];

    static Eval() {
        // adjacent files for isolated pawn eval
        for (int i = 0; i < 8; i++) {
            AdjFiles[i] = Consts.RelevantFileMask[i]
                | (i != 0 ? Consts.RelevantFileMask[i - 1] : 0UL)
                | (i != 7 ? Consts.RelevantFileMask[i + 1] : 0UL);
        }
    }

    // returns the static evaluation of a position. static eval is used
    // in the leaf nodes of the search tree or is some pruning cases. it
    // doesn't implement any searches, so it is purely static. the point
    // of this method is to give the engine a very rough estimate of how
    // good a position is. it evaluates material, piece position, pawn
    // structure, king safety, etc. the score returned is color relative,
    // so a positive score means the position is likely to be winning for
    // white, and a negative score should be better for black
    internal static short StaticEval(in Board board) {

        ulong wOccupied = board.WOccupied;
        ulong bOccupied = board.BOccupied;

        byte pieceCount = (byte)ulong.PopCount(wOccupied | bOccupied);
        
        ReadOnlySpan<ulong> pieces = board.Pieces;

        // don't check for insufficient material draw if there are more than 4 pieces
        if (pieceCount <= 4 && IsInsufficientMaterialDraw(pieces, pieceCount))
            return 0;
        
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

        // pawn eval:
        // 
        // 1. penalties for doubled, tripled, and more stacked pawns
        // 2. penalties for isolated pawns (no friendly pawns on adjacent files)
        // 3. bonuses for connected pawns in the other half of the board
        // 4. penalties for pawns blocked by friendly pieces
        eval += PawnEval(pieces[0], Color.WHITE, wOccupied, bOccupied);
        eval -= PawnEval(pieces[6], Color.BLACK, wOccupied, bOccupied);

        // knight eval:
        //
        // 1. decreasing value with fewer pawns on the board
        eval += KnightEval(pieces, pieceCount);

        // bishop eval:
        //
        // 1. bonus for having a full bishop pair
        eval += BishopEval(pieces);

        // rook eval:
        //
        // 1. increasing value with fewer pieces on the board
        // 2. bonuses for rooks on open or semi-open files
        // 3. bonuses for rooks on the seventh rank
        eval += RookEval(pieces, pieceCount);

        // king eval:
        //
        // 1. bonuses for friendly pieces protecting the king
        eval += KingEval(pieces, wOccupied, bOccupied);

        // side to move should also get a slight advantage
        eval += (short)(board.Color == Color.WHITE ? SideToMoveBonus : -SideToMoveBonus);
        
        return eval;
    }

    // bonuses or penalties for pawn structure
    private static short PawnEval(ulong p, Color col, ulong wOccupied, ulong bOccupied) {
        short eval = 0;

        sbyte colMult = (sbyte)(col == Color.WHITE ? 1 : -1);

        for (int i = 0; i < 8; i++) {
            ulong file = Consts.RelevantFileMask[i];

            // count the number of pawns on the file
            byte fileOcc = (byte)ulong.PopCount(file & p);
            if (fileOcc == 0)
                continue;

            // penalties for doubled pawns. by subtracting 1 we can simultaneously
            // penalize all sorts of stacked pawns while not checking single ones
            eval += (short)((fileOcc - 1) * DoubledPawnPenalty * colMult);

            //if (BB.Popcount(file & board.Pieces[(byte)(col == Color.WHITE ? Color.BLACK : Color.WHITE), (byte)PType.PAWN]) == 0)
            //    eval += OpenPawnBonus * colMult;

            // current file + files next to it
            ulong adj = AdjFiles[i];

            // if the number of pawns on current file is equal to the number of pawns
            // on the current plus adjacent files, we know the pawn/s are isolated
            byte adjOcc = (byte)ulong.PopCount(adj & p);

            // isolani is an isolated pawn on the d-file. this usually tends
            // to be the worst isolated pawn, so there's a higher penalty
            sbyte isolani = i == 3 ? IsolaniAddPenalty : (sbyte)0;
            eval += (short)(fileOcc != adjOcc ? 0 : (IsolatedPawnPenalty + isolani) * colMult);
        }

        ulong copy = p;
        while (copy != 0UL) {
            byte sq = BB.LS1BReset(ref copy);

            if (col == Color.WHITE ? sq < 40 : sq > 23) {

                // add a bonus for connected pawns in the opponent's half of the board.
                // this should (and hopefully does) increase the playing strength in
                // endgames and also allow better progressing into endgames
                ulong targets = Pawn.GetPawnCaptureTargets(sq, 64, col, p);
                eval += (short)((sbyte)ulong.PopCount(targets) * ConnectedPawnBonus);
            }

            // penalize blocked pawns - pawns that have a friendly piece directly in
            // front of them and thus cannot push further. in order to push this pawn,
            // you first have to move the other piece, which makes it worse.
            if (col == Color.WHITE && (1UL << sq - 8 & wOccupied) != 0UL)
                eval += BlockedPawnPenalty;

            else if (col == Color.BLACK && (1UL << sq + 8 & bOccupied) != 0UL)
                eval -= BlockedPawnPenalty;
        }

        return eval;
    }

    private static short KnightEval(ReadOnlySpan<ulong> pieces, byte pawnCount) {
        short eval = 0;

        // knights are less valuable if there are fewer pawns on the board.
        // number of white knights and black knights on the board:
        byte wKnights = (byte)ulong.PopCount(pieces[1]);
        byte bKnights = (byte)ulong.PopCount(pieces[7]);

        // subtract some eval for white if it has knights
        eval -= (short)(wKnights * (pawnCount / 2));

        // add some eval for black it has knights
        eval += (short)(bKnights * (pawnCount / 2));

        return eval;
    }

    private static short BishopEval(ReadOnlySpan<ulong> pieces) {
        short eval = 0;

        // accidental bishop pairs may appear in endgames - a player can
        // have two bishops of the same color, so it isn't really a bishop
        // pair. this error should, however, be rare and inconsequential

        // i did some testing with checking the colors of the bishops, and it
        // slows down the eval quite a lot, that's why it isn't implemented

        // does white have two (or more) bishops?
        eval += (short)(ulong.PopCount(pieces[2]) > 1 ? BishopPairBonus : 0);

        // does black have two (or more) bishops?
        eval -= (short)(ulong.PopCount(pieces[8]) > 1 ? BishopPairBonus : 0);

        return eval;
    }

    private static short RookEval(ReadOnlySpan<ulong> pieces, byte pieceCount) {
        short eval = 0;

        // rooks are, as opposed to knights, more valuable if there are
        // fewer pieces on the board. this should motivate the engine into
        // protecting and keeping its rooks as it goes into the endgame.
        // number of white rooks and black rooks on the board:
        byte wRooksCount = (byte)ulong.PopCount(pieces[3]);
        byte bRooksCount = (byte)ulong.PopCount(pieces[9]);

        // add some eval for white if it has rooks
        eval += (short)(wRooksCount * (32 - pieceCount) / 2);

        // subtract some eval for black it has rooks
        eval -= (short)(bRooksCount * (32 - pieceCount) / 2);

        // all pawns
        /*ulong wPawns = pieces[0];
        ulong bPawns = pieces[6];

        // here we try to add bonuses for rooks on open files. a file
        // is open if there aren't any pawns on it, regardless of color.
        // other minor and major pieces are not taken into account
        ulong wCopy = pieces[3];
        while (wCopy != 0UL) {
            byte sq = BB.LS1BReset(ref wCopy);

            // number of friendly pawns on the same file as the rook
            //byte ownPawnCount = (byte)ulong.PopCount(Consts.FileMask[sq & 7] & wPawns);

            // total number of pawns on the same file
            byte pawnCount = (byte)ulong.PopCount(Consts.FileMask[sq & 7] & (wPawns | bPawns));

            // there are no pawns on this file => add the bonus
            if (pawnCount == 0)
                eval += OpenFileRookBonus;

            // we also add bonuses for rooks on semi-open files (smaller than
            // those for open files). a file is semi-open only if there aren't
            // any friendly pawns on it. we assume this file might open in the
            // future since we can always capture the opposite pawns
            //else if (ownPawnCount == 0)
            //    eval += SemiOpenFileRookBonus;
        }

        // the same exact principle as above, but for black. although repeating
        // code isn't clean code and certainly not good coding practice, in this
        // case a loop, or a separate function would slow everything down and time
        // is very precious and expensive
        ulong bCopy = pieces[9];
        while (bCopy != 0UL) {
            
            byte sq           = BB.LS1BReset(ref bCopy);
            byte pawnCount    = (byte)ulong.PopCount(Consts.FileMask[sq & 7] & (wPawns | bPawns));
            //byte ownPawnCount = (byte)ulong.PopCount(Consts.FileMask[sq & 7] & bPawns);

            // we subtract this time for black
            if (pawnCount == 0)
                eval -= OpenFileRookBonus;

            //else if (ownPawnCount == 0)
            //    eval -= SemiOpenFileRookBonus;
        }

        // rooks on the seventh rank (or second rank for black) are considered very
        // powerful, since they can typically "eat" undeveloped enemy pawns quite
        // easily and also threaten the king on the eighth (or first) rank. you can
        // read that this rook is worth almost a pawn, but i've tested larger bonuses,
        // and it doesn't work as well, so we only use smaller ones. the bonuses also
        // decrease when progressing into the endgame, because undeveloped pawns are
        // less likely
        //if ((board.Pieces[(byte)Color.WHITE, (byte)PType.ROOK] & 0x000000000000FF00) != 0)
        //    eval += (short)Math.Min(pieceCount >> 3, SeventhRankRookBonus);

        //if ((board.Pieces[(byte)Color.BLACK, (byte)PType.ROOK] & 0x00FF000000000000) != 0)
        //    eval -= (short)Math.Min(pieceCount >> 3, SeventhRankRookBonus);*/

        return eval;
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //private static short QueenEval(ulong q, Color col, int piece_count) {
    //    int eval = 0;
    //
    //    return (short)eval;
    //}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short KingEval(ReadOnlySpan<ulong> pieces, ulong wOccupied, ulong bOccupied) {
        short eval = 0;

        // same color pieces around the king - protection
        ulong wProtection = King.GetKingTargets(BB.LS1B(pieces[5]),  wOccupied);
        ulong bProtection = King.GetKingTargets(BB.LS1B(pieces[11]), bOccupied);

        // bonus for the number of friendly pieces protecting the king
        short wProtBonus = (short)(ulong.PopCount(wProtection) * 2);
        short bProtBonus = (short)(ulong.PopCount(bProtection) * 2);

        eval += (short)(wProtBonus - bProtBonus);

        return eval;
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
}