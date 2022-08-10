using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherLib.Dwd.OpenData.Util
{
    internal static class SimpleCsvTableReader
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
            var tableRows = new List<IReadOnlyDictionary<string, string>>();

            string firstLine = await textReader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(firstLine))
                return tableRows;

            string[] columnHeaders = firstLine.Split(';');

            while (true)
            {
                var currentLine = await textReader.ReadLineAsync();
                if (currentLine == null)
                    break;

                string[] columns = currentLine.Split(';');
                if (columns.Length != columnHeaders.Length)
                    continue;
                
                var tableRow = columnHeaders.Select((header, i) => (header: header, index: i)).ToDictionary(c => c.header?.Trim().ToLower() ?? c.index.ToString(), c => columns[c.index]);
                tableRows.Add(tableRow);
            }

            return tableRows;
        }
    }
}