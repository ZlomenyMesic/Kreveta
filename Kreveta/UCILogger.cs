// ReSharper disable InconsistentNaming

using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// ReSharper disable once RedundantUsingDirective
using NK = NeoKolors.Console;

namespace Kreveta;

internal static partial class UCI {
    internal enum LogLevel : byte {
        INFO, WARNING, ERROR, RAW
    }

    private static void InitNK() {
        const string NKLogFilePath = @".\out.log";
        
        try {
            var nkOutput = new StreamWriter(NKLogFilePath);

            NK::NKDebug.Logger.Output         = nkOutput;
            NK::NKDebug.Logger.SimpleMessages = true;
        }

        // we are catching a "general exception type", because we have
        // zero idea, which type of exception NeoKolors might throw.
        catch (Exception ex)
            when (LogException("NKLogger initialization failed", ex)) { }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Log(string? msg, LogLevel level = LogLevel.RAW, bool logIntoFile = true) {
        if (logIntoFile)
            Task.Run(() => LogIntoFile(msg ?? string.Empty, level));

        Output.WriteLine(msg);
    }

    // had to use this crazy syntax just because it exists
    internal static void LogMultiDebug(__arglist) {
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

        foreach (var stat in stats) {
            string  name = stat.Name + new string(' ', dataOffset - stat.Name.Length);
            string? data = stat.Data.ToString();

            if (data is null)
                continue;

            // number stats are formatted in a nice way to separate number groups with ','
            if (stat.Data is long or ulong or int) {
                var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                format.NumberGroupSeparator = ",";

                data = Convert.ToInt64(stat.Data, null).ToString("N0", format);
            }

            Log($"{name}{data}");
        }

        Log(STATS_AFTER);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once MemberCanBePrivate.Global
    internal static bool LogException(string context, Exception ex, bool logIntoFile = true) {
        Log($"{context}: {ex.Message}", LogLevel.ERROR, logIntoFile);
        return true;
    }

    // combining sync and async code is generally a bad idea, but we must avoid slowing
    // down the code if something takes too long in NK (although it's probably unlikely)
    private static async Task LogIntoFile(string msg, LogLevel level = LogLevel.RAW) {
        if (!Options.NKLogs)
            return;

        // using KryKom's NeoKolors library for fancy logs
        // this option can be toggled via the FancyLogs option
        try {

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (level) {
                case LogLevel.INFO:    await Task.Run(() => NK::NKDebug.Logger.Info(msg)).ConfigureAwait(false);  break;
                case LogLevel.WARNING: await Task.Run(() => NK::NKDebug.Logger.Warn(msg)).ConfigureAwait(false);  break;
                case LogLevel.ERROR:   await Task.Run(() => NK::NKDebug.Logger.Error(msg)).ConfigureAwait(false); break;

                default:               await Task.Run(() => NK::NKDebug.Logger.Info(msg)).ConfigureAwait(false);  break;
            }
        } catch (Exception ex)
              when (LogException("NKLogger failed to log into file", ex, false)) { }
    }
}