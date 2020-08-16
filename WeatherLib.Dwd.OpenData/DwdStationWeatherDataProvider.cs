using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WeatherLib.Dwd.OpenData
{
    /// <summary>
    /// Provides weather forecast and warning data to a <see cref="WeatherService"/> for a single
    /// DWD weather station and warning cell
    /// </summary>
    public class DwdStationWeatherDataProvider : IWeatherDataProvider
    {
        private readonly DwdOpenDataClient _dwdOpenDataClient;

        public DwdStationWeatherDataProvider(string stationId, string warningCellId)
            : this(new WeatherStation(stationId, "", double.NaN, double.NaN, double.NaN),
                new WarningCell(warningCellId, "", ""))
        {

        }

        public DwdStationWeatherDataProvider(WeatherStation weatherStation, WarningCell warningCell)
        {
            WeatherStation = weatherStation ?? throw new ArgumentNullException(nameof(weatherStation));
            WarningCell = warningCell ?? throw new ArgumentNullException(nameof(warningCell));
            _dwdOpenDataClient = new DwdOpenDataClient();
        }

        public WeatherStation WeatherStation { get; }
        public WarningCell WarningCell { get; }

        public string GetIdentifier()
        {
            return $"{WeatherStation.Id}-{WarningCell.Id}";
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ForecastDataPoint>> GetForecastAsync()
        {
            return _dwdOpenDataClient.GetForecastAsync(WeatherStation);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<WeatherWarning>> GetWarningsAsync()
        {
            return _dwdOpenDataClient.GetWarningsAsync(WarningCell);
        }
    }
}
