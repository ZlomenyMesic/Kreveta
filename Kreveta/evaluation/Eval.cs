//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen.pieces;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.evaluation;

internal static class Eval {

    // the side to play gets a small bonus
    private const sbyte SideToMoveBonus          = 5;

    // POSITION STRUCTURE BONUSES & PENALTIES

    private const sbyte DoubledPawnPenalty       = -6;
    private const sbyte IsolatedPawnPenalty      = -21;
    private const sbyte IsolaniAddPenalty        = -4;
    private const sbyte ConnectedPassedPawnBonus = 9;
    private const sbyte BlockedPawnPenalty       = -4;
    //private const int OpenPawnBonus            = 5;

    private const sbyte BishopPairBonus          = 35;

    private const sbyte OpenFileRookBonus        = 18;
    private const sbyte SemiOpenFileRookBonus    = 7;
    //private const int SeventhRankRookBonus     = 3;

    //internal const int KingInCheckPenalty      = 72;

    [ReadOnly(true), DebuggerBrowsable(DebuggerBrowsableState.Never)]
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
    internal static short StaticEval([In, ReadOnly(true)] in Board board) {

        ulong wOccupied = board.WOccupied;
        ulong bOccupied = board.BOccupied;

        byte pieceCount = BB.Popcount(wOccupied | bOccupied);

        short wEval = 0, bEval = 0;

        // loop all piece types
        for (int i = 0; i < 6; i++) {

            // copy the respective piece bitboards for both colors
            ulong wCopy = board.Pieces[(byte)Color.WHITE][i];
            ulong bCopy = board.Pieces[(byte)Color.BLACK][i];

            // here for each color we add the table value of the piece. the tables
            // are in EvalTables.cs, and they give both material and position values.
            // although this code isn't really clean, it is much faster than putting
            // the color into a loop as well
            while (wCopy != 0UL) {
                sbyte sq = BB.LS1BReset(ref wCopy);
                wEval += GetTableValue((PType)i, Color.WHITE, sq, pieceCount);
            }

            while (bCopy != 0UL) {
                sbyte sq = BB.LS1BReset(ref bCopy);
                bEval += GetTableValue((PType)i, Color.BLACK, sq, pieceCount);
            }
        }

        short eval = (short)(wEval - bEval);

        // pawn structure eval:
        // 
        // 1. penalties for doubled, tripled, and more stacked pawns
        // 2. penalties for isolated pawns (no friendly pawns on adjacent files)
        // 3. bonuses for connected pawns in the other half of the board
        // 4. penalties for pawns blocked by friendly pieces
        eval += PawnStructureEval(board, board.Pieces[(byte)Color.WHITE][(byte)PType.PAWN], Color.WHITE);
        eval -= PawnStructureEval(board, board.Pieces[(byte)Color.BLACK][(byte)PType.PAWN], Color.BLACK);

        // knight eval:
        //
        // 1. decreasing value with fewer pawns on the board
        eval += KnightEval(board, pieceCount);

        // bishop eval:
        //
        // 1. bonus for having a full bishop pair
        eval += BishopEval(board);

        // rook eval:
        //
        // 1. increasing value with fewer pieces on the board
        // 2. bonuses for rooks on open or semi-open files
        // 3. bonuses for rooks on the seventh rank
        eval += RookEval(board, pieceCount);

        // king eval:
        //
        // 1. bonuses for friendly pieces protecting the king
        eval += KingEval(board);

        // side to move should also get a slight advantage
        eval += (short)(board.Color == Color.WHITE ? SideToMoveBonus : -SideToMoveBonus);

        //eval += (short)History.GetPawnCorrection(board);

        return eval;
    }
    
    private static short GetTableValue(PType type, Color col, sbyte sq, byte pieceCount) {
        // this method uses the value tables in EvalTables.cs, and is used to evaluate a piece position
        // there are two tables - midgame and endgame, this is important, because the pieces should be
        // in different positions as the game progresses (e.g. a king in the midgame should be in the corner,
        // but in the endgame in the center)

        // we have to index the piece and position correctly. white
        // pieces are simple, but black piece have to be mirrored
        short i = (short)((byte)type * 64 + (col == Color.WHITE
            ? 63 - sq
            : (sq >> 3) * 8 + (7 - (sq & 7))));

        // we grab both the midgame and endgame table values
        short mgValue = EvalTables.Midgame[i];
        short egValue = EvalTables.Endgame[i];

        // a very rough attempt for tapering evaluation - instead of
        // just switching straight from midgame into endgame, the table
        // value of the piece is always somewhere in between depending
        // on the number of pieces left on the board.
        return (short)(mgValue * pieceCount / 32 + egValue * (32 - pieceCount) / 32);
    }

    // bonuses or penalties for pawn structure
    private static short PawnStructureEval([In, ReadOnly(true)] in Board board, ulong p, Color col) {

        short eval = 0;

        sbyte colMult = (sbyte)(col == Color.WHITE ? 1 : -1);

        for (int i = 0; i < 8; i++) {
            ulong file = Consts.RelevantFileMask[i];

            // count the number of pawns on the file
            byte fileOcc = BB.Popcount(file & p);
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
            byte adjOcc = BB.Popcount(adj & p);

            // isolani is an isolated pawn on the d-file. this usually tends
            // to be the worst isolated pawn, so there's a higher penalty
            sbyte isolani = i == 3 ? IsolaniAddPenalty : (sbyte)0;
            eval += (short)(fileOcc != adjOcc ? 0 : (IsolatedPawnPenalty + isolani) * colMult);
        }

        ulong copy = p;
        while (copy != 0UL) {
            byte sq = (byte)BB.LS1BReset(ref copy);

            if (col == Color.WHITE ? sq < 40 : sq > 23) {

                // add a bonus for connected pawns in the opponent's half of the board.
                // this should (and hopefully does) increase the playing strength in
                // endgames and also allow better progressing into endgames
                ulong targets = Pawn.GetPawnCaptureTargets(1UL << sq, 64, col, p);
                eval += (short)(BB.Popcount(targets) * ConnectedPassedPawnBonus);
            }

            // penalize blocked pawns - pawns that have a friendly piece directly in
            // front of them and thus cannot push further. in order to push this pawn,
            // you first have to move the other piece, which makes it worse.
            if (col == Color.WHITE && ((1UL << (sq - 8)) & board.WOccupied) != 0UL)
                eval += BlockedPawnPenalty;

            else if (col == Color.BLACK && ((1UL << (sq + 8)) & board.BOccupied) != 0UL)
                eval -= BlockedPawnPenalty;
        }

        return eval;
    }
    
    private static short KnightEval([In, ReadOnly(true)] in Board board, byte pawnCount) {
        short eval = 0;

        // knights are less valuable if there are fewer pawns on the board.
        // number of white knights and black knights on the board:
        byte wKnights = BB.Popcount(board.Pieces[(byte)Color.WHITE][(byte)PType.KNIGHT]);
        byte bKnights = BB.Popcount(board.Pieces[(byte)Color.BLACK][(byte)PType.KNIGHT]);

        // subtract some eval for white if it has knights
        eval -= (short)(wKnights * (pawnCount / 2));

        // add some eval for black it has knights
        eval += (short)(bKnights * (pawnCount / 2));

        return eval;
    }
    
    private static short BishopEval([In, ReadOnly(true)] in Board board) {

        short eval = 0;

        // accidental bishop pairs may appear in endgames - a player can
        // have two bishops of the same color, so it isn't really a bishop
        // pair. this error should, however, be rare and inconsequential

        // i did some testing with checking the colors of the bishops, and it
        // slows down the eval quite a lot, that's why it isn't implemented

        // does white have two (or more) bishops?
        eval += (short)(BB.Popcount(board.Pieces[(byte)Color.WHITE][(byte)PType.BISHOP]) > 1 ? BishopPairBonus : 0);

        // does black have two (or more) bishops?
        eval -= (short)(BB.Popcount(board.Pieces[(byte)Color.BLACK][(byte)PType.BISHOP]) > 1 ? BishopPairBonus : 0);

        return eval;
    }
    
    private static short RookEval([In, ReadOnly(true)] in Board board, byte pieceCount) {
        short eval = 0;

        // rooks are, as opposed to knights, more valuable if there are
        // fewer pieces on the board. this should motivate the engine into
        // protecting and keeping its rooks as it goes into the endgame.
        // number of white rooks and black rooks on the board:
        byte wRooksCount = BB.Popcount(board.Pieces[(byte)Color.WHITE][(byte)PType.ROOK]);
        byte bRooksCount = BB.Popcount(board.Pieces[(byte)Color.BLACK][(byte)PType.ROOK]);

        // add some eval for white if it has rooks
        eval += (short)(wRooksCount * (32 - pieceCount) / 2);

        // subtract some eval for black it has rooks
        eval -= (short)(bRooksCount * (32 - pieceCount) / 2);

        // all pawns
        ulong wPawns = board.Pieces[(byte)Color.WHITE][(byte)PType.PAWN];
        ulong bPawns = board.Pieces[(byte)Color.BLACK][(byte)PType.PAWN];

        // here we try to add bonuses for rooks on open files. a file
        // is open if there aren't any pawns on it, regardless of color.
        // other minor and major pieces are not taken into account
        ulong wCopy = board.Pieces[(byte)Color.WHITE][(byte)PType.ROOK];
        while (wCopy != 0UL) {
            sbyte sq           = BB.LS1BReset(ref wCopy);

            // number of friendly pawns on the same file as the rook
            byte ownPawnCount = BB.Popcount(Consts.FileMask[sq & 7] & wPawns);

            // total number of pawns on the same file
            byte pawnCount    = BB.Popcount(Consts.FileMask[sq & 7] & (wPawns | bPawns));

            // there are no pawns on this file => add the bonus
            if (pawnCount == 0) 
                eval += OpenFileRookBonus;
            
            // we also add bonuses for rooks on semi-open files (smaller than
            // those for open files). a file is semi-open only if there aren't
            // any friendly pawns on it. we assume this file might open in the
            // future since we can always capture the opposite pawns
            else if (ownPawnCount == 0) 
                eval += SemiOpenFileRookBonus;
        }

        // the same exact principle as above, but for black. although repeating
        // code isn't clean code and certainly not good coding practice, in this
        // case a loop or a separate function would slow everything down and time
        // is very precious and expensive
        ulong bCopy = board.Pieces[(byte)Color.BLACK][(byte)PType.ROOK];
        while (bCopy != 0UL) {
            sbyte sq          = BB.LS1BReset(ref bCopy);
            byte pawnCount    = BB.Popcount(Consts.FileMask[sq & 7] & (wPawns | bPawns));
            byte ownPawnCount = BB.Popcount(Consts.FileMask[sq & 7] & bPawns);

            // we subtract this time for black
            if (pawnCount == 0)
                eval -= OpenFileRookBonus;

            else if (ownPawnCount == 0)
                eval -= SemiOpenFileRookBonus;
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
        //    eval -= (short)Math.Min(pieceCount >> 3, SeventhRankRookBonus);

        return eval;
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //private static short QueenEval(ulong q, Color col, int piece_count) {
    //    int eval = 0;
    //
    //    return (short)eval;
    //}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short KingEval([In, ReadOnly(true)] in Board board) {
        short eval = 0;

        // same color pieces around the king - protection
        ulong wProtection = King.GetKingTargets(board.Pieces[(byte)Color.WHITE][(byte)PType.KING], board.WOccupied);
        ulong bProtection = King.GetKingTargets(board.Pieces[(byte)Color.BLACK][(byte)PType.KING], board.BOccupied);

        // bonus for the number of friendly pieces protecting the king
        short wProtBonus = (short)(BB.Popcount(wProtection) * 2);
        short bProtBonus = (short)(BB.Popcount(bProtection) * 2);

        eval += (short)(wProtBonus - bProtBonus);

        return eval;
    }
}