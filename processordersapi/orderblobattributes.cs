using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace processordersapi
{
    public class OrderBlobAttributes
    {
        static readonly Regex blobUrlRegexExtract = new Regex(@"^\S*/([^/]+)/(([\d^-]+)-([\w]+))\.csv$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public string FullUrl { get; private set; }
        public string Filename { get; private set; }
        public string BatchPrefix { get; private set; }
        public string Filetype { get; private set; }
        public string ContainerName { get; private set; }

        public static OrderBlobAttributes Parse(string fullUri)
        {
            var regexMatch = blobUrlRegexExtract.Match(fullUri);
            /*
            Match = https://khsvrlessohsaorders.blob.core.windows.net/orders/20180518151300-OrderHeaderDetails.csv
            $1 = orders
            $2 = 20180518151300-OrderHeaderDetails
            $3 = 20180518151300
            $4 = OrderHeaderDetails
            */

            if (regexMatch.Success)
            {
                return new OrderBlobAttributes
                {
                    FullUrl = regexMatch.Groups[0].Value,
                    ContainerName = regexMatch.Groups[1].Value,
                    Filename = regexMatch.Groups[2].Value,
                    BatchPrefix = regexMatch.Groups[3].Value,
                    Filetype = regexMatch.Groups[4].Value
                };
            }

            return null;
        }
    }
}
