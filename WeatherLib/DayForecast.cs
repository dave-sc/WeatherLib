using System;
using System.Collections.Generic;
using System.Linq;

namespace WeatherLib
{
    public class DayForecast
    {
        public DayForecast(DateTime day, double minimumTemperature, double maximumTemperature, WeatherType overallWeather, SectionForecast morningForecast, SectionForecast middayForecast, SectionForecast eveningForecast, IReadOnlyList<ForecastDataPoint> detailedForecast, IReadOnlyList<WeatherWarning> weatherWarnings = null)
        {
            Day = day;
            MinimumTemperature = minimumTemperature;
            MaximumTemperature = maximumTemperature;
            OverallWeather = overallWeather;
            MorningForecast = morningForecast;
            MiddayForecast = middayForecast;
            EveningForecast = eveningForecast;
            DetailedForecast = detailedForecast ?? Array.Empty<ForecastDataPoint>();
            WeatherWarnings = weatherWarnings ?? Array.Empty<WeatherWarning>();
        }

        public DateTime Day { get; }

        public double MinimumTemperature { get; }

        public double MaximumTemperature { get; }

        public WeatherType OverallWeather { get; }

        public SectionForecast MorningForecast { get; }

        public SectionForecast MiddayForecast { get; }

        public SectionForecast EveningForecast { get; }

        public IReadOnlyList<ForecastDataPoint> DetailedForecast { get; }

        public IReadOnlyList<WeatherWarning> WeatherWarnings { get; }

        public override string ToString()
        {
            return
                $"{Day:ddd yyyy-MM-dd}: {MinimumTemperature:0.#} - {MaximumTemperature:0.#} °C {OverallWeather}; {MorningForecast} {MiddayForecast} {EveningForecast}; Warnings: {(WeatherWarnings == null ? "none" : string.Join(", ", WeatherWarnings.Select(w => w.ToString())))}";
        }
    }
}