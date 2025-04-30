//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

namespace Kreveta.movegen.pieces;

internal static class Rook {
    
    // returns a bitboard of all possible move targets (ending squares)
    // of a certain rook. the free parameter should include empty and
    // enemy squares, while occupied should contain all occupied squares
    internal static unsafe ulong GetRookTargets(ulong rook, ulong free, ulong occupied) {

        // get the square index
        int sq = BB.LS1B(rook);

        // the occupancy is then used to index the
        // target bitboard in the lookup table
        int occupancy
            
            // first we take only the relevant rank bits
            = (int)((occupied & Consts.RelevantRankMask[sq >> 3])
                              
                    // now we shift this bitboard by the rounded square
                    // index (this essentially puts it on the first rank)
                    >> ((sq >> 3) << 3));
        
        // now we take the lookup targets
        ulong targets = LookupTables.RankTargets[sq][(occupancy >> 1) & 63];

        // this time we want the relevant file
        occupancy = (int)((occupied & Consts.RelevantFileMask[sq & 7])
            
            // and we multiply this by a magic number and then shift
            // it to once again allow correct lookup table indexing
            * Consts.FileMagic[sq & 7] >> 57);
        
        // and we add these file targets to the rank targets
        targets |= LookupTables.FileTargets[sq][occupancy & 63];

        // we now have to & this result with free to avoid own captures
        // (occupied contains both friendly and enemy pieces, so at this
        // point we just & the targets with empty squares or enemy pieces)
        return targets & free;
    }
}
