using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WeatherLib
{
    public class WeatherService : IDisposable
    {
        private readonly IWeatherDataProvider _dataProvider;
        private readonly string _providerIdentifier;
        private readonly IWeatherDataCache _weatherDataCache;
        private Timer _updateTimer;
        private bool _hasUpdateCompletedOnce;

        public WeatherService(IWeatherDataProvider dataProvider, IWeatherDataCache weatherDataCache)
        {
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            _providerIdentifier = dataProvider.GetIdentifier();
            _weatherDataCache = weatherDataCache ?? throw new ArgumentNullException(nameof(weatherDataCache));
            UpdateSchedule = new Queue<DateTime>(0);
            WeatherForecast = Array.Empty<DayForecast>();
        }

        public async Task InitializeAsync(IEnumerable<TimeSpan> updateSchedule)
        {
            if (updateSchedule == null)
                throw new ArgumentNullException(nameof(updateSchedule));

            await UpdateDataAsync(false, false);
            var today = DateTime.Now.Date;
            var now = DateTime.Now;
            var triggerTimes = updateSchedule.Select(t => today + t).Select(t => t <= now ? t.AddDays(1) : t);
            UpdateSchedule = new Queue<DateTime>(triggerTimes);
            _updateTimer = new Timer(OnUpdateTimerTick, null, TimeSpan.FromSeconds(60), TimeSpan.FromMinutes(5));
        }

        public Queue<DateTime> UpdateSchedule { get; private set; }

        public DateTime? LastUpdate { get; set; }

        public IReadOnlyList<DayForecast> WeatherForecast { get; private set; }

        public event EventHandler<IReadOnlyList<DayForecast>> WeatherForecastChanged;

        public event EventHandler<Exception> UpdateError;

        public async Task<IReadOnlyList<DayForecast>> UpdateForecastAsync()
        {
            await UpdateDataAsync(true, true);
            return WeatherForecast;
        }

        private async Task UpdateDataAsync(bool updateForecast, bool updateWarnings)
        {
            IReadOnlyList<WeatherWarning> warnings;
            if (updateWarnings)
            {
                try
                {
                    warnings = await _dataProvider.GetWarningsAsync();
                    await _weatherDataCache.RememberWarnings(_providerIdentifier, warnings);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Failed to get weather warnings: {e.Message}");
                    warnings = await _weatherDataCache.GetRememberedWarnings(_providerIdentifier);
                }
            }
            else
            {
                warnings = await _weatherDataCache.GetRememberedWarnings(_providerIdentifier);
            }

            IReadOnlyList<ForecastDataPoint> forecastDataToUse;
            if (updateForecast)
            {
                IReadOnlyList<ForecastDataPoint> forecastData;
                try
                {
                    forecastData = await _dataProvider.GetForecastAsync();
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Failed to get weather forecast: {e.Message}");
                    forecastData = null;
                }

                if (forecastData != null)
                {
                    var forecastStartTime = forecastData.Select(d => d.Time).Min();
                    var rememberedData = await _weatherDataCache.GetRememberedForecastData(_providerIdentifier);
                    var rememberedDataToPrepend = rememberedData?.Where(d => d.Time >= forecastStartTime.Date && d.Time < forecastStartTime);
                    forecastDataToUse = rememberedDataToPrepend?.Concat(forecastData).ToList() ?? forecastData;
                    await _weatherDataCache.RememberForecastData(_providerIdentifier, forecastDataToUse);
                }
                else if (updateWarnings)
                {
                    forecastDataToUse = await _weatherDataCache.GetRememberedForecastData(_providerIdentifier);
                }
                else
                {
                    return;
                }
            }
            else
            {
                forecastDataToUse = await _weatherDataCache.GetRememberedForecastData(_providerIdentifier);
            }

            if (updateWarnings || updateForecast)
                LastUpdate = DateTime.Now;

            var forecast = BuildWeatherForecast(forecastDataToUse, warnings);
            WeatherForecast = forecast;
            WeatherForecastChanged?.Invoke(this, forecast);
        }

        private IReadOnlyList<DayForecast> BuildWeatherForecast(IReadOnlyList<ForecastDataPoint> forecastData, IReadOnlyList<WeatherWarning> warnings)
        {
            return forecastData.GroupBy(d => d.Time.Date)
                .Select(g => BuildWeatherForecastForDay(g.Key, g.ToArray(), (IReadOnlyList<WeatherWarning>)warnings?.Where(w => g.Key >= w.StartTime.Date && g.Key <= w.EndTime.Date)?.ToList() ?? Array.Empty<WeatherWarning>()))
                .ToList();
        }

        private DayForecast BuildWeatherForecastForDay(DateTime date, IReadOnlyList<ForecastDataPoint> forecastData, IReadOnlyList<WeatherWarning> warnings)
        {
            var minTemp = forecastData.Select(p => p.Temperature).Min();
            var maxTemp = forecastData.Select(p => p.Temperature).Max();

            var morningDataPoints = forecastData.Where(f => f.Time.TimeOfDay >= new TimeSpan(5, 0, 0) && f.Time.TimeOfDay < new TimeSpan(10, 0, 0)).ToList();
            var noonDataPoints = forecastData.Where(f => f.Time.TimeOfDay >= new TimeSpan(10, 0, 0) && f.Time.TimeOfDay < new TimeSpan(15, 30, 0)).ToList();
            var eveningDataPoints = forecastData.Where(f => f.Time.TimeOfDay >= new TimeSpan(15, 30, 0) && f.Time.TimeOfDay < new TimeSpan(23, 0, 0)).ToList();

            WeatherType GetSignificantWeather(IEnumerable<WeatherType> weathers)
            {
                return weathers.GroupBy(w => w).OrderByDescending(grp => grp.Count()).ThenByDescending(grp => Convert.ToInt32(grp.Key))
                    .Select(grp => grp.Key).FirstOrDefault();
            }

            SectionForecast morningForecast;
            if (morningDataPoints.Count > 0)
                morningForecast = new SectionForecast(ForecastSection.Morning, morningDataPoints.Select(p => p.Temperature).Min(), GetSignificantWeather(morningDataPoints.Select(p => p.Weather)));
            else
                morningForecast = null;


            SectionForecast noonForecast;
            if (noonDataPoints.Count > 0)
                noonForecast = new SectionForecast(ForecastSection.Noon, noonDataPoints.Select(p => p.Temperature).Max(), GetSignificantWeather(noonDataPoints.Select(p => p.Weather)));
            else
                noonForecast = null;


            SectionForecast eveningForecast;
            if (eveningDataPoints.Count > 0)
                eveningForecast = new SectionForecast(ForecastSection.Evening, eveningDataPoints.Select(p => p.Temperature).Min(), GetSignificantWeather(eveningDataPoints.Select(p => p.Weather)));
            else
                eveningForecast = null;

            var forecast = new DayForecast(date, minTemp, maxTemp, GetSignificantWeather(forecastData.Select(p => p.Weather)), morningForecast, noonForecast, eveningForecast, forecastData, warnings);
            return forecast;
        }

        private void OnUpdateTimerTick(object state)
        {
            if (UpdateSchedule.Count <= 0)
                return;

            bool doUpdate;
            bool isUpdateBecauseOfSchedule;
            if (DateTime.Now >= UpdateSchedule.Peek())
            {
                doUpdate = true;
                isUpdateBecauseOfSchedule = true;
            }
            else if (!_hasUpdateCompletedOnce)
            {
                doUpdate = true;
                isUpdateBecauseOfSchedule = false;
            }
            else
            {
                doUpdate = false;
                isUpdateBecauseOfSchedule = false;
            }

            if (doUpdate)
            {
                try
                {
                    UpdateDataAsync(true, true).GetAwaiter().GetResult();
                    _hasUpdateCompletedOnce = true;
                }
                catch (Exception ex)
                {
                    try
                    {
                        UpdateError?.Invoke(this, ex);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                    return;
                }

                if (isUpdateBecauseOfSchedule)
                    UpdateSchedule.Enqueue(UpdateSchedule.Dequeue().AddDays(1));
            }
        }

        public void Dispose()
        {
            _updateTimer?.Dispose();
        }
    }
}
