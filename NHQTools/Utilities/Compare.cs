using System;
using System.IO;
using System.Text;

namespace NHQTools.Utilities
{
    public static class Compare
    {
        private static readonly string[] LineSeparators = { "\r\n", "\r", "\n" };

        ////////////////////////////////////////////////////////////////////////////////////
        public static string BinaryDiff(byte[] expected, byte[] actual, int maxDiffs = 10)
        {

            if (expected == null) 
                throw new ArgumentNullException(nameof(expected), "Expected byte array cannot be null.");

            if (actual == null) 
                throw new ArgumentNullException(nameof(actual), "Actual byte array cannot be null.");

            var sb = new StringBuilder();

            if (expected.Length != actual.Length)
                sb.AppendLine($"[Size Mismatch] Expected: {expected.Length} bytes, Actual: {actual.Length} bytes, Diff: {actual.Length - expected.Length}");

            var limit = Math.Min(expected.Length, actual.Length);
            var count = 0;

            for (var i = 0; i < limit; i++)
            {
                if (expected[i] == actual[i])
                    continue;

                // Expected Values
                var expInt = 0;
                var expFlt = 0f;
                long expLng = 0;

                if (i + 4 <= expected.Length)
                {
                    expInt = BitConverter.ToInt32(expected, i);
                    expFlt = BitConverter.ToSingle(expected, i);
                }

                if (i + 8 <= expected.Length)
                    expLng = BitConverter.ToInt64(expected, i);

                // Actual Values
                var actInt = 0;
                var actFlt = 0f;
                long actLng = 0;

                if (i + 4 <= actual.Length)
                {
                    actInt = BitConverter.ToInt32(actual, i);
                    actFlt = BitConverter.ToSingle(actual, i);
                }

                if (i + 8 <= actual.Length)
                    actLng = BitConverter.ToInt64(actual, i);

                var expChar = expected[i] >= 32 && expected[i] <= 126 
                    ? (char)expected[i] 
                    : '.';

                var actChar = actual[i] >= 32 && actual[i] <= 126 
                    ? (char)actual[i] 
                    : '.';

                sb.AppendLine(string.Format("Offset 0x{0:X8} (pos:{0}):", i));

                sb.Append($"   Exp: Byte:0x{expected[i]:X2}");
                sb.Append($"  | Char: '{expChar}' ");
                sb.AppendLine($"| Int:{expInt,12} | Float:{expFlt,14:G7} | Long:{expLng,20}");

                sb.Append($"   Got: Byte:0x{actual[i]:X2}");
                sb.Append($"  | Char: '{actChar}' ");
                sb.AppendLine($"| Int:{actInt,12} | Float:{actFlt,14:G7} | Long:{actLng,20}");

                sb.AppendLine(); // Spacer

                count++;

                if (count < maxDiffs)
                    continue;

                sb.AppendLine();
                sb.AppendLine($"... stopping after {maxDiffs} differences.");

                break;
            }

            if (count == 0 && expected.Length == actual.Length)
                return "Binary data is identical.";

            return sb.ToString();
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static string TextDiff(string expected, string actual, int maxDiffs = 5)
        {
            if (expected == null) 
                throw new ArgumentNullException(nameof(expected), "Expected string cannot be null.");

            if (actual == null) 
                throw new ArgumentNullException(nameof(actual), "Actual string cannot be null.");

            var linesExp = expected.Split(LineSeparators, StringSplitOptions.None);
            var linesAct = actual.Split(LineSeparators, StringSplitOptions.None);

            return CompareLines(linesExp, linesAct, "Exp", "Got", maxDiffs);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static string FromText(string text1, string text2, int maxDiffs = 5)
        {
            if (text1 == null) 
                throw new ArgumentNullException(nameof(text1), "First text string cannot be null.");

            if (text2 == null) 
                throw new ArgumentNullException(nameof(text2), "Second text string cannot be null.");

            var text1Lines = text1.Split(LineSeparators, StringSplitOptions.None);
            var text2Lines = text2.Split(LineSeparators, StringSplitOptions.None);

            return CompareLines(text1Lines, text2Lines, "File 1", "File 2", maxDiffs);
        }

        ////////////////////////////////////////////////////////////////////////////////////
        public static string FromFile(string file1, string file2, int maxDiffs = 5)
        {
            // Compares the contents of two text files line by line and returns a summary of the differences

            if (file1 == null) 
                throw new ArgumentNullException(nameof(file1), "First file path cannot be null.");

            if (file2 == null) 
                throw new ArgumentNullException(nameof(file2), "Second file path cannot be null.");

            var sb = new StringBuilder();

            if (!File.Exists(file1))
            {
                sb.AppendLine($"Not found: {file1}");
                return sb.ToString();
            }

            if (!File.Exists(file2))
            {
                sb.AppendLine($"Not found: {file2}");
                return sb.ToString();
            }

            using (var r1 = new StreamReader(file1))
            using (var r2 = new StreamReader(file2))
            {
                var lineNo = 1;
                var count = 0;

                while (true)
                {
                    // Read both lines independently to avoid losing a line
                    // when one file ends before the other
                    var line1 = r1.ReadLine();
                    var line2 = r2.ReadLine();

                    // Both files ended at the same point
                    if (line1 == null && line2 == null)
                        break;

                    if (count >= maxDiffs)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"... stopping after {maxDiffs} differences.");
                        break;
                    }

                    if (line1 == null)
                    {
                        sb.AppendLine($"File 1 has fewer lines than File 2 (starting from line {lineNo}).");
                        count++;
                        break;
                    }

                    if (line2 == null)
                    {
                        sb.AppendLine($"File 2 has fewer lines than File 1 (starting from line {lineNo}).");
                        count++;
                        break;
                    }

                    if (line1 != line2)
                    {
                        sb.AppendLine($"Difference at Line {lineNo}:");
                        sb.AppendLine($"  File 1: {line1}");
                        sb.AppendLine($"  File 2: {line2}");
                        count++;
                    }

                    lineNo++;
                }

                if (count == 0)
                    sb.AppendLine("Files are identical.");

            }

            return sb.ToString();
        }

        ////////////////////////////////////////////////////////////////////////////////////
        private static string CompareLines(string[] lines1, string[] lines2, string label1, string label2, int maxDiffs)
        {
            var sb = new StringBuilder();

            if (lines1.Length != lines2.Length)
                sb.AppendLine($"[Line Count Mismatch] {label1}: {lines1.Length} lines, {label2}: {lines2.Length} lines");

            var maxLength = Math.Max(lines1.Length, lines2.Length);
            var count = 0;

            for (var i = 0; i < maxLength; i++)
            {
                if (count >= maxDiffs)
                {
                    sb.AppendLine();
                    sb.AppendLine($"... stopping after {maxDiffs} differences.");
                    break;
                }

                if (i >= lines1.Length)
                {
                    sb.AppendLine($"{label1} has fewer lines than {label2} (missing line {i + 1}).");
                    count++;
                    break;
                }

                if (i >= lines2.Length)
                {
                    sb.AppendLine($"{label2} has fewer lines than {label1} (missing line {i + 1}).");
                    count++;
                    break;
                }

                if (lines1[i] == lines2[i])
                    continue;

                sb.AppendLine($"Line {i + 1}:");
                sb.AppendLine($"   {label1}: \"{lines1[i]}\"");
                sb.AppendLine($"   {label2}: \"{lines2[i]}\"");

                count++;
            }

            if (count == 0 && lines1.Length == lines2.Length)
                return "Text content is identical.";

            return sb.ToString();
        }

    }

}