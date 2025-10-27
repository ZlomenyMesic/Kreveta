//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.InteropServices;

namespace Kreveta.movegen;

/*
generating moves can be fairly slow, and for this reason most modern engines
use the so-called lookup tables. these lookup tables are indexed by the square
index and occupancy (surrounding pieces), and contain precomputed move targets
for every single combination of these two

since the occupancies could get very large, we must also compress them into
a reasonable size. to do this task, magic bitboards are often used. occupancies
are multiplied by "magic" numbers, which perfectly hashes them into a dense range
    
just to demonstrate - let's say we have a rook along with three enemy pawns:

  - - p - R - p p      occupancy: 00101011

now this rook has obviously 4 legal moves (2 quiet ones and 2 captures). but as
you may notice, this number doesn't change when the irrelevant squares are changed
(the squares hidden behind the pawns - blockers). so all of these positions have
the exact same legal moves, while the occupancy is very different:

  - - p - R - p -      occupancy: 00101010
  p p p - R - p p      occupancy: 11101011
  - p p - R - p p      occupancy: 01101011
  p - p - R - p -      occupancy: 10101010

and now magic numbers come into play. with the right magic number, every single
one of the shown positions would hash into something like this

  - - p - R - p -      occupancy: 00101010

all the irrelevant pieces would get removed, and the occupancy would be compressed
*/
internal static unsafe class LookupTables {

    internal static readonly ulong* KingTargets     = (ulong*)NativeMemory.AlignedAlloc(64 * sizeof(ulong),      64);
    internal static readonly ulong* KnightTargets   = (ulong*)NativeMemory.AlignedAlloc(64 * sizeof(ulong),      64);
    internal static readonly ulong* RankTargets     = (ulong*)NativeMemory.AlignedAlloc(64 * 64 * sizeof(ulong), 4);
    internal static readonly ulong* FileTargets     = (ulong*)NativeMemory.AlignedAlloc(64 * 64 * sizeof(ulong), 4);
    internal static readonly ulong* AntidiagTargets = (ulong*)NativeMemory.AlignedAlloc(64 * 64 * sizeof(ulong), 4);
    internal static readonly ulong* DiagTargets     = (ulong*)NativeMemory.AlignedAlloc(64 * 64 * sizeof(ulong), 4);

    private static bool _memoryFreed;

    // all lookup tables need to be initialized right as the engine launches
    static LookupTables() 
        // what the actual fuck is this syntactic sugar? how is this legal C#?
        => ((Action)InitKingTargets + InitKnightTargets + InitRankTargets + InitFileTargets + InitAntidiagTargets + InitDiagTargets)();

    // king and knight targets don't use the occupancy as explained above. the target
    // bitboard includes every single landing square, regardless of other pieces. only
    // later are these moves filtered if the square is blocked
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

    private static void InitRankTargets() {
        for (int i = 0; i < 64; i++) {
            //RankTargets[i] = (ulong*)NativeMemory.AlignedAlloc(64 * sizeof(ulong), 64);

            for (int o = 0; o < 64; o++) {
                ulong occ = (ulong)o << 1;
                ulong targets = 0;

                // sliding to the right until we hit a blocker
                int slider = (i & 7) + 1;
                while (slider <= 7) {
                    targets |= 1UL << slider;
                    if (BB.IsBitSet(occ, slider)) break;
                    slider++;
                }

                // sliding to the left
                slider = (i & 7) - 1;
                while (slider >= 0) {
                    targets |= 1UL << slider;
                    if (BB.IsBitSet(occ, slider)) break;
                    slider--;
                }

                // move to correct rank
                targets <<= 8 * (i >> 3);
                RankTargets[i * 64 + o] = targets;
            }
        }
    }
    private static void InitFileTargets() {
        for (int i = 0; i < 64; i++) {
            //FileTargets[i] = (ulong*)NativeMemory.AlignedAlloc(64 * sizeof(ulong), 64);

            for (int o = 0; o < 64; o++) {
                ulong targets = 0;
                ulong rankTargets = RankTargets[(7 - i / 8) * 64 + o];

                // rotate rank targets
                for (int bit = 0; bit < 8; bit++) {
                    if (!BB.IsBitSet(rankTargets, bit))
                        continue;
                    
                    targets |= 1UL << (i & 7) + 8 * (7 - bit);
                }

                FileTargets[i * 64 + o] = targets;
            }
        }
    }

    private static void InitAntidiagTargets() {
        for (int i = 0; i < 64; i++) {
            //AntidiagTargets[i] = (ulong*)NativeMemory.AlignedAlloc(64 * sizeof(ulong), 64);

            for (int o = 0; o < 64; o++) {
                int diag = (i >> 3) - (i & 7);

                ulong targets = 0;
                ulong rankTargets = diag > 0 
                    ? RankTargets[(i & 7) * 64 + o] 
                    : RankTargets[i / 8 * 64 + o];

                for (int bit = 0; bit < 8; bit++) {

                    // rotate rank moves
                    if (!BB.IsBitSet(rankTargets, bit))
                        continue;
                    
                    int file, rank;
                        
                    if (diag >= 0) {
                        rank = diag + bit;
                        file = bit;
                    } 
                    else {
                        file = bit - diag;
                        rank = bit;
                    }

                    if (file is >= 0 and <= 7 && rank is >= 0 and <= 7) {
                        targets |= 1UL << file + 8 * rank;
                    }
                }

                AntidiagTargets[i * 64 + o] = targets;
            }
        }
    }

    private static void InitDiagTargets() {
        for (int i = 0; i < 64; i++) {
            //DiagTargets[i] = (ulong*)NativeMemory.AlignedAlloc(64 * sizeof(ulong), 64);

            for (int o = 0; o < 64; o++) {
                int diag = (i >> 3) + (i & 7);

                ulong targets = 0;
                ulong rankTargets = diag > 7 
                    ? RankTargets[(7 - i / 8) * 64 + o] 
                    : RankTargets[(i & 7)     * 64 + o];

                for (int bit = 0; bit < 8; bit++) {
                    
                    // rotate rank moves
                    if (!BB.IsBitSet(rankTargets, bit))
                        continue;
                    
                    int rank, file;
                        
                    if (diag >= 7) {
                        rank = 7 - bit;
                        file = diag - 7 + bit;
                    } 
                    else {
                        rank = diag - bit;
                        file = bit;
                    }

                    if (file is >= 0 and <= 7 && rank is >= 0 and <= 7) {
                        targets |= 1UL << file + 8 * rank;
                    }
                }

                DiagTargets[i * 64 + o] = targets;
            }
        }
    }
    
    internal static void Clear() {
        if (_memoryFreed) 
            return;
        
        NativeMemory.AlignedFree(KingTargets);
        NativeMemory.AlignedFree(KnightTargets);
        NativeMemory.AlignedFree(RankTargets);
        NativeMemory.AlignedFree(FileTargets);
        NativeMemory.AlignedFree(AntidiagTargets);
        NativeMemory.AlignedFree(DiagTargets);
        
        _memoryFreed = true;
    }
}
