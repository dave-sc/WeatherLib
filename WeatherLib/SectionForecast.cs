
namespace WeatherLib
{
    public class SectionForecast
    {
        public SectionForecast(ForecastSection section, double temperature, WeatherType weather)
        {
            Section = section;
            Temperature = temperature;
            Weather = weather;
        }

        public ForecastSection Section { get; }

        public double Temperature { get; }

        public WeatherType Weather { get; }

        public override string ToString()
        {
            return $"{Section}: {Temperature:0.#} °C {Weather}";
        }
    }
}