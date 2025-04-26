//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Specify IFormatProvider
#pragma warning disable CA1305

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable InconsistentNaming

namespace Kreveta;

internal static class Options {

    private const bool DefaultOwnBook = true;
    private const bool DefaultNKLogs  = true;
    private const long DefaultHash    = 40;

    private enum OptionType {
        CHECK, SPIN, BUTTON, STRING
    }

    private struct Option {

        internal string Name;
        internal OptionType Type;

        internal string MinValue;
        internal string MaxValue;
        internal string DefaultValue;
        internal string Value;
    }

    private static readonly Option[] options = [

        // should the engine use its own opening book?
        new() {
            Name = nameof(OwnBook),
            Type = OptionType.CHECK,

            // standard ToString returns True and False, which
            // isn't what we want. this works just fine
            DefaultValue = DefaultOwnBook ? "true" : "false",
            Value = DefaultOwnBook ? "true" : "false"
        },

        // modify the size of the hash table (transpositions)
        new() {
            Name = nameof(Hash),
            Type = OptionType.SPIN,

            MinValue = "1",
            MaxValue = "2048",

            DefaultValue = DefaultHash.ToString(),
            Value        = DefaultHash.ToString()
        },


        // special fancy info, warning and error logs
        // using the NeoKolors library by KryKom
        new() {
            Name = nameof(NKLogs),
            Type = OptionType.CHECK,

            DefaultValue = DefaultNKLogs ? "true" : "false",
            Value = DefaultNKLogs ? "true" : "false"
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string OTypeToString(OptionType type) {
        return type switch {
            OptionType.CHECK  => "check",
            OptionType.SPIN   => "spin",
            OptionType.BUTTON => "button",
            OptionType.STRING => "string",
            _ => string.Empty
        };
    }

    internal static void Print() {
        foreach (var opt in options) {
            StringBuilder sb = new();

            sb.Append($"option name {opt.Name}");
            
            sb.Append($" type {OTypeToString(opt.Type)}");

            switch (opt.Type) {
                case OptionType.CHECK or OptionType.STRING:
                    sb.Append($" default {opt.DefaultValue}");
                    break;
                
                case OptionType.SPIN:
                    sb.Append($" default {opt.DefaultValue} min {opt.MinValue} max {opt.MaxValue}");
                    break;
            }

            UCI.Log(sb.ToString());
        }
    }

    internal static void SetOption(string[] tokens) {
        if (tokens.Length < 3 || tokens[1] != "name") {
            goto invalid_syntax;
        }

        for (int i = 0; i < options.Length; i++) {
            if (options[i].Name != tokens[2]) 
                continue;
            
            switch (options[i].Type) {
                case OptionType.BUTTON: {
                    //
                    //
                    //
                    return;
                }
                case OptionType.CHECK: {
                    //if (tokens.Length == 5 && tokens[3] == "value"
                    //    && (tokens[4] == "true" || tokens[4] == "false")) {

                    //    options[i].Value = tokens[4];

                    //    return;

                    //} else goto invalid_syntax;
                    break;
                }
                case OptionType.SPIN: {
                    if (tokens is [_, _, _, "value", _]) {

                        if (!long.TryParse(tokens[4], out _))
                            goto invalid_syntax;

                        options[i].Value = tokens[4];
                        return;

                    } goto invalid_syntax;
                }
                case OptionType.STRING: {
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
    }
}

#pragma warning restore CA1305
#pragma warning restore IDE0079