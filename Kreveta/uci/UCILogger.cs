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
    
    // log something to the console
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Log(string? msg, bool nl = true)
        => Output.Write(msg + (nl ? '\n' : string.Empty));

    // log a set of statistics to the console in a nice format. each statistic consists of a name,
    // and any value that can be turned into a string. furthermore, certain integral types are
    // formatted with a group separator to make them more readable
    internal static void LogStats(bool forcePrint, bool header, params ReadOnlySpan<(string Name, object Data)> stats) {
        const string STATS_HEADER = "---STATS-------------------------------";
        const string STATS_AFTER  = "---------------------------------------";
        
        // this sadly cannot be const in case we modify the strings above
        int dataOffset = STATS_HEADER.Length - 16;

        // printing statistics can be toggled via the PrintStats option. printing can, however, be
        // forced when we are, for example, printing perft results (or else perft would be meaningless)
        if (!Options.PrintStats && !forcePrint)
            return;

        if (header) Log(STATS_HEADER);

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

        if (header) Log(STATS_AFTER);
    }
}