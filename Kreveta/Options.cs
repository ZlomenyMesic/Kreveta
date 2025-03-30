//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;
using System.Text;

namespace Kreveta;

internal static class Options {
    internal enum OptionType {
        CHECK, SPIN, BUTTON, STRING
    }

    internal struct Option {
        internal string name;
        internal OptionType type;

        internal string min_value;
        internal string max_value;

        internal string def_value;
        internal string value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string TypeToString(OptionType type) {
            return type switch {
                OptionType.CHECK  => "check",
                OptionType.SPIN   => "spin",
                OptionType.BUTTON => "button",
                OptionType.STRING => "string",
                _ => ""
            };
        }
    }

    private static readonly Option[] options = [

        // should the engine use its own opening book?
        new() {
            name = "OwnBook",
            type = OptionType.CHECK,

            def_value = "false",
            value = "false"
        },

        // modify the size of the hash table (transpositions)
        new() {
            name = "Hash",
            type = OptionType.SPIN,

            min_value = "1",
            max_value = "2048",

            def_value = "16",
            value = "16"
        },
    ];

    internal static bool OwnBook {
        get => options[0].value == "true";
    }

    internal static int Hash {
        get => int.Parse(options[1].value);
    }

    internal static void Print() {
        foreach (Option opt in options) {
            StringBuilder sb = new();

            sb.Append($"option name {opt.name}");

            string type = Option.TypeToString(opt.type);

            sb.Append($" type {type}");

            if (opt.type == OptionType.CHECK || opt.type == OptionType.STRING) {
                sb.Append($" default {opt.def_value}");
            }

            else if (opt.type == OptionType.SPIN) {
                sb.Append($" default {opt.def_value} min {opt.min_value} max {opt.max_value}");
            }

            Console.WriteLine(sb.ToString());
        }
    }

    internal static void SetOption(string[] toks) {
        if (toks.Length < 3 || toks[1] != "name") {
            goto invalid_syntax;
        }

        for (int i = 0; i < options.Length; i++) {
            if (options[i].name == toks[2]) {

                if (options[i].type == OptionType.BUTTON) {
                    //
                    //
                    //
                    return;
                }

                if (options[i].type == OptionType.CHECK) {
                    if (toks.Length == 5 && toks[3] == "value"
                        && (toks[4] == "true" || toks[4] == "false")) {

                        options[i].value = toks[4];

                        return;

                    } else goto invalid_syntax;
                }

                if (options[i].type == OptionType.SPIN) {
                    if (toks.Length == 5 && toks[3] == "value") {

                        if (!long.TryParse(toks[4], out _))
                            goto invalid_syntax;

                        options[i].value = toks[4];
                        return;

                    } else goto invalid_syntax;
                }

                if (options[i].type == OptionType.STRING) {
                    if (toks.Length >= 5 && toks[3] == "value") {

                        options[i].value = "";
                        for (int j = 3; j < toks.Length; j++) {

                            options[i].value += $" {toks[j]}";
                            options[i].value = options[i].value.Trim();
                        }
                        return;

                    } else goto invalid_syntax;
                }
            }
        }

        Console.WriteLine($"unsupported option {toks[2]}");
        return;

        invalid_syntax:
        Console.WriteLine("invalid setoption syntax");
        return;
    }
}