namespace WeatherLib
{
    /// <summary>
    /// According to OASIS-CAP-1.2: <br/>
    /// The code denoting the certainty of the subject event of a alert message
    /// </summary>
    public enum WarningCertainty
    {
        /// <summary>
        /// Certainty unknown
        /// </summary>
        Unknown,
        /// <summary>
        /// Not expected to occur (p ~ 0)
        /// </summary>
        Unlikely,
        /// <summary>
        /// Possible but not likely (p &lt;= ~50%)
        /// </summary>
        Possible,
        /// <summary>
        /// Likely (p &gt; ~50%)
        /// </summary>
        Likely,
        /// <summary>
        /// Determined to have occurred or to be ongoing
        /// </summary>
        Observed,
    }
}