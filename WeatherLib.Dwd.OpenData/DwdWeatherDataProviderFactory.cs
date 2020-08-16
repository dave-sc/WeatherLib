using System;

namespace WeatherLib.Dwd.OpenData
{
    public class DwdWeatherDataProviderFactory : IWeatherDataProviderFactory
    {
        public IWeatherDataProvider GetProvider(params object[] args)
        {
            if (args.Length == 1 && args[0] is WeatherLocation location)
                return new DwdLocationWeatherDataProvider(location);
            
            if (args.Length == 2 && args[0] is string stationId && args[1] is string warningCellId)
                return new DwdStationWeatherDataProvider(stationId, warningCellId);
            
            throw new ArgumentException($"Can not create {nameof(IWeatherDataProvider)} from provided arguments");
        }
    }
}