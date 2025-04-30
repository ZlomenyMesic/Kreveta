//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

namespace Kreveta.movegen.pieces;

internal static class Bishop {
    internal static unsafe ulong GetBishopTargets(ulong bishop, ulong free, ulong occupied) {
        ulong targets = 0;

        int sq = BB.LS1B(bishop);

        int diag = 7 + (sq >> 3) - (sq & 7);
        int occupancy = (int)((occupied & Consts.AntidiagMask[diag]) 
            * Consts.AntidiagMagic[diag] >> 57);
        
        targets |= LookupTables.AntidiagTargets[sq][occupancy & 63];

        diag = (sq >> 3) + (sq & 7);
        
        occupancy = (int)(
            (occupied & Consts.DiagMask[diag]) 
            * Consts.DiagMagic[diag] >> 57);
        
        targets |= LookupTables.DiagTargets[sq][occupancy & 63];

        return targets & free;
    }
}
