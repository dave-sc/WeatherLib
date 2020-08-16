namespace WeatherLib.Dwd.OpenData
{
    public class WeatherStation
    {
        public WeatherStation(string id, string name, double longitude, double latitude, double elevation)
            : this(id, name, CoordinateType.DegreesDecimalMinutes, longitude, latitude, elevation)
        { }
        public WeatherStation(string id, string name, CoordinateType coordinateType, double longitude, double latitude, double elevation)
            : this(id, new WeatherLocation(name, coordinateType, longitude, latitude), elevation)
        { }

        public WeatherStation(string id, WeatherLocation location, double elevation)
        {
            Id = id;
            Location = location;
            Elevation = elevation;
        }

        public string Id { get; set; }

        public WeatherLocation Location { get; }

        public double Elevation { get; }

        public override string ToString()
        {
            return $"{Id} - {Location}";
        }
    }
}