namespace WeatherLib
{
    public interface IWeatherDataProviderFactory
    {
        IWeatherDataProvider GetProvider(params object[] args);
    }
}