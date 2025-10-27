//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
namespace Kreveta.uci;

internal static partial class Options {
    private static class Default {
        internal const bool   DPolyglotUseBook = true;
        internal const string DPolyglotBook    = "";
        internal const long   DPolyglotRisk    = 0L;
        internal const long   DHash            = 64L;
        internal const bool   DNKLogs          = false;
        internal const bool   DPrintStats      = true;
    }
}