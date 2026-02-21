using System;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Web.Script.Serialization;

namespace NHQTools.Utilities
{
    public static class Json
    {

        [Flags]
        public enum If // Serialization Exclusions
        {
            Never = 0,
            Always = 1,
            Null = 2,
            Empty = 4,  // Empty string or collection
            Zero = 8   // Numeric zero
        }

        // Marks a property for exclusion during JSON serialization.
        [AttributeUsage(AttributeTargets.Property)]
        public class ExcludeAttribute : Attribute
        {
            public If Condition { get; }

            public ExcludeAttribute(If con = If.Always) => Condition = con;
        }

        // Default Pretty-Print Token Replacements
        private static readonly string[][] DefaultReplacements =
        {
            // Some text has \\t within. Stop trying.
            //new[] { @"\\",   "[BSLASH]" },
            //new[] { @"\r\n", "[CRLF]" },
            //new[] { @"\n",   "[LF]" },
            //new[] { @"\t",   "[TAB]" },
            new[] { @"\u0027", "'" },
            new[] { @"\u0026", "&" },
            new[] { @"\u003c", "<" },
            new[] { @"\u003e", ">" },
            new[] { @"\u002f", "/" },
        };

        // Default property names that should appear at the top of serialized JSON objects
        private static readonly List<string> DefaultPropertyPriority = new List<string>
        {
            "Id",
            "Encoding",
            "EncryptionKey",
            "Section",
            "GroupName",
            "Group",
            "Name",
            "Entries",
            "Key",
            "Type",
            "Value",
        };

        // Cache for per-type reflection metadata used by Sanitize
        private static readonly Dictionary<Type, PropertyMeta[]> TypeMetaCache = new Dictionary<Type, PropertyMeta[]>();

        private struct PropertyMeta
        {
            public PropertyInfo Prop;
            public ExcludeAttribute Exclude;
            public bool IsIgnored;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        #region Stringify / Parse
        // Serializes an object to JSON, excluding properties marked with [ScriptIgnore] or [Exclude(Condition)]
        public static string Stringify(this object dataObj, bool prettyPrint = false, int maxLength = 2097152,
            string[][] replacements = null, List<string> priorityOrder = null)
        {
            var sanitizedObj = Sanitize(dataObj, priorityOrder ?? DefaultPropertyPriority);

            var serialized = new JavaScriptSerializer { MaxJsonLength = maxLength }
                .Serialize(sanitizedObj);

            return prettyPrint ? ToPrettyPrint(serialized, replacements: replacements) : serialized;
        }

        // Deserializes a JSON string to an object of type<T>
        public static T Parse<T>(this string json, bool fromPrettyPrint = false, string[][] replacements = null)
        {
            if (fromPrettyPrint)
                json = FromPrettyPrint(json, replacements);

            return string.IsNullOrEmpty(json)
                ? default
                : new JavaScriptSerializer().Deserialize<T>(json);
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Pretty-Print
        // Converts minified JSON into a human-readable indented format
        public static string ToPrettyPrint(string rawJson, int spaces = 2, string[][] replacements = null)
        {
            if (string.IsNullOrEmpty(rawJson))
                return string.Empty;

            // Replace escaped sequences with readable tokens
            var json = ApplyReplacements(rawJson, escaped: true, replacements ?? DefaultReplacements);

            var indent = 0;
            var quoted = false;

            // Pre-build indentation cache to avoid repeated string concatenation
            const int maxCachedIndent = 64;
            var indentCache = new string[maxCachedIndent];
            indentCache[0] = string.Empty;
            for (var c = 1; c < maxCachedIndent; c++)
                indentCache[c] = new string(' ', spaces * c);

            // Estimate capacity: pretty-printed JSON is roughly 2x the minified size
            var sb = new StringBuilder(json.Length * 2);

            for (var i = 0; i < json.Length; i++)
            {
                var ch = json[i];

                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            indent++;
                            sb.Append(indent < maxCachedIndent ? indentCache[indent] : indentCache[maxCachedIndent - 1]);
                        }
                        break;

                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            sb.AppendLine();
                            indent--;
                            sb.Append(indent < maxCachedIndent ? indentCache[indent] : indentCache[maxCachedIndent - 1]);
                        }
                        sb.Append(ch);
                        break;

                    case '"':
                        sb.Append(ch);
                        if (!IsEscaped(json, i))
                            quoted = !quoted;
                        break;

                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            sb.Append(indent < maxCachedIndent ? indentCache[indent] : indentCache[maxCachedIndent - 1]);
                        }
                        break;

                    case ':':
                        sb.Append(ch);
                        if (!quoted)
                            sb.Append(' ');
                        break;

                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Reverts a pretty-print JSON string back to its serializer escaped format
        public static string FromPrettyPrint(string json, string[][] replacements = null)
        {
            return string.IsNullOrEmpty(json) ?
                json :
                ApplyReplacements(json, escaped: false, replacements ?? DefaultReplacements);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Applies pretty-print token replacements in the specified direction.
        private static string ApplyReplacements(string text, bool escaped, string[][] replacements)
        {
            foreach (var pair in replacements)
            {
                // escaped=true:  replace escaped (e.g. \r\n > [CR])
                // escaped=false: replace readable (e.g. [CR] > \r\n)
                text = escaped
                    ? text.Replace(pair[0], pair[1])
                    : text.Replace(pair[1], pair[0]);
            }
            return text;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Determines if the character at N is preceded by an odd number of backslashes
        private static bool IsEscaped(string json, int index)
        {
            var escaped = false;

            while (index > 0 && json[--index] == '\\')
                escaped = !escaped;

            return escaped;
        }

        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Sanitize / Exclusions

        // Sanitization (removes excluded properties recursively)
        private static object Sanitize(object objData, List<string> priorityOrder)
        {
            if (objData == null)
                return null;

            var type = objData.GetType();

            // Primitives pass through directly
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return objData;

            // Enums serialize as their underlying value, not the name
            if (type.IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(type);
                return Convert.ChangeType(objData, underlyingType);
            }

            // Recurse into collections
            if (objData is IEnumerable list && !(objData is string))
            {
                var items = new List<object>();

                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var item in list)
                    items.Add(Sanitize(item, priorityOrder));

                return items;
            }

            // Convert objects to ordered dictionaries, filtering excluded properties
            var result = new SortedDictionary<string, object>(new PriorityKeyComparer(priorityOrder));

            // Use cached reflection metadata
            if (!TypeMetaCache.TryGetValue(type, out var metas))
            {
                var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                metas = new PropertyMeta[props.Length];
                for (var i = 0; i < props.Length; i++)
                {
                    metas[i] = new PropertyMeta
                    {
                        Prop = props[i],
                        IsIgnored = props[i].IsDefined(typeof(ScriptIgnoreAttribute), false),
                        Exclude = (ExcludeAttribute)Attribute.GetCustomAttribute(props[i], typeof(ExcludeAttribute))
                    };
                }
                TypeMetaCache[type] = metas;
            }

            foreach (var meta in metas)
            {
                if (meta.IsIgnored)
                    continue;

                var value = meta.Prop.GetValue(objData, null);

                if (meta.Exclude != null && ShouldExclude(meta.Exclude.Condition, value))
                    continue;

                result[meta.Prop.Name] = Sanitize(value, priorityOrder);
            }

            return result;
        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Evaluates whether a value should be excluded based on the given condition flags
        private static bool ShouldExclude(If condition, object value)
        {
            if (condition.HasFlag(If.Always))
                return true;

            if (condition.HasFlag(If.Null) && value == null)
                return true;

            if (condition.HasFlag(If.Zero) && IsZero(value))
                return true;

            if (!condition.HasFlag(If.Empty))
                return false;

            switch (value)
            {
                case string s when s.Length == 0:
                case ICollection col when col.Count == 0:
                    return true;
                default:
                    return false;
            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        // Returns true if the value is a numeric type equal to zero
        private static bool IsZero(object value)
        {
            switch (value)
            {
                case null: return false;
                case int i: return i == 0;
                case short s: return s == 0;
                case long l: return l == 0;
                case double d: return d == 0;
                case decimal m: return m == 0;
                default: return false;
            }
        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        #region Property Priority Comparer

        // Comparer that forces property names to the top of serialized JSON objects.
        private class PriorityKeyComparer : IComparer<string>
        {
            private readonly Dictionary<string, int> _priorityIndex;

            public PriorityKeyComparer(List<string> priorityOrder)
            {
                _priorityIndex = new Dictionary<string, int>(priorityOrder.Count);
                for (var i = 0; i < priorityOrder.Count; i++)
                    _priorityIndex[priorityOrder[i]] = i;
            }

            [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
            public int Compare(string x, string y)
            {
                if (x == y) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                var hasX = _priorityIndex.TryGetValue(x, out var ix);
                var hasY = _priorityIndex.TryGetValue(y, out var iy);

                if (hasX && hasY) return ix.CompareTo(iy);
                if (hasX) return -1;
                if (hasY) return 1;

                return string.Compare(x, y, StringComparison.Ordinal);
            }

        }
        #endregion

        ////////////////////////////////////////////////////////////////////////////////////
        // Extacts the message and line number from an exception message
        public static void ExceptionHandler(Exception ex, Action<string, int> onErrorAction)
        {
            // Matches: "Error message (131):"
            var match = Regex.Match(ex.Message, @"^(.*)\s\((\d+)\):", RegexOptions.Singleline);

            if (match.Success)
            {
                onErrorAction(match.Groups[1].Value.Trim(), int.Parse(match.Groups[2].Value));
                return;
            }

            onErrorAction(ex.Message, -1);
        }

    }

}