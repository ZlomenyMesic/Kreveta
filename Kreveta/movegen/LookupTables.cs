//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1810

using System;
using System.Runtime.InteropServices;

namespace Kreveta.movegen;

internal static unsafe class LookupTables {

    internal static readonly ulong* PawnCaptTargets;
    internal static readonly ulong* KingTargets;
    internal static readonly ulong* KnightTargets;
    
    // when escaping check or ensuring move legality, these star shapes are used
    internal static readonly ulong* KingStars;
    
    private static bool _memoryFreed;

    // all lookup tables need to be initialized right as the engine launches
    static LookupTables() {
        PawnCaptTargets = (ulong*)NativeMemory.AlignedAlloc(2  * 64 * sizeof(ulong), 64);
        KingTargets     = (ulong*)NativeMemory.AlignedAlloc(64 * sizeof(ulong),      64);
        KnightTargets   = (ulong*)NativeMemory.AlignedAlloc(64 * sizeof(ulong),      64);
        KingStars       = (ulong*)NativeMemory.AlignedAlloc(64 * sizeof(ulong),      64);
        
        InitPawnTargets();
        InitKingTargets();
        InitKnightTargets();

        InitKingStars();
    }
    
    // pawn, king and knight targets don't use the occupancy as explained above. the
    // target bitboard includes every single landing square, regardless of other pieces.
    // only later are these moves filtered if the square is blocked
    private static void InitPawnTargets() {
        // pawns cannot exist on 1st and 8th ranks, but the
        // loop cannot be shortened for an unknown cause
        for (int i = 0; i < 64; i++) {
            ulong pawn = 1UL << i;
            
            // in both cases we ensure the pawn hasn't jumped to the other side of the board
            // captures to the left
            ulong wleft = pawn >> 9 & 0x7F7F7F7F7F7F7F7F;
            ulong bleft = pawn << 7 & 0x7F7F7F7F7F7F7F7F;

            // captures to the right
            ulong wright = pawn >> 7 & 0xFEFEFEFEFEFEFEFE;
            ulong bright = pawn << 9 & 0xFEFEFEFEFEFEFEFE;
            
            PawnCaptTargets[i     ] = wleft | wright;
            PawnCaptTargets[i + 64] = bleft | bright;
        }
    }
    
    private static void InitKingTargets() {
        for (int i = 0; i < 64; i++) {
            ulong king = 1UL << i;

            // starting, right and left square
            ulong sides = king << 1 & 0xFEFEFEFEFEFEFEFE | king >> 1 & 0x7F7F7F7F7F7F7F7F;
            king |= sides;

            // also move these up and down and remove the king from the center
            ulong all = sides | king >> 8 | king << 8;
            KingTargets[i] = all;
        }
    }

    private static void InitKnightTargets() {
        for (int i = 0; i < 64; i++) {
            ulong knight = 1UL << i;

            // right and left sqaures
            // again make sure we're not jumping across the whole board
            ulong right = knight << 1 & 0xFEFEFEFEFEFEFEFE;
            ulong left  = knight >> 1 & 0x7F7F7F7F7F7F7F7F;

            // shift the side squares up and down to generate "vertical" moves
            ulong vertical = (right | left) >> 16 
                           | (right | left) << 16;

            // shift the side squares to the side again
            right = right << 1 & 0xFEFEFEFEFEFEFEFE;
            left  = left  >> 1 & 0x7F7F7F7F7F7F7F7F;

            // move these up and down to generate "horizontal" moves
            ulong horizontal = (right | left) >> 8 
                             | (right | left) << 8;

            KnightTargets[i] = vertical | horizontal;
        }
    }

    private static void InitKingStars() {
        for (int i = 0; i < 64; i++) {
            ulong king = 1UL << i;
            
            ulong knight = KnightTargets[i];
            ulong bishop = Pext.GetBishopTargets(i, ulong.MaxValue, 0UL);
            ulong rook   = Pext.GetRookTargets(i, ulong.MaxValue, 0UL);
            
            KingStars[i] = king | knight | bishop | rook;
        }
    }
    
    internal static void Clear() {
        if (_memoryFreed) 
            return;
        
        NativeMemory.AlignedFree(PawnCaptTargets);
        NativeMemory.AlignedFree(KingTargets);
        NativeMemory.AlignedFree(KnightTargets);
        
        _memoryFreed = true;
    }
}

#pragma warning restore CA1810