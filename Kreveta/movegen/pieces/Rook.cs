//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

namespace Kreveta.movegen.pieces;

internal static class Rook {
    internal static unsafe ulong GetRookTargets(ulong rook, ulong free, ulong occupied) {
        ulong targets = 0;

        int sq = BB.LS1B(rook);

        int occupancy = (int)((occupied & Consts.RelevantRankMask[sq >> 3]) >> (8 * (sq >> 3)));
        targets |= LookupTables.RankTargets[sq][(occupancy >> 1) & 63];

        occupancy = (int)((occupied & Consts.RelevantFileMask[sq & 7]) * Consts.FileMagic[sq & 7] >> 56);
        targets |= LookupTables.FileTargets[sq][(occupancy >> 1) & 63];

        return targets & free;
    }
}
