using System.Collections.Generic;
using System.Threading.Tasks;

namespace WeatherLib
{
    /// <summary>
    /// Provides weather forecast and warning data to a <see cref="WeatherService"/>
    /// </summary>
    public interface IWeatherDataProvider
    {
        /// <summary>
        /// Returns an identifier, that can be used to refer data to this weather data provider.
        /// </summary>
        string GetIdentifier();

        /// <summary>
        /// Gets the weather forecast for this station or location as each individual data point the weather data provider can provide
        /// </summary>
        Task<IReadOnlyList<ForecastDataPoint>> GetForecastAsync();

        /// <summary>
        /// Gets the weather warnings for this station or location
        /// </summary>
        Task<IReadOnlyList<WeatherWarning>> GetWarningsAsync();
    }
}