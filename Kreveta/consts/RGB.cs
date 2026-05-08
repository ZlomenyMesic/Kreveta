//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming

namespace Kreveta.consts;

// sometimes we want a bit of color in the console
internal static class RGB {
    internal const string Reset = "\e[0m";
    
    // used for the header when launching the engine
    internal const string Peach = "\e[38;2;255;156;124m";
    
    // used for nicer 'eval' and 'perft' outputs
    internal const string Red    = "\e[38;2;224;142;142m";
    internal const string Green  = "\e[38;2;152;195;121m";
    internal const string Blue   = "\e[38;2;120;170;255m";
    internal const string Yellow = "\e[38;2;229;192;123m";
    
    // used to display the current position (piece colors)
    internal const string White  = "\e[38;2;255;255;255m";
    internal const string Black  = "\e[38;2;0;0;0m";
    
    // light/dark squares on the chessboard
    internal const string BGLight = "\e[48;2;128;128;128m";
    internal const string BGDark  = "\e[48;2;80;80;80m";
}