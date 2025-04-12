//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;
using System.Text;

namespace Kreveta;

internal static class Options {

    private const bool DefaultOwnBook = true;
    private const bool DefaultNKLogs = true;
    private const long DefaultHash = 40;

    internal enum OptionType {
        CHECK, SPIN, BUTTON, STRING
    }

    internal struct Option {

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
            Name = "OwnBook",
            Type = OptionType.CHECK,

            // standard ToString returns True and False, which
            // isn't what we want. this works just fine
            DefaultValue = DefaultOwnBook ? "true" : "false",
            Value = DefaultOwnBook ? "true" : "false"
        },

        // modify the size of the hash table (transpositions)
        new() {
            Name = "Hash",
            Type = OptionType.SPIN,

            MinValue = "1",
            MaxValue = "2048",

            DefaultValue = DefaultHash.ToString(),
            Value = DefaultHash.ToString()
        },


        // special fancy info, warning and error logs
        // using the NeoKolors library by KryKom
        new() {
            Name = "NKLogs",
            Type = OptionType.CHECK,

            DefaultValue = DefaultNKLogs ? "true" : "false",
            Value = DefaultNKLogs ? "true" : "false"
        },
    ];

    internal static bool OwnBook {
        get => options[0].Value == "true";
    }

    internal static int Hash {
        get => int.Parse(options[1].Value);
    }

    internal static bool NKLogs {
        get => options[2].Value == "true";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string OTypeToString(OptionType type) {
        return type switch {
            OptionType.CHECK  => "check",
            OptionType.SPIN   => "spin",
            OptionType.BUTTON => "button",
            OptionType.STRING => "string",
            _ => ""
        };
    }

    internal static void Print() {
        foreach (Option opt in options) {
            StringBuilder sb = new();

            sb.Append($"option name {opt.Name}");

            string type = OTypeToString(opt.Type);
            sb.Append($" type {type}");

            if (opt.Type == OptionType.CHECK || opt.Type == OptionType.STRING) {
                sb.Append($" default {opt.DefaultValue}");
            } else if (opt.Type == OptionType.SPIN) {
                sb.Append($" default {opt.DefaultValue} min {opt.MinValue} max {opt.MaxValue}");
            }

            UCI.Log(sb.ToString(), UCI.LogLevel.RAW);
        }
    }

    internal static void SetOption(string[] tokens) {
        if (tokens.Length < 3 || tokens[1] != "name") {
            goto invalid_syntax;
        }

        for (int i = 0; i < options.Length; i++) {
            if (options[i].Name == tokens[2]) {

                if (options[i].Type == OptionType.BUTTON) {
                    //
                    //
                    //
                    return;
                }

                if (options[i].Type == OptionType.CHECK) {
                    //if (tokens.Length == 5 && tokens[3] == "value"
                    //    && (tokens[4] == "true" || tokens[4] == "false")) {

                    //    options[i].Value = tokens[4];

                    //    return;

                    //} else goto invalid_syntax;
                }

                if (options[i].Type == OptionType.SPIN) {
                    if (tokens.Length == 5 && tokens[3] == "value") {

                        if (!long.TryParse(tokens[4], out _))
                            goto invalid_syntax;

                        options[i].Value = tokens[4];
                        return;

                    } else goto invalid_syntax;
                }

                if (options[i].Type == OptionType.STRING) {
                    if (tokens.Length >= 5 && tokens[3] == "value") {

                        options[i].Value = "";
                        for (int j = 3; j < tokens.Length; j++) {

                            options[i].Value += $" {tokens[j]}";
                            options[i].Value = options[i].Value.Trim();
                        }
                        return;

                    } else goto invalid_syntax;
                }
            }
        }

        UCI.Log($"unsupported option {tokens[2]}", UCI.LogLevel.ERROR);
        return;

        invalid_syntax:
        UCI.Log("invalid setoption syntax", UCI.LogLevel.ERROR);
        return;
    }
}