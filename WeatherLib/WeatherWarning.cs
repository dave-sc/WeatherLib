using System;

namespace WeatherLib
{
    public class WeatherWarning
    {
        public WeatherWarning(DateTime startTime, DateTime endTime, WeatherWarningType type, WarningSeverity severity, WarningCertainty certainty, string shortText, string headline, string fullText)
        {
            StartTime = startTime;
            EndTime = endTime;
            Type = type;
            Severity = severity;
            Certainty = certainty;
            ShortText = shortText ?? string.Empty;
            Headline = headline ?? string.Empty;
            FullText = fullText ?? string.Empty;
        }

        public DateTime StartTime { get; }

        public DateTime EndTime { get; }

        public WeatherWarningType Type { get; }
        public WarningSeverity Severity { get; }
        public WarningCertainty Certainty { get; }

        public string ShortText { get; }

        public string Headline { get; }

        public string FullText { get; }

        public override string ToString()
        {
            return $"{StartTime:s}-{EndTime:s}: {ShortText} ({Severity} {Type} {Certainty})";
        }
    }
}