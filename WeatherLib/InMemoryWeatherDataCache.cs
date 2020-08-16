using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WeatherLib
{
    public class InMemoryWeatherDataCache : IWeatherDataCache
    {
        private readonly Dictionary<string, IReadOnlyList<ForecastDataPoint>> _rememberedForecastData = new Dictionary<string, IReadOnlyList<ForecastDataPoint>>();
        private readonly Dictionary<string, IReadOnlyList<WeatherWarning>> _rememberedWarnings = new Dictionary<string, IReadOnlyList<WeatherWarning>>();

        public Task<IReadOnlyList<ForecastDataPoint>> GetRememberedForecastData(string identifier)
        {
            if (_rememberedForecastData.TryGetValue(identifier, out var forecastData))
                return Task.FromResult(forecastData);

            return Task.FromResult((IReadOnlyList<ForecastDataPoint>)Array.Empty<ForecastDataPoint>());
        }

        public Task RememberForecastData(string identifier, IReadOnlyList<ForecastDataPoint> forecastData)
        {
            _rememberedForecastData[identifier] = forecastData;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WeatherWarning>> GetRememberedWarnings(string identifier)
        {
            if (_rememberedWarnings.TryGetValue(identifier, out var warnings))
                return Task.FromResult(warnings);

            return Task.FromResult((IReadOnlyList<WeatherWarning>)Array.Empty<WeatherWarning>());
        }

        public Task RememberWarnings(string identifier, IReadOnlyList<WeatherWarning> warnings)
        {
            _rememberedWarnings[identifier] = warnings;
            return Task.CompletedTask;
        }
    }
}