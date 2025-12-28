//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
namespace Kreveta.uci.options;

internal static partial class Options {
    private static class Default {
        
        // the default values for all UCI options (visible with 'uci')
        internal const bool   DPolyglotUseBook = false;
        internal const string DPolyglotBook    = "";
        internal const long   DPolyglotRisk    = 0L;
        
        internal const long   DHash            = 128L;
        
        internal const bool   DPrintStats      = false;
    }
}