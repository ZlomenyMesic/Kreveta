//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.InteropServices;

namespace Kreveta.uci.options;

internal static partial class Options {
    [StructLayout(LayoutKind.Sequential)]
    private sealed record Option {
        public required string Name { get; init; }
        public required OpType Type { get; init; }

        // min and max values are only used with the
        // "spin" option type
        internal long MinValue;
        internal long MaxValue;

        // default value is displayed when the "uci"
        // command is received. current value of the
        // option is stored in Value
        internal required object DefaultValue;
        internal required object Value;
    }
}