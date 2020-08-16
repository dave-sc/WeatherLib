using System;

namespace WeatherLib
{
    public class WeatherLocation
    {
        public WeatherLocation(string name, double longitude, double latitude)
            : this(name, CoordinateType.DecimalDegrees, longitude, latitude)
        { }

        public WeatherLocation(string name, CoordinateType coordinateType, double longitude, double latitude)
        {
            Name = name;
            CoordinateType = coordinateType;
            Longitude = longitude;
            Latitude = latitude;
        }

        /// <summary>
        /// Contains the name of the location
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Contains the type of coordinate system in which this location is described.
        /// </summary>
        public CoordinateType CoordinateType { get; }

        /// <summary>
        /// Contains the geographic longitude of the place
        /// </summary>
        public double Longitude { get; }

        /// <summary>
        /// Contains the geographic latitude of the place
        /// </summary>
        public double Latitude { get; }

        /// <summary>
        /// Calculates the distance in kilometers between two <see cref="WeatherLocation"/>s using the haversine formula
        /// </summary>
        /// <param name="location">The location to calculate the distance to</param>
        public double GetDistanceTo(WeatherLocation location)
        {
            location = location.AsType(this.CoordinateType);
            double ToRadians(double degrees)
            {
                return degrees * Math.PI / 180;
            }
            
            var dLat = ToRadians(location.Latitude - this.Latitude);
            var dLon = ToRadians(location.Longitude - this.Longitude);

            var lat1 = ToRadians(this.Latitude);
            var lat2 = ToRadians(location.Latitude);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            const double earthRadius = 6371;
            return earthRadius * c;
        }

        /// <summary>
        /// Returns an instance of a <see cref="WeatherLocation"/> for the same place in the given coordinate system
        /// </summary>
        /// <param name="coordinateType">The coordinate system to return this <see cref="WeatherLocation"/> in</param>
        public WeatherLocation AsType(CoordinateType coordinateType)
        {
            if (CoordinateType == coordinateType)
                return this;

            double ddLongitude, ddLatitude;
            switch (CoordinateType)
            {
                case CoordinateType.DecimalDegrees:
                    ddLongitude = Longitude;
                    ddLatitude = Latitude;
                    break;
                case CoordinateType.DegreesDecimalMinutes:
                    ddLongitude = ConvertDegreeDecimalMinutesToDecimalDegrees(Longitude);
                    ddLatitude = ConvertDegreeDecimalMinutesToDecimalDegrees(Latitude);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            double targetLongitude, targetLatitude;
            switch (coordinateType)
            {
                case CoordinateType.DecimalDegrees:
                    targetLongitude = ddLongitude;
                    targetLatitude = ddLatitude;
                    break;
                case CoordinateType.DegreesDecimalMinutes:
                    targetLongitude = ConvertDecimalDegreesToDegreeDecimalMinutes(ddLongitude);
                    targetLatitude = ConvertDecimalDegreesToDegreeDecimalMinutes(ddLatitude);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(coordinateType), coordinateType, null);
            }

            return new WeatherLocation(Name, targetLongitude, targetLatitude);
        }

        private static double ConvertDegreeDecimalMinutesToDecimalDegrees(double ddmValue)
        {
            var degrees = Math.Floor(ddmValue);
            var minutes = (ddmValue - degrees) * 100;
            return degrees + (minutes / 60);
        }

        private static double ConvertDecimalDegreesToDegreeDecimalMinutes(double ddValue)
        {
            var degrees = Math.Floor(ddValue);
            var minutes = (ddValue - degrees) * 60;
            return degrees + (minutes / 100);
        }

        public override string ToString()
        {
            return $"{Name} ({Longitude}, {Latitude}, {CoordinateType})";
        }
    }
}