namespace Server_Messenger
{
    internal static class StringExtensions
    {
        /// <summary>
        /// This method only replaces the first char to make the string camelCase
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ToCamelCase(this string str)
        {
            return string.IsNullOrEmpty(str)
                ? str
                : char.ToLowerInvariant(str[0]) + str[1..];
        }
    }
}
