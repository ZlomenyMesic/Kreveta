//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Specify IFormatProvider
#pragma warning disable CA1305

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable InconsistentNaming

namespace Kreveta;

internal static class Options {
    
    private enum OpType : byte {
        CHECK, SPIN, BUTTON, STRING
    }

    [StructLayout(LayoutKind.Auto)]
    private record struct Option {

        internal required string Name;
        internal required OpType Type;

        internal          string MinValue;
        internal          string MaxValue;
        internal required string DefaultValue;
        internal required string Value;
    }

    private static readonly Option[] options = [

        // should the engine use its own opening book?
        new() {
            Name = nameof(OwnBook),
            Type = OpType.CHECK,

            // standard ToString returns True and False, which
            // isn't what we want. this works just fine
            DefaultValue = "true",
            Value        = "true"
        },

        // modify the size of the hash table (transpositions)
        new() {
            Name = nameof(Hash),
            Type = OpType.SPIN,

            MinValue = "1",
            MaxValue = "2048",

            DefaultValue = "40",
            Value        = "40"
        },


        // special fancy info, warning and error logs
        // using the NeoKolors library by KryKom
        new() {
            Name = nameof(NKLogs),
            Type = OpType.CHECK,

            DefaultValue = "false",
            Value        = "false"
        },
    ];

    // changing the names of these properties also changes
    // the name of the actual option, which isn't great.
    // non-custom options (OwnBook and Hash) need to keep
    // their names in order to be used properly by the GUI
    [ReadOnly(true)]
    internal static bool OwnBook 
        => options[0].Value == "true";

    [ReadOnly(true)]
    internal static int Hash 
        => int.Parse(options[1].Value);

    [ReadOnly(true)]
    internal static bool NKLogs 
        => options[2].Value == "true";
    
    // we could just rename the items in the enum to be lowercase, but
    // that doesn't look good at all, so we use an extension method instead
    private static string GetName(this OpType type)
        => type switch {
            OpType.CHECK  => "check",
            OpType.SPIN   => "spin",
            OpType.BUTTON => "button",
            OpType.STRING => "string",
            _ => string.Empty
        };

    internal static void Print() {
        foreach (var opt in options) {
            StringBuilder sb = new();

            sb.Append($"option name {opt.Name}");
            
            sb.Append($" type {opt.Type.GetName()}");

            switch (opt.Type) {
                case OpType.CHECK or OpType.STRING:
                    sb.Append($" default {opt.DefaultValue}");
                    break;
                
                case OpType.SPIN:
                    sb.Append($" default {opt.DefaultValue} min {opt.MinValue} max {opt.MaxValue}");
                    break;
            }

            UCI.Log(sb.ToString());
        }
    }

    internal static void SetOption(ReadOnlySpan<string> tokens) {
        if (tokens.Length < 3 || tokens[1] != "name") {
            goto invalid_syntax;
        }

        for (int i = 0; i < options.Length; i++) {
            if (options[i].Name != tokens[2]) 
                continue;
            
            switch (options[i].Type) {
                case OpType.BUTTON: {
                    //
                    //
                    //
                    return;
                }

                case OpType.CHECK: {
                    // if (tokens is [_, _, _, "value", "true" or "false"]) {
                    //     options[i].Value = tokens[4];
                    //
                    //     return;
                    //
                    // } goto invalid_syntax;
                    break;
                }

                case OpType.SPIN: {
                    if (tokens is [_, _, _, "value", _]) {

                        if (!long.TryParse(tokens[4], out long val))
                            goto invalid_syntax;
                        
                        long minVal = long.Parse(options[i].MinValue);
                        long maxVal = long.Parse(options[i].MaxValue);

                        if (val < minVal || val > maxVal) 
                            goto val_out_of_range;

                        options[i].Value = tokens[4];
                        return;

                    } goto invalid_syntax;
                }

                case OpType.STRING: {
                    if (tokens is [_, _, _, "value", _, ..]) {

                        options[i].Value = string.Empty;
                        for (byte j = 3; j < tokens.Length; j++) {

                            options[i].Value += $" {tokens[j]}";
                            options[i].Value = options[i].Value.Trim();
                        }
                        return;

                    } goto invalid_syntax;
                }
            }
        }

        UCI.Log($"unsupported option {tokens[2]}", UCI.LogLevel.ERROR);
        return;

        invalid_syntax:
        UCI.Log("invalid setoption syntax", UCI.LogLevel.ERROR);
        return;
        
        val_out_of_range:
        UCI.Log("option value out of range", UCI.LogLevel.ERROR);
    }
}

#pragma warning restore CA1305
#pragma warning restore IDE0079