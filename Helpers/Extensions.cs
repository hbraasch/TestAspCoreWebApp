namespace EasyMinutesServer.Helpers
{
    public static class Extensions
    {
        public static T GetMax<T, U>(this IEnumerable<T> data, Func<T, U> f) where U : IComparable
        {
            return data.Aggregate((i1, i2) => f(i1).CompareTo(f(i2)) > 0 ? i1 : i2);
        }

        public static bool IsNullOrEmpty(this string value)
        {
            return string.IsNullOrEmpty(value);
        }
    }
}
