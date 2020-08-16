﻿using System;
using System.Collections.Generic;
using System.Text;

namespace WeatherLib
{
    public class ForecastDataPoint
    {
        public ForecastDataPoint(DateTime time, double temperature, double temperatureError, double windSpeed, double windSpeedError, double pressure, double pressureError, double cloudCover, WeatherType weather, int synopCode)
        {
            Time = time;
            Temperature = temperature;
            TemperatureError = temperatureError;
            WindSpeed = windSpeed;
            WindSpeedError = windSpeedError;
            Pressure = pressure;
            PressureError = pressureError;
            CloudCover = cloudCover;
            Weather = weather;
            SynopCode = synopCode;
        }

        public DateTime Time { get; }
        public double Temperature { get; }
        public double TemperatureError { get; }
        public double WindSpeed { get; }
        public double WindSpeedError { get; }
        public double Pressure { get; }
        public double PressureError { get; }
        public double CloudCover { get; }
        public WeatherType Weather { get; }
        public int SynopCode { get; }

        public override string ToString()
        {
            return $"{Time:s}: {Weather} ({SynopCode}); {Temperature:0.#} ±{TemperatureError:0.#} °C; {CloudCover:0.#} %Cover; {WindSpeed:0.#} ±{WindSpeedError:0.#} m/s; {Pressure/100:0.#} ±{PressureError/100:0.#} hPa;";
        }
    }
}
