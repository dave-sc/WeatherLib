using System.Collections.Generic;
using System.Threading.Tasks;

namespace WeatherLib
{
    public interface IWeatherDataCache
    {
        Task<IReadOnlyList<ForecastDataPoint>> GetRememberedForecastData(string identifier);
        Task RememberForecastData(string identifier, IReadOnlyList<ForecastDataPoint> forecastData);

        Task<IReadOnlyList<WeatherWarning>> GetRememberedWarnings(string identifier);
        Task RememberWarnings(string identifier, IReadOnlyList<WeatherWarning> warnings);
    }
}