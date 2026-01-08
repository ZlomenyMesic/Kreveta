//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.uci.options;

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace Kreveta.uci;

internal static partial class UCI {
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Log(string? msg, bool nl = true)
        => Output.Write(msg + (nl ? '\n' : string.Empty));

    // had to use this crazy syntax just because it exists
    internal static void LogMultiple(__arglist) {
        var iter = new ArgIterator(__arglist);

        while (iter.GetRemainingCount() > 0) {
            var obj = TypedReference.ToObject(iter.GetNextArg());
            Log(obj.ToString());
        }
    }

    internal static void LogStats(bool forcePrint, params ReadOnlySpan<(string Name, object Data)> stats) {
        const string STATS_HEADER = "---STATS-------------------------------";
        const string STATS_AFTER  = "---------------------------------------";
        
        // this sadly cannot be const in case we modify the strings above
        int dataOffset = STATS_HEADER.Length - 16;

        // printing statistics can be toggled via the PrintStats option.
        // printing can, however, be forced when we are, for example,
        // printing perft results (or else perft would be meaningless)
        if (!Options.PrintStats && !forcePrint)
            return;

        Log(STATS_HEADER);

        foreach ((string Name, object Data) stat in stats) {
            string  name = stat.Name + new string(' ', dataOffset - stat.Name.Length);
            string? data = stat.Data.ToString();

            if (data is null)
                continue;

            // number stats are formatted in a nice way to separate number groups with ','
            if (stat.Data is long or ulong or int or short) {
                var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                format.NumberGroupSeparator = ",";

                data = Convert.ToInt64(stat.Data, null).ToString("N0", format);
            }

            Log($"{name}{data}");
        }

        Log(STATS_AFTER);
    }
}