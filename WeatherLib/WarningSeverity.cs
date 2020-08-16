namespace WeatherLib
{
    /// <summary>
    /// According to OASIS-CAP-1.2: <br/>
    /// The code denoting the severity of the subject event of a alert message
    /// </summary>
    public enum WarningSeverity
    {
        /// <summary>
        /// Default value
        /// </summary>
        None,
        /// <summary>
        /// Severity unknown
        /// </summary>
        Unknown,
        /// <summary>
        /// Minimal to no known threat to life or property
        /// </summary>
        Minor,
        /// <summary>
        /// Possible threat to life or property
        /// </summary>
        Moderate,
        /// <summary>
        /// Significant threat to life or property
        /// </summary>
        Severe,
        /// <summary>
        /// Extraordinary threat to life or property
        /// </summary>
        Extreme,
    }
}