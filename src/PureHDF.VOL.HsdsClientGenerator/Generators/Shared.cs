using System.Text.RegularExpressions;

namespace PureHDF.VOL.HsdsClientGenerator
{
    public partial class Shared
    {
        [GeneratedRegex("(?<=[a-z])([A-Z])")]
        private static partial Regex SplitCamelCaseRegex();

        [GeneratedRegex("(?<=[a-z])([A-Z])")]
        private static partial Regex ToSnakeCaseRegex();

        // https://stackoverflow.com/questions/4135317/make-first-letter-of-a-string-upper-case-with-maximum-performance?rq=1
        public static string FirstCharToUpper(string input)
        {
            return input switch
            {
                null => throw new ArgumentNullException(nameof(input)),
                "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
                _ => input[0].ToString().ToUpper() + input[1..]
            };
        }

        public static string FirstCharToLower(string input)
        {
            return input switch
            {
                null => throw new ArgumentNullException(nameof(input)),
                "" => throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input)),
                _ => input[0].ToString().ToLower() + input[1..]
            };
        }

        public static string SplitCamelCase(string input)
        {
            return SplitCamelCaseRegex().Replace(input, " $1").Trim();
        }

        public static string ToSnakeCase(string input)
        {
            return ToSnakeCaseRegex().Replace(input, "_$1").Trim().ToLower();
        }
    }
}