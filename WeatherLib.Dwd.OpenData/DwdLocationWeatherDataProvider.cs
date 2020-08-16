using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace WeatherLib.Dwd.OpenData
{
    /// <summary>
    /// Provides weather forecast and warning data to a <see cref="WeatherService"/> for a given
    /// location
    /// </summary>
    public class DwdLocationWeatherDataProvider : IWeatherDataProvider
    {
        private readonly DwdOpenDataClient _dwdOpenDataClient;
        private WeatherStation _weatherStation;

        public DwdLocationWeatherDataProvider(WeatherLocation location)
        {
            Location = location ?? throw new ArgumentNullException(nameof(location));
            _dwdOpenDataClient = new DwdOpenDataClient();
        }

        public WeatherLocation Location { get; }

        public string GetIdentifier()
        {
            return $"{Location.Longitude.ToString(CultureInfo.InvariantCulture)}-{Location.Latitude.ToString(CultureInfo.InvariantCulture)}";
        }

        public async Task<IReadOnlyList<ForecastDataPoint>> GetForecastAsync()
        {
            if (_weatherStation == null)
                _weatherStation = (await _dwdOpenDataClient.GetClosestStationsToAsync(Location)).FirstOrDefault();
            if (_weatherStation == null)
                return Array.Empty<ForecastDataPoint>();

            return await _dwdOpenDataClient.GetForecastAsync(_weatherStation);
        }

        public Task<IReadOnlyList<WeatherWarning>> GetWarningsAsync()
        {
            return _dwdOpenDataClient.GetWarningsAsync(Location);
        }
    }
}