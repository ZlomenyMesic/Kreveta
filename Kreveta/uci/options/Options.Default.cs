//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
namespace Kreveta.uci.options;

internal static partial class Options {
    private static class Default {
        
        // the default values for all UCI options (visible with 'uci')
        internal const bool   DPolyglotUseBook   = false;
        internal const string DPolyglotBook      = "";
        internal const long   DPolyglotRisk      = 0L;
        
        internal const long   DHash              = 32L;
        internal const bool   DUsePerftHash      = true;
        
        internal const long   DUCI_Elo           = 1500;
        internal const bool   DUCI_LimitStrength = false;
        internal const bool   DUCI_AnalyseMode   = false;
        internal const string DUCI_EngineAbout   = $"{Program.Name}-{Program.Version} by {Program.Author}";
        
        internal const bool   DPrintStats        = false;
        
        internal const bool   DPlayWorst         = false;
    }
}