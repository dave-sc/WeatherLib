using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WeatherLib.Dwd.OpenData.Util
{
    internal static class FixedWidthTableReader
    {
        public static Task<IEnumerable<IReadOnlyDictionary<string, string>>> ReadTableAsync(Stream stream)
        {
            using (var reader = new StreamReader(stream))
                return ReadTableAsync(reader);
        }

        public static Task<IEnumerable<IReadOnlyDictionary<string, string>>> ReadTableAsync(string tableString)
        {
            using (var reader = new StringReader(tableString))
                return ReadTableAsync(reader);
        }

        public static async Task<IEnumerable<IReadOnlyDictionary<string, string>>> ReadTableAsync(TextReader textReader)
        {
            string currentLine = null, lastLine = null;
            Dictionary<string, (int index, int length)> tableStructure = null;
            int tableWidth = -1;

            var tableRows = new List<IReadOnlyDictionary<string, string>>();
            while (true)
            {
                lastLine = currentLine;
                currentLine = await textReader.ReadLineAsync();
                if (currentLine == null)
                    break;

                var tableStructureMatch = Regex.Match(currentLine, @"^((=+|-+)\s?)+$");
                if (tableStructureMatch.Success)
                {
                    var columnCaptures = tableStructureMatch.Groups[2].Captures;
                    tableStructure = columnCaptures.Cast<Capture>().ToDictionary(c => lastLine?.Substring(c.Index, c.Length)?.ToLower()?.Trim() ?? c.Index.ToString(), c => (c.Index, c.Length));
                    tableWidth = currentLine.Length;
                    continue;
                }

                if (tableStructure == null)
                    continue;

                if (currentLine.Length != tableWidth)
                {
                    tableStructure = null;
                    continue;
                }

                var tableRow = tableStructure.ToDictionary(t => t.Key.ToLower(), t => currentLine.Substring(t.Value.index, t.Value.length));
                tableRows.Add(tableRow);
            }

            return tableRows;
        }
    }
}