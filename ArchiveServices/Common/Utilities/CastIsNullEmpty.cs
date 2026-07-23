
namespace ArchiveServices.Common.Utilities
{
    public static class CastIsNullEmpty
    {
        public static T To<T>(object value, object check, T defaultValue)
        {
            return (value != check) ? (T)value : defaultValue;
        }
    }
}
