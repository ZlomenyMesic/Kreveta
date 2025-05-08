//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Specify IFormatProvider
#pragma warning disable CA1305

// specify CultureInfo
#pragma warning disable CA1304

// use ToUpperInvariant
#pragma warning disable CA1308

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable InconsistentNaming

namespace Kreveta;

// this class manages the internal options that can
// be modified via the "setoption" command. see UCI
// documentation for more details
internal static class Options {
    
    // the UCI Protocol defines a couple option types (we only
    // implement the ones we want). CHECK is essentially a boolean
    // value. SPIN is an integer value. BUTTON has no value but
    // should trigger an action, and STRING has a text value
    private enum OpType : byte {
        CHECK, SPIN, BUTTON, STRING
    }

    // since all options are stored in an array, we must
    // define an interface from which the generic option
    // is inherited
    private interface IOption {
        
        // option name and type are independent
        // of the rest of the generic option, so
        // they can be defined here
        internal string Name { get; }
        internal OpType Type { get; }
        
        // a separate method must be used to set
        // the value, because the value type is
        // generic, which this interface isn't
        internal void SetValue(object value);
    }

    [StructLayout(LayoutKind.Auto)]
    private record struct Option<T> : IOption {
        public string Name { get; init; }
        public OpType Type { get; init; }

        // min and max values are only used with the
        // "spin" option type
        internal long MinValue;
        internal long MaxValue;
        
        // default value is displayed when the "uci"
        // command is received. currect value of the
        // option is stored in Value
        internal required T DefaultValue;
        internal required T Value;

        // no exceptions should ever be thrown
        void IOption.SetValue(object value) {
            if (value is not T casted)
                throw new InvalidCastException();
            
            Value = casted;
        }
    }

    private static readonly IOption[] options = [

        // should the engine use its own opening book?
        // this usually gets turned off by the GUI, but
        // it's great to have a custom book for debugging
        // when playing variety is required
        new Option<bool> {
            Name         = nameof(OwnBook),
            Type         = OpType.CHECK,
            
            DefaultValue = true,
            Value        = true
        },

        // size of the hash table in megabytes. this only
        // sets the size of the transposition table. other
        // tables, such as pawn corrections, or the perfttt,
        // are not modified using this option
        new Option<long> {
            Name         = nameof(Hash),
            Type         = OpType.SPIN,

            // a transposition table with no size would
            // probably break the engine, so there's always
            // going to be at least a small one
            MinValue     = 1,
            MaxValue     = 2048,

            DefaultValue = 40,
            Value        = 40
        },

        // logging into a file using the NKLogger by KryKom.
        // the engine may log its commands and responses into
        // a custom log file
        new Option<bool> {
            Name         = nameof(NKLogs),
            Type         = OpType.CHECK,

            DefaultValue = false,
            Value        = false
        },
        
        // print fancy statistics after each finished search
        new Option<bool> {
            Name         = nameof(PrintStats),
            Type         = OpType.CHECK,
            
            DefaultValue = true,
            Value        = true
        }
    ];

    // changing the names of these properties also changes
    // the name of the actual option, which isn't great.
    // non-custom options (OwnBook and Hash) need to keep
    // their names in order to be used properly by the GUI
    [ReadOnly(true)] internal static bool OwnBook
        => ((Option<bool>)options[0]).Value;

    [ReadOnly(true)] internal static long Hash
        => ((Option<long>)options[1]).Value;

    [ReadOnly(true)] internal static bool NKLogs
        => ((Option<bool>)options[2]).Value;
    
    [ReadOnly(true)] internal static bool PrintStats
        => ((Option<bool>)options[3]).Value;
    
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

    // after receiving the "uci" command, the engine must
    // also list all of its modifiable options, so the GUI
    // knows the ones it can use
    internal static void Print() {
        
        // when displaying options, we must provide the name,
        // option type, default value and possibly the range
        // of values the option can hold
        
        foreach (IOption opt in options) {
            StringBuilder sb = new();

            // append the option name and type
            sb.Append($"option name {opt.Name}");
            sb.Append($" type {opt.Type.GetName()}");

            // we must always cast the ioption to the correct generic
            // option type. this would usually throw exceptions, but
            // we are absolutely certain about the types
            switch (opt.Type) {
                
                case OpType.CHECK:
                    
                    // boolean converts to "True" or "False", so we
                    // must also convert it to the lowercase variant
                    sb.Append($" default {((Option<bool>)opt).DefaultValue.ToString().ToLowerInvariant()}");
                    break;
                
                case OpType.STRING:
                    sb.Append($" default {((Option<string>)opt).DefaultValue}");
                    break;
                
                case OpType.SPIN:
                    var optCast = (Option<long>)opt;
                    
                    // spin option type must provide the range of values it can hold
                    sb.Append($" default {optCast.DefaultValue} min {optCast.MinValue} max {optCast.MaxValue}");
                    break;
            }

            UCI.Log(sb.ToString());
        }
    }

    // this is called when the engine receives the "setoption"
    // command, which is used to modify the value of an option
    internal static void SetOption(ReadOnlySpan<string> tokens) {
        
        // the syntax must be "setoption name <NAME> value <VALUE>"
        if (tokens.Length < 3 || tokens[1] != "name") {
            goto invalid_syntax;
        }

        // there's probably a better way to do this, but i am lazy,
        // so we simply loop over the existing options and try to
        // match the names
        foreach (IOption opt in options) {
            if (opt.Name != tokens[2]) 
                continue;
            
            switch (opt.Type) {
                case OpType.BUTTON: {
                    return;
                }

                case OpType.CHECK: {
                    if (tokens is [_, _, _, "value", "true" or "false"]) {
                        
                        // boolean values are either "True" or "False", but we store
                        // "true" and "false", so we simply do it this way
                        //opt.SetValue(tokens[4] == "true");
                        return;
                    
                    } goto invalid_syntax;
                }

                case OpType.SPIN: {
                    if (tokens is [_, _, _, "value", _]) {

                        // the value probably wasn't an integer
                        if (!long.TryParse(tokens[4], out long val))
                            goto invalid_syntax;
                        
                        // cast the ioption to option<long>
                        var optCast = (Option<long>)opt;
                        
                        // get the minimum and maximum values
                        long minVal = optCast.MinValue;
                        long maxVal = optCast.MaxValue;

                        // check whether the new value falls into the range
                        if (val < minVal || val > maxVal) 
                            goto val_out_of_range;

                        opt.SetValue(val);
                        return;

                    } goto invalid_syntax;
                }

                case OpType.STRING: {
                    if (tokens is [_, _, _, "value", ..]) {
                        StringBuilder sb = new();
                        
                        // the value of a string option type can be
                        // any length, and can be divided by spaces
                        for (byte j = 3; j < tokens.Length; j++)
                            sb.Append(tokens[j]);

                        opt.SetValue(sb.ToString().Trim());
                        return;

                    } goto invalid_syntax;
                }
            }
        }

        // didn't match the name with any option
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
#pragma warning restore CA1304
#pragma warning restore CA1308

#pragma warning restore IDE0079