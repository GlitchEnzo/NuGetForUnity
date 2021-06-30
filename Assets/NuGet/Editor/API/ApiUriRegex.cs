using System.Text.RegularExpressions;

namespace App
{
    public class ApiUriRegex
    {
        public static readonly Regex nugetV2ApiRegex = new Regex(@"\S*(\/v2\/)\S*", RegexOptions.IgnoreCase);
        public static readonly Regex nugetV3ApiRegex = new Regex(@"\S*(\/v3\/)\S*", RegexOptions.IgnoreCase);
        public static readonly Regex nugetV4ApiRegex = new Regex(@"\S*(\/v4\/)\S*", RegexOptions.IgnoreCase);

        public bool IsGreaterThanV2(ApiUriType uriType)
        {
            return uriType.Equals(ApiUriType.Nugetv3) 
                || uriType.Equals(ApiUriType.Nugetv4);
        }
        
        public bool IsGreaterThanV2(string uri)
        {
            ApiUriType uriType = DecideApiUriType(uri);
            return IsGreaterThanV2(uriType);
        }
        
        public ApiUriType DecideApiUriType(string uri)
        {
            ApiUriType result = ApiUriType.Unkown;

            if (nugetV2ApiRegex.IsMatch(uri))
            {
                result = ApiUriType.Nugetv2;
            } else if (nugetV3ApiRegex.IsMatch(uri))
            {
                result = ApiUriType.Nugetv3;
            } else if (nugetV4ApiRegex.IsMatch(uri))
            {
                result = ApiUriType.Nugetv4;
            }

            return result;
        }
    }
}