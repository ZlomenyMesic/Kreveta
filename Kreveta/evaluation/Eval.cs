//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.movegen.pieces;

using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.evaluation;

internal static class Eval {
    
    // the side to play gets a small bonus
    private const sbyte SideToMoveBonus = 5;

    // POSITION STRUCTURE BONUSES & MALUSES
    internal static int DoubledPawnMalus   = -44;
    internal static int IsolatedPawnMalus  = -156;
    internal static int PassedPawnBonus    = 70;
    internal static int ConnectedPawnBonus = 33;
    internal static int BlockedPawnMalus   = -30;
    
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
        eval += (short)((PawnEval(pieces[0], pieces[6], Color.WHITE, wOccupied)
                         - PawnEval(pieces[6], pieces[0], Color.BLACK, bOccupied)) / 10);
        
        eval += RookEval(pieces, pieceCount);
        eval += KingEval(pieces, wOccupied, bOccupied);

        if (Check.IsKingChecked(in board, Color.WHITE)) eval -= 30;
        if (Check.IsKingChecked(in board, Color.BLACK)) eval += 30;

        // side to move should also get a slight advantage
        eval += (short)(board.Color == Color.WHITE ? SideToMoveBonus : -SideToMoveBonus);
        return eval;
    }
    
    // bonuses or penalties for pawn structure
    private static short PawnEval(ulong pawns, ulong enemyPawns, Color col, ulong friendlyPieces) {
        int eval = 0;
        
        ulong copy = pawns;
        while (copy != 0UL) {
            byte sq = BB.LS1BReset(ref copy);
            ulong file = Consts.RelevantFileMask[sq & 7];
            
            int fileOcc = (int)ulong.PopCount(file & pawns);
            eval += (short)((fileOcc - 1) * DoubledPawnMalus);
            
            ulong adjFiles  = AdjFiles[sq & 7];
            int   adjOcc    = (int)ulong.PopCount(adjFiles & pawns);
            int   oppAdjOcc = (int)ulong.PopCount(adjFiles & enemyPawns);
            
            eval += (short)(fileOcc   != adjOcc ? 0 : IsolatedPawnMalus);
            eval += (short)(oppAdjOcc != 0      ? 0 : PassedPawnBonus * (col == Color.WHITE ? 8 - (sq >> 3) : (sq >> 3)));
            
            ulong targets = Pawn.GetPawnCaptureTargets(sq, 64, col, pawns);
            eval += (short)((int)ulong.PopCount(targets) * ConnectedPawnBonus);
            
            if (col == Color.WHITE && (1UL << sq - 8 & friendlyPieces) != 0UL)
                eval += BlockedPawnMalus;

            else if (col == Color.BLACK && (1UL << sq + 8 & friendlyPieces) != 0UL)
                eval += BlockedPawnMalus;
        }

        /*short eval = 0;
        for (int i = 0; i < 8; i++) {
            ulong file = Consts.RelevantFileMask[i];

            // count the number of pawns on the file
            int fileOcc = (int)ulong.PopCount(file & p);
            if (fileOcc == 0)
                continue;

            // penalties for doubled pawns. by subtracting 1 we can simultaneously
            // penalize all sorts of stacked pawns while not checking single ones
            eval += (short)((fileOcc - 1) * DoubledPawnMalus);

            // if the number of pawns on current file is equal to the number of pawns
            // on the current plus adjacent files, we know the pawn/s are isolated
            int adjOcc = (int)ulong.PopCount(AdjFiles[i] & p);

            // isolani is an isolated pawn on the d-file. this usually tends
            // to be the worst isolated pawn, so there's a higher penalty
            sbyte isolani = i == 3 ? IsolaniAddPenalty : (sbyte)0;
            eval += (short)(fileOcc != adjOcc ? 0 : IsolatedPawnPenalty + isolani);
        }

        ulong copy = p;
        while (copy != 0UL) {
            byte sq = BB.LS1BReset(ref copy);

            if (col == Color.WHITE ? sq < 40 : sq > 23) {

                // add a bonus for connected pawns in the opponent's half of the board.
                // this should (and hopefully does) increase the playing strength in
                // endgames and also allow better progressing into endgames
                ulong targets = Pawn.GetPawnCaptureTargets(sq, 64, col, p);
                eval += (short)((int)ulong.PopCount(targets) * ConnectedPawnBonus);
            }

            // penalize blocked pawns - pawns that have a friendly piece directly in
            // front of them and thus cannot push further. in order to push this pawn,
            // you first have to move the other piece, which makes it worse.
            if (col == Color.WHITE && (1UL << sq - 8 & wOccupied) != 0UL)
                eval += BlockedPawnPenalty;

            else if (col == Color.BLACK && (1UL << sq + 8 & bOccupied) != 0UL)
                eval += BlockedPawnPenalty;
        }*/

        return (short)eval;
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

        return eval;
    }

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