using System;
using System.Collections.Generic;

namespace NHQTools.Utilities
{
    public class ArgumentParser
    {
        // Public
        public List<string> PositionalArgs { get; }
        public List<string> Switches { get; }

        // Private
        private readonly Dictionary<string, string> _namedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        ////////////////////////////////////////////////////////////////////////////////////
        // Parses command-line arguments into named options, flags, and positional arguments
        // App.exe -verbose input.pff -output:out.pff
        // Tell parser that "verbose", "debug" etc. are toggles by passing them as boolFlags
        // var parser = new ArgumentParser(args, "verbose", "debug");
        public ArgumentParser(string[] args, params string[] boolFlags)
        {
            PositionalArgs = new List<string>();
            Switches = new List<string>();

            // Store known boolean flags for quick lookup
            var booleanFlags = new HashSet<string>(boolFlags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                // Skip empty arguments
                if (string.IsNullOrEmpty(arg))
                    continue;

                // Check if it's a switch (starts with -, or /), allows (--)
                // If not, it's a positional argument such as a filename
                if (!arg.StartsWith("-") && !arg.StartsWith("/"))
                {
                    PositionalArgs.Add(arg);
                    continue;
                }

                var key = arg.TrimStart('-', '/');

                // Skip invalid inputs like just "-" or "---"
                if (string.IsNullOrEmpty(key))
                    continue;

                var value = "true"; // Default for flags

                // Split key and value if arg contains ':' or '='
                var separatorIndex = key.IndexOfAny(new[] { ':', '=' });

                if (separatorIndex > -1)
                {
                    value = key.Substring(separatorIndex + 1);
                    key = key.Substring(0, separatorIndex);
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("-") && !args[i + 1].StartsWith("/"))
                {
                    var isBool = booleanFlags.Contains(key);

                    if (!isBool)
                    {
                        value = args[i + 1];
                        i++;
                    }

                }

                Switches.Add(arg);
                _namedArgs[key] = value;

            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Maps positional arguments to named keys
        public int MapPositionalArgs(params string[] names)
        {
            var mapped = 0;

            for (var i = 0; i < names.Length; i++)
            {
                // Ensure we have enough actual arguments to match this name
                if (i >= PositionalArgs.Count)
                    break;

                var key = names[i];

                // Only map it if the user didn't already provide a specific flag with this name.
                // This allows specific flags to override positional order if needed.
                if (!_namedArgs.ContainsKey(key))
                {
                    _namedArgs[key] = PositionalArgs[i];
                    mapped++;
                }

            }

            return mapped;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public string Get(string key) => _namedArgs.TryGetValue(key, out var value) ? value : null;

        ////////////////////////////////////////////////////////////////////////////////////
        public string Get(string key, string defaultValue) => _namedArgs.TryGetValue(key, out var value) ? value : defaultValue;

        ////////////////////////////////////////////////////////////////////////////////////
        public string GetRequired(string key)
        {
            return _namedArgs.TryGetValue(key, out var value) 
                ? value 
                : throw new ArgumentException($"Required argument '{key}' was not provided.");
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public bool Has(string key) => _namedArgs.ContainsKey(key); // Check if a /flag exists

    }

}