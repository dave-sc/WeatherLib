using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using WeatherLib.Dwd.OpenData.Util;

namespace WeatherLib.Dwd.OpenData
{
    public class DwdOpenDataClient
    {
        private const string WarnCellListUrl = "https://www.dwd.de/DE/leistungen/opendata/help/warnungen/cap_warncellids_csv.csv?__blob=publicationFile&v=3";

        private const string StationCatalogUrl = "https://www.dwd.de/DE/leistungen/met_verfahren_mosmix/mosmix_stationskatalog.cfg?view=nasPublication";
        
        private readonly HttpClient _httpClient;

        public DwdOpenDataClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<IReadOnlyList<WeatherStation>> GetClosestStationsToAsync(WeatherLocation location)
        {
            var stations = await GetStationCatalogAsync();
            return stations
                .Where(s => !string.Equals(s.Location.Name, "SWIS-PUNKT", StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Location.GetDistanceTo(location))
                .Take(10)
                .ToList();
        }

        public async Task<IReadOnlyList<WeatherStation>> SearchStationsByNameAsync(string locationName)
        {
            locationName = locationName.ToLower();
            var stations = await GetStationCatalogAsync();
            return stations
                .Where(s => !string.Equals(s.Location.Name, "SWIS-PUNKT", StringComparison.OrdinalIgnoreCase))
                .Select(s => (station: s, similarity: LevenshteinDistance.Compute(s.Location?.Name?.ToLower() ?? "", locationName ?? "")))
                .OrderBy(t => t.similarity)
                .Take(10)
                .Select(t => t.station)
                .ToList();
        }
        public async Task<IReadOnlyList<WarningCell>> SearchWarningCellsByNameAsync(string locationName)
        {
            locationName = locationName.ToLower();
            var warningCells = await GetWarningCellCatalogAsync();
            return warningCells
                .Select(c => (warningCell: c, similarity: Math.Min(LevenshteinDistance.Compute(c.Name?.ToLower() ?? "", locationName ?? ""), LevenshteinDistance.Compute(c.ShortName?.ToLower() ?? "", locationName ?? ""))))
                .OrderBy(t => t.similarity)
                .Take(10)
                .Select(t => t.warningCell)
                .ToList();
        }

        public async Task<IReadOnlyList<ForecastDataPoint>> GetForecastAsync(WeatherStation weatherStation)
        {
            var forecastUrl =
                $"https://opendata.dwd.de/weather/local_forecasts/mos/MOSMIX_L/single_stations/{weatherStation.Id}/kml/MOSMIX_L_LATEST_{weatherStation.Id}.kmz";
            return (await GetXDocumentsFrom(forecastUrl))
                .Where(forecastXml => IsForecastForGivenStation(forecastXml, weatherStation))
                .Select(forecastXml => TryGetForecastDataFromXml(forecastXml, weatherStation))
                .Where(result => result.success)
                .SelectMany(result => result.forecastData)
                .ToList();
        }

        public async Task<IReadOnlyList<WeatherWarning>> GetWarningsAsync(WeatherLocation location)
        {
            var warningsUrl = "https://opendata.dwd.de/weather/alerts/cap/COMMUNEUNION_CELLS_STAT/Z_CAP_C_EDZW_LATEST_PVW_STATUS_PREMIUMCELLS_COMMUNEUNION_DE.zip";
            return (await GetXDocumentsFrom(warningsUrl))
                .Where(warningXml => IsWarningForGivenLocation(warningXml, location))
                .Select(TryGetWeatherWarningFromXml)
                .Where(result => result.success)
                .Select(result => result.warning)
                .ToList();
        }

        public async Task<IReadOnlyList<WeatherWarning>> GetWarningsAsync(WarningCell warningCell)
        {
            var warningsUrl = $"https://opendata.dwd.de/weather/alerts/cap/{(warningCell.IsCommune ? "COMMUNEUNION" : "DISTRICT")}_CELLS_STAT/Z_CAP_C_EDZW_LATEST_PVW_STATUS_PREMIUMCELLS_{(warningCell.IsCommune ? "COMMUNEUNION" : "DISTRICT")}_DE.zip";
            return (await GetXDocumentsFrom(warningsUrl))
                .Where(warningXml => IsWarningForGivenCell(warningXml, warningCell))
                .Select(TryGetWeatherWarningFromXml)
                .Where(result => result.success)
                .Select(result => result.warning)
                .ToList();
        }

        private async Task<IReadOnlyList<WeatherStation>> GetStationCatalogAsync()
        {
            IEnumerable<IReadOnlyDictionary<string, string>> stationCatalog;
            using (var stream = await GetHttpFileAsync(StationCatalogUrl))
                stationCatalog = await FixedWidthTableReader.ReadTableAsync(stream);

            WeatherStation ConvertToWeatherStation(IReadOnlyDictionary<string, string> values)
            {
                if (!values.TryGetValue("id", out var id))
                    return null;

                if (!values.TryGetValue("name", out var name))
                    return null;

                if (!values.TryGetValue("nb.", out var latitudeStr) && !values.TryGetValue("lat", out latitudeStr))
                    return null;
                var latitude = double.Parse(latitudeStr.Trim(), CultureInfo.InvariantCulture);

                if (!values.TryGetValue("el.", out var longitudeStr) && !values.TryGetValue("lon", out longitudeStr))
                    return null;
                var longitude = double.Parse(longitudeStr.Trim(), CultureInfo.InvariantCulture);

                if (!values.TryGetValue("elev", out var elevStr))
                    return null;
                var elev = double.Parse(elevStr.Trim(), CultureInfo.InvariantCulture);

                return new WeatherStation(id.Trim(), name.Trim(), CoordinateType.DegreesDecimalMinutes, longitude,
                    latitude, elev);
            }

            var stations = stationCatalog.Select(ConvertToWeatherStation).Where(s => s != null).ToList();
            return stations;
        }

        private async Task<IReadOnlyList<WarningCell>> GetWarningCellCatalogAsync()
        {
            IEnumerable<IReadOnlyDictionary<string, string>> warningCellCatalog;
            using (var stream = await GetHttpFileAsync(WarnCellListUrl))
                warningCellCatalog = await SimpleCsvTableReader.ReadTableAsync(stream);

            WarningCell ConvertToWarningCell(IReadOnlyDictionary<string, string> values)
            {
                if (!values.TryGetValue("# warncellid", out var id))
                    return null;

                if (!values.TryGetValue("name", out var name))
                    return null;

                if (!values.TryGetValue("kurzname", out var shortName))
                    return null;

                return new WarningCell(id.Trim(), name.Trim(), shortName.Trim());
            }

            var stations = warningCellCatalog.Select(ConvertToWarningCell).Where(s => s != null).ToList();
            return stations;
        }

        private bool IsForecastForGivenStation(XDocument forecastXml, WeatherStation weatherStation)
        {
            if (forecastXml.Root == null)
                return false;

            var kml = forecastXml.Root.GetNamespaceOfPrefix("kml");

            var places = forecastXml.Descendants(kml + "Placemark");
            var isForecastForStation = places.Any(p => string.Equals(p.Element(kml + "name")?.Value, weatherStation.Id, StringComparison.OrdinalIgnoreCase));
            return isForecastForStation;
        }

        private (bool success, IReadOnlyList<ForecastDataPoint> forecastData) TryGetForecastDataFromXml(XDocument forecastXml, WeatherStation weatherStation)
        {
            if (forecastXml == null)
                throw new ArgumentNullException(nameof(forecastXml));

            if (forecastXml.Root == null)
                return (false, null);

            var dwd = forecastXml.Root.GetNamespaceOfPrefix("dwd");
            var kml = forecastXml.Root.GetNamespaceOfPrefix("kml");

            var places = forecastXml.Descendants(kml + "Placemark");
            var placemark = places.FirstOrDefault(p => string.Equals(p.Element(kml + "name")?.Value, weatherStation.Id, StringComparison.OrdinalIgnoreCase));
            if (placemark == null)
                return (false, null);

            string ExtractForecastName(XElement f) => f.Attribute(dwd + "elementName")?.Value;
            double[] ExtractForecastValues(XElement f) => f.Value
                .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : double.NaN)
                .ToArray();
            
            var dataLines = placemark.Descendants(dwd + "Forecast")
                .OrderBy(ExtractForecastName)
                .ToDictionary(ExtractForecastName, ExtractForecastValues);
            double[] GetForecastByName(string elementName) => 
                dataLines.GetValueOrDefault(elementName, Array.Empty<double>());

            double ElementAtOrDefault(double[] arr, int i) => 
                i < arr.Length ? arr[i] : double.NaN;

            var timeSteps = forecastXml.Descendants(dwd + "ForecastTimeSteps").Elements().Select(t => DateTime.Parse(t.Value, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)).ToList();
            // https://opendata.dwd.de/weather/lib/MetElementDefinition.xml
            var temperature = GetForecastByName("TTT");
            var temperatureError = GetForecastByName("E_TTT");
            var precipitation = GetForecastByName("RR1c");
            var precipitationProbability = GetForecastByName("wwP");
            var windSpeed = GetForecastByName("FF");
            var windSpeedError = GetForecastByName("E_FF");
            var windDirection = GetForecastByName("DD");
            var pressure = GetForecastByName("PPPP");
            var pressureError = GetForecastByName("E_PPP");
            var cloudCover = GetForecastByName("Neff");
            var weather = GetForecastByName("ww");

            var detailedForecast = timeSteps
                .Select((time, index) => new ForecastDataPoint(time,
                    ElementAtOrDefault(temperature,index) - 273.15, ElementAtOrDefault(temperatureError,index),
                    ElementAtOrDefault(precipitation,index), ElementAtOrDefault(precipitationProbability,index),
                    ElementAtOrDefault(windSpeed,index), ElementAtOrDefault(windSpeedError,index), ElementAtOrDefault(windDirection,index),
                    ElementAtOrDefault(pressure,index), ElementAtOrDefault(pressureError,index),
                    ElementAtOrDefault(cloudCover,index), ParseWeatherType(ElementAtOrDefault(weather,index), ElementAtOrDefault(cloudCover,index)), (int)ElementAtOrDefault(weather,index)))
                .ToList();
            return (true, detailedForecast);
        }

        private bool IsWarningForGivenLocation(XDocument warningXml, WeatherLocation location)
        {
            throw new NotImplementedException();
        }

        private bool IsWarningForGivenCell(XDocument warningXml, WarningCell warningCell)
        {
            if (warningXml == null)
                throw new ArgumentNullException(nameof(warningXml));
            if (warningCell == null)
                throw new ArgumentNullException(nameof(warningCell));

            if (warningXml.Root == null)
                return false;

            var ns = warningXml.Root.Name.Namespace;

            var applicapleCellsForWarning = warningXml.Descendants(ns + "area")
                .Select(a => a.Element(ns + "geocode")?.Element(ns + "value")?.Value)
                .Where(c => !string.IsNullOrWhiteSpace(c));
            var isWarningForGivenCell = applicapleCellsForWarning.Any(c => string.Equals(c, warningCell.Id, StringComparison.OrdinalIgnoreCase));

            return isWarningForGivenCell;
        }

        private (bool success, WeatherWarning warning) TryGetWeatherWarningFromXml(XDocument warningXml)
        {
            if (warningXml == null)
                throw new ArgumentNullException(nameof(warningXml));

            if (warningXml.Root == null)
                return (false, null);

            var ns = warningXml.Root.Name.Namespace;
            var warningInfo = warningXml.Descendants(ns + "info").FirstOrDefault();
            if (warningInfo == null)
                return (false, null);

            var warningCode = warningInfo.Elements(ns + "eventCode")
                .FirstOrDefault(ec => string.Equals(ec.Element(ns + "valueName")?.Value, "II", StringComparison.OrdinalIgnoreCase))
                ?.Element(ns + "value")?.Value;
            var warningType = ParseWarningCode(warningCode ?? "-1");
            var shortText = warningInfo.Element(ns + "event")?.Value ?? "";
            var severity = ParseWarningSeverity(warningInfo.Element(ns + "severity")?.Value ?? "unknown");
            var certainty = ParseWarningCertainty(warningInfo.Element(ns + "certainty")?.Value ?? "unknown");
            var headline = warningInfo.Element(ns + "headline")?.Value ?? "";
            var fullText = warningInfo.Element(ns + "description")?.Value ?? "";
            if (!DateTime.TryParse(warningInfo.Element(ns + "onset")?.Value, out var startTime))
                startTime = DateTime.MinValue;
            if (!DateTime.TryParse(warningInfo.Element(ns + "expires")?.Value, out var endTime))
                endTime = DateTime.MaxValue;

            var warning = new WeatherWarning(startTime, endTime, warningType, severity, certainty, shortText, headline, fullText);
            return (true, warning);
        }

        private WarningSeverity ParseWarningSeverity(string severity)
        {
            switch (severity.ToLowerInvariant())
            {
                case "unknown":
                    return WarningSeverity.Unknown;
                case "minor":
                    return WarningSeverity.Minor;
                case "moderate":
                    return WarningSeverity.Moderate;
                case "severe":
                    return WarningSeverity.Severe;
                case "extreme":
                    return WarningSeverity.Extreme;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, "");
            }
        }

        private WarningCertainty ParseWarningCertainty(string certainty)
        {
            switch (certainty.ToLowerInvariant())
            {
                case "unknown":
                    return WarningCertainty.Unknown;
                case "unlikely":
                    return WarningCertainty.Unlikely;
                case "possible":
                    return WarningCertainty.Possible;
                case "likely":
                    return WarningCertainty.Likely;
                case "observed":
                    return WarningCertainty.Observed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(certainty), certainty, "");
            }
        }

        private WeatherWarningType ParseWarningCode(string warningCode)
        {
            if (!int.TryParse(warningCode, out var intCode))
                return WeatherWarningType.General;

            switch (intCode)
            {
                case 22: // FROST (frost)
                    return WeatherWarningType.Frost;
                case 24: // GLÄTTE (slippery road surfaces)
                    return WeatherWarningType.General;
                case 31: // GEWITTER (thunderstorms)
                    return WeatherWarningType.Thunder;
                case 33: // STARKES GEWITTER (heavy thunderstorms)
                    return WeatherWarningType.Thunder;
                case 34: // STARKES GEWITTER (heavy thunderstorms)
                    return WeatherWarningType.Thunder;
                case 36: // STARKES GEWITTER (heavy thunderstorms)
                    return WeatherWarningType.Thunder;
                case 38: // STARKES GEWITTER (heavy thunderstorms)
                    return WeatherWarningType.Thunder;
                case 40: // SCHWERES GEWITTER mit ORKANBÖEN (severe thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 41: // SCHWERES GEWITTER mit EXTREMEN ORKANBÖEN (extreme thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 42: // SCHWERES GEWITTER mit HEFTIGEM STARKREGEN (severe thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 44: // SCHWERES GEWITTER mit ORKANBÖEN und HEFTIGEM STARKREGEN (severe thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 45: // SCHWERES GEWITTER mit EXTREMEN ORKANBÖEN und HEFTIGEM STARKREGEN (extreme thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 46: // SCHWERES GEWITTER mit HEFTIGEM STARKREGEN und HAGEL (severe thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 48: // SCHWERES GEWITTER mit ORKANBÖEN, HEFTIGEM STARKREGEN und HAGEL (severe thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 49: // SCHWERES GEWITTER mit EXTREMEN ORKANBÖEN, HEFTIGEM STARKREGEN und HAGEL (extreme thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 51: // WINDBÖEN (wind gusts)
                    return WeatherWarningType.Storm;
                case 52: // STURMBÖEN (gale-force gusts)
                    return WeatherWarningType.Storm;
                case 53: // SCHWERE STURMBÖEN (storm-force gusts)
                    return WeatherWarningType.Storm;
                case 54: // ORKANARTIGE BÖEN (violent storm gusts)
                    return WeatherWarningType.Storm;
                case 55: // ORKANBÖEN (hurricane-force gusts)
                    return WeatherWarningType.Storm;
                case 56: // EXTREME ORKANBÖEN (extreme hurricane-force gusts)
                    return WeatherWarningType.Storm;
                case 57: // STARKWIND (strong wind)
                    return WeatherWarningType.Storm;
                case 58: // STURM (storm)
                    return WeatherWarningType.Storm;
                case 59: // NEBEL (fog)
                    return WeatherWarningType.General;
                case 61: // STARKREGEN (heavy rain)
                    return WeatherWarningType.Rain;
                case 62: // HEFTIGER STARKREGEN (very heavy rain)
                    return WeatherWarningType.Rain;
                case 63: // DAUERREGEN (persistent rain)
                    return WeatherWarningType.Rain;
                case 64: // ERGIEBIGER DAUERREGEN (heavy persistent rain)
                    return WeatherWarningType.Rain;
                case 65: // EXTREM ERGIEBIGER DAUERREGEN (extremely persistent rain)
                    return WeatherWarningType.Rain;
                case 66: // EXTREM HEFTIGER STARKREGEN (extremely heavy rain)
                    return WeatherWarningType.Rain;
                case 70: // LEICHTER SCHNEEFALL (light snowfall)
                    return WeatherWarningType.Snow;
                case 71: // SCHNEEFALL (snowfall)
                    return WeatherWarningType.Snow;
                case 72: // STARKER SCHNEEFALL (heavy snowfall)
                    return WeatherWarningType.Snow;
                case 73: // EXTREM STARKER SCHNEEFALL (extremely heavy snowfall)
                    return WeatherWarningType.Snow;
                case 74: // SCHNEEVERWEHUNG (snow drifts)
                    return WeatherWarningType.Snow;
                case 75: // STARKE SCHNEEVERWEHUNG (heavy snow drifts)
                    return WeatherWarningType.Snow;
                case 76: // SCHNEEFALL und SCHNEEVERWEHUNG (snowfall with snow drifts)
                    return WeatherWarningType.Snow;
                case 77: // STARKER SCHNEEFALL und SCHNEEVERWEHUNG (heavy snowfall with snow drifts)
                    return WeatherWarningType.Snow;
                case 78: // EXTREM STARKER SCHNEEFALL und SCHNEEVERWEHUNG (extremely heavy snowfall with snow drifts)
                    return WeatherWarningType.Snow;
                case 79: // LEITERSEILSCHWINGUNGEN (powerline vibrations)
                    return WeatherWarningType.General;
                case 81: // FROST (frost)
                    return WeatherWarningType.Frost;
                case 82: // STRENGER FROST (severe frost)
                    return WeatherWarningType.Frost;
                case 84: // GLÄTTE (widespread icy surfaces)
                    return WeatherWarningType.General;
                case 85: // GLATTEIS (widespread glaze)
                    return WeatherWarningType.General;
                case 87: // GLATTEIS (localized glaze)
                    return WeatherWarningType.General;
                case 88: // TAUWETTER (thaw)
                    return WeatherWarningType.General;
                case 89: // STARKES TAUWETTER (heavy thaw)
                    return WeatherWarningType.General;
                case 90: // GEWITTER (thunderstorms)
                    return WeatherWarningType.Thunder;
                case 91: // STARKES GEWITTER (heavy thunderstorms)
                    return WeatherWarningType.Thunder;
                case 92: // SCHWERES GEWITTER (severe thunderstorms)
                    return WeatherWarningType.Thunder;
                case 93: // EXTREMES GEWITTER (extreme thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 95: // SCHWERES GEWITTER mit EXTREM HEFTIGEM STARKREGEN und HAGEL (extreme thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 96: // SCHWERES GEWITTER mit ORKANBÖEN, EXTREM HEFTIGEM STARKREGEN und HAGEL (extreme thunderstorms)
                    return WeatherWarningType.Thunderstorm;
                case 98: // TEST-WARNUNG (test warning)
                    return WeatherWarningType.General;
                case 99: // TEST-UNWETTERWARNUNG (test warning)
                    return WeatherWarningType.General;
                case 11: // BÖEN (minor gusts (coast))
                    return WeatherWarningType.Storm;
                case 12: // WIND (moderate gusts (coast))
                    return WeatherWarningType.Storm;
                case 13: // STURM (severe gusts (coast))
                    return WeatherWarningType.Storm;
                case 14: // Starkwind (minor gusts (sea))
                    return WeatherWarningType.Storm;
                case 15: // Sturm (moderate gusts (sea))
                    return WeatherWarningType.Storm;
                case 16: // schwerer Sturm (severe gusts (sea))
                    return WeatherWarningType.Storm;
                case 246: // UV-INDEX (UV Index)
                    return WeatherWarningType.General;
                case 247: // HITZE (heat)
                    return WeatherWarningType.Heat;
                default:
                    return WeatherWarningType.General;
            }
        }

        public WeatherType ParseWeatherType(double weatherType, double cloudCover)
        {
            if (double.IsNaN(weatherType))
                return WeatherType.None;

            return ParseWeatherType((int) weatherType, cloudCover);
        }

        public WeatherType ParseWeatherType(int weatherType, double cloudCover)
        {
            switch (weatherType)
            {
                // haze, smoke, dust or snd
                case 4: // visibility reduced by smoke
                    return WeatherType.Smoke;
                case 5: // haze
                    return WeatherType.Haze;
                case 6: // widespread dust in suspension not raised by wind
                    return WeatherType.Dust;
                case 7: // dust or sand raised by wind
                    return WeatherType.Dust;
                case 8: // well developed dust or sand whirls
                    return WeatherType.DustWhirl;
                case 9: //  dust or sand storm within sight but not at station
                    return WeatherType.DustStorm;

                // non-precipitation events
                case 10: // mist
                    return WeatherType.Haze;
                case 11: // patches of shallow fog
                    return WeatherType.Fog;
                case 12: // continuous shallow fog
                    return WeatherType.Fog;
                case 17: // thunderstorm but no precipitation falling at station
                    return WeatherType.Thunder;
                case 18: // squalls within sight but no precipitation falling at station
                    return WeatherType.Storm;
                case 19: // funnel clouds within sight
                    return WeatherType.None;

                // duststorm, sandstorm
                case 30: // slight to moderate duststorm, decreasing in intensity
                    return WeatherType.DustStorm;
                case 31: // slight to moderate duststorm, no change
                    return WeatherType.DustStorm;
                case 32: // slight to moderate duststorm, increasing in intensity
                    return WeatherType.DustStorm;
                case 33: // severe duststorm, decreasing in intensity
                    return WeatherType.SevereDustStorm;
                case 34: // severe duststorm, no change
                    return WeatherType.SevereDustStorm;
                case 35: // severe duststorm, increasing in intensity
                    return WeatherType.SevereDustStorm;

                // drifting or blowing snow
                case 36: // slight to moderate drifting snow, below eye level
                    return WeatherType.SnowDrifts;
                case 37: // heavy drifting snow, below eye level
                    return WeatherType.SnowDrifts;
                case 38: // slight to moderate drifting snow, above eye level
                    return WeatherType.SnowDrifts;
                case 39: // heavy drifting snow, above eye level
                    return WeatherType.SnowDrifts;

                // fog or ice fog
                case 40: // Fog at a distance 
                    return WeatherType.Fog;
                case 41: // patches of fog
                    return WeatherType.Fog;
                case 42: // fog, sky visible, thinning
                    return WeatherType.Fog;
                case 43: // fog, sky not visible, thinning 
                    return WeatherType.Fog;
                case 44: // fog, sky visible, no change 
                    return WeatherType.Fog;
                case 45: // fog, sky not visible, no change
                    return WeatherType.Fog;
                case 46: // fog, sky visible, becoming thicker 
                    return WeatherType.Fog;
                case 47: // fog, sky not visible, becoming thicker
                    return WeatherType.Fog;
                case 48: // fog, depositing rime, sky visible 
                    return WeatherType.FrostFog;
                case 49: //  fog, depositing rime, sky not visible
                    return WeatherType.FrostFog;

                // drizzle
                case 50: // intermittent light drizzle
                    return WeatherType.LightIntermittentDrizzle;
                case 51: // continuous light drizzle
                    return WeatherType.LightDrizzle;
                case 52: // intermittent moderate drizzle 
                    return WeatherType.IntermittentDrizzle;
                case 53: // continuous moderate drizzle 
                    return WeatherType.Drizzle;
                case 54: // intermittent heavy drizzle
                    return WeatherType.IntermittentDrizzle;
                case 55: // continuous heavy drizzle
                    return WeatherType.Drizzle;
                case 56: // light freezing drizzle
                    return WeatherType.FreezingLightDrizzle;
                case 57: // moderate to heavy freezing drizzle
                    return WeatherType.FreezingDrizzle;
                case 58: // light drizzle and rain
                    return WeatherType.Rain;
                case 59: // moderate to heavy drizzle and rain 
                    return WeatherType.Rain;

                // rain
                case 60: // intermittent light rain 
                    return WeatherType.IntermittentLightRain;
                case 61: // continuous light rain
                    return WeatherType.LightRain;
                case 62: // intermittent moderate rain
                    return WeatherType.IntermittentRain;
                case 63: // continuous moderate rain
                    return WeatherType.Rain;
                case 64: // intermittent heavy rain
                    return WeatherType.IntermittentHeavyRain;
                case 65: // continuous heavy rain
                    return WeatherType.HeavyRain;
                case 66: // light freezing rain 
                    return WeatherType.FreezingLightRain;
                case 67: // moderate to heavy freezing rain 
                    return WeatherType.FreezingRain;
                case 68: // light rain and snow 
                    return WeatherType.LightSleet;
                case 69: // moderate to heavy rain and snow 
                    return WeatherType.Sleet;

                // snow
                case 70: // intermittent light snow
                    return WeatherType.IntermittentLightSnow;
                case 71: // continuous light snow
                    return WeatherType.LightSnow;
                case 72: // intermittent moderate snow 
                    return WeatherType.IntermittentSnow;
                case 73: // continuous moderate snow
                    return WeatherType.Snow;
                case 74: // intermittent heavy snow 
                    return WeatherType.IntermittentSnow;
                case 75: // continuous heavy snow
                    return WeatherType.Snow;
                case 76: // diamond dust 
                    return WeatherType.Icy;
                case 77: // snow grains 
                    return WeatherType.Snow;
                case 78: // snow crystals
                    return WeatherType.Snow;
                case 79: // ice pellets
                    return WeatherType.Icy;

                // showers
                case 80: // light rain showers
                    return WeatherType.LightRain;
                case 81: // moderate to heavy rain showers
                    return WeatherType.Rain;
                case 82: // violent rain showers
                    return WeatherType.HeavyRain;
                case 83: // light rain and snow showers 
                    return WeatherType.LightSleet;
                case 84: // moderate to heavy rain and snow showers 
                    return WeatherType.Sleet;
                case 85: // light snow showers 
                    return WeatherType.LightSnow;
                case 86: // moderate to heavy snow showers 
                    return WeatherType.Snow;
                case 87: // light snow/ice pellet showers 
                    return WeatherType.Snow;
                case 88: // moderate to heavy snow/ice pellet showers 
                    return WeatherType.Snow;
                case 89: // light hail showers
                    return WeatherType.LightHail;
                case 90: // moderate to heavy hail showers
                    return WeatherType.Hail;

                // thunderstorm
                case 91: // thunderstorm in past hour, currently only light rain
                    return WeatherType.LightRain;
                case 92: // thunderstorm in past hour, currently only moderate to heavy rain
                    return WeatherType.Rain;
                case 93: // thunderstorm in past hour, currently only light snow or rain/snow mix
                    return WeatherType.LightSnow;
                case 94: // thunderstorm in past hour, currently only moderate to heavy snow or rain/snow mix 
                    return WeatherType.Snow;
                case 95: // light to moderate thunderstorm
                    return WeatherType.ThunderStorm;
                case 96: // light to moderate thunderstorm with hail 
                    return WeatherType.ThunderStormHail;
                case 97: // heavy thunderstorm 
                    return WeatherType.HeavyThunderStorm;
                case 98: // heavy thunderstorm with duststorm
                    return WeatherType.HeavyThunderStorm;
                case 99: // heavy thunderstorm with hail 
                    return WeatherType.HeavyThunderStormHail;

                default:
                    if (cloudCover < 35)
                        return WeatherType.Clear;
                    if (cloudCover < 55)
                        return WeatherType.CloudyLight;
                    if (cloudCover < 80)
                        return WeatherType.CloudyMedium;
                    return WeatherType.CloudyVery;
            }
        }

        private async Task<Stream> GetHttpFileAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        private IEnumerable<ZipArchiveEntry> GetAllFilesInZipArchive(Stream archiveStream, bool leaveOpen = false)
        {
            using (var zipArchive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen))
            {
                foreach (var entry in zipArchive.Entries)
                {
                    yield return entry;
                }
            }
        }

        private XDocument GetXDocument(ZipArchiveEntry archiveEntry)
        {
            XDocument xDoc;
            using (var stream = archiveEntry.Open())
                xDoc = XDocument.Load(stream);

            return xDoc;
        }

        private async Task<IEnumerable<XDocument>> GetXDocumentsFrom(string url)
        {
            return GetAllFilesInZipArchive(await GetHttpFileAsync(url)).Select(GetXDocument);
        }
    }
}
