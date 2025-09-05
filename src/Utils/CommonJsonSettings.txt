using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FollowTheWay.Utils
{
    /// <summary>
    /// Common JSON serialization settings for FollowTheWay
    /// </summary>
    public static class CommonJsonSettings
    {
        /// <summary>
        /// Default JSON settings for API communication
        /// </summary>
        public static readonly JsonSerializerSettings Default = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.None
        };

        /// <summary>
        /// Compact JSON settings for data compression
        /// </summary>
        public static readonly JsonSerializerSettings Compact = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.None,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        /// <summary>
        /// Pretty-printed JSON settings for debugging
        /// </summary>
        public static readonly JsonSerializerSettings Pretty = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            Formatting = Formatting.Indented
        };

        /// <summary>
        /// Get default settings (for backward compatibility)
        /// </summary>
        public static JsonSerializerSettings GetSettings()
        {
            return Default;
        }
    }
}