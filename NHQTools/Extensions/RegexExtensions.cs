using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NHQTools.Extensions
{
    public static class RegexExtensions
    {

        ////////////////////////////////////////////////////////////////////////////////
        #region Regex Matches
        public struct MatchLine
        {
            public readonly Match Match;
            public readonly int LineNo;

            public MatchLine(Match match, int lineNo)
            {
                Match = match;
                LineNo = lineNo;
            }

        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static IEnumerable<MatchLine> IncludeLineNo(this MatchCollection matches, string textData)
        {
            // Iterates through matches while tracking the line number.
            // O(N) total performance vs O(N^2) with text.Take(index).Count(c => c == '\n') + 1;

            var currentLine = 1;
            var lastIndex = 0;

            foreach (Match m in matches)
            {
                // Scan the gap between the last match and current one
                for (var i = lastIndex; i < m.Index; i++)
                {
                    if (textData[i] == '\n')
                        currentLine++;
                }

                // Update cursor index
                lastIndex = m.Index;

                yield return new MatchLine(m, currentLine);

            }

        }

        #endregion

    }

}