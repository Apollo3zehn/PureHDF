using System.Text.RegularExpressions;

namespace PureHDF.HsdsClientGenerator
{
    public class Shared
    {
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
            return Regex.Replace(input, "(?<=[a-z])([A-Z])", " $1").Trim();
        }

        public static string ToSnakeCase(string input)
        {
            return Regex.Replace(input, "(?<=[a-z])([A-Z])", "_$1").Trim().ToLower();
        }

    }
}