using System;

namespace ArchiveServices.Common.Utilities
{
    public static class CastDbNull
    {
        public static T To<T>(object value, T defaultValue)
        {
            return (value != DBNull.Value) ? (T)value : defaultValue;
        }
    }

    public static class CastNull
    {
        public static T To<T>(object value, T defaultValue)
        {
            return (value != null) ? (T)Convert.ChangeType(value, typeof(T)) : defaultValue;
        }

    }

}
