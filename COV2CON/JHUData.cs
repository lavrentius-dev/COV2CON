using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;


namespace COV2CON
{
    public partial class MainWindow : Window
    {
        IEnumerable<IDictionary<string, string>> Cov2Data_Confirmed, Cov2Data_Deaths, Cov2Data_Recovered;
        readonly string CovidUrlJHUConfirmed = "https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_time_series/time_series_covid19_confirmed_global.csv";
        readonly string CovidUrlJHUDeaths = "https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_time_series/time_series_covid19_deaths_global.csv";
        readonly string CovidUrlJHURecovered = "https://raw.githubusercontent.com/CSSEGISandData/COVID-19/master/csse_covid_19_data/csse_covid_19_time_series/time_series_covid19_recovered_global.csv";
        readonly string CovidFilenameJHUConfirmed = "COV2CON.dataset.JHU.confirmed.csv";
        readonly string CovidFilenameJHUDeaths = "COV2CON.dataset.JHU.deaths.csv";
        readonly string CovidFilenameJHURecovered = "COV2CON.dataset.JHU.recovered.csv";


        //
        // Parse active case data from Cov2Data_Confirmed, Cov2Data_Deaths, Cov2Data_Confirmed
        //
        private async Task<Dictionary<string, Dictionary<DateTime, (int, double)>>> GetJHUData()
        {
            ShowNetworkAnomalyWarning = false;

            // create consolidated dictionary to store data from all 3 tables
            Dictionary<string, Dictionary<DateTime, (int, double)>> ConsolidatedJHUTable = new Dictionary<string, Dictionary<DateTime, (int, double)>>();

            string res1 = await GetJHUFile(CovidUrlJHUConfirmed, CovidFilenameJHUConfirmed);
            if ((res1 != null) && (res1 != ""))
            {
                try
                {
                    string res2 = await GetJHUFile(CovidUrlJHUDeaths, CovidFilenameJHUDeaths);
                    if ((res2 != null) && (res2 != ""))
                    {
                        string res3 = await GetJHUFile(CovidUrlJHURecovered, CovidFilenameJHURecovered);
                        if ((res3 != null) && (res3 != ""))
                        {
                            // parse csv files
                            Cov2Data_Confirmed = ParseCsvWithHeaderIgnoreErrors(res1);
                            Cov2Data_Deaths = ParseCsvWithHeaderIgnoreErrors(res2);
                            Cov2Data_Recovered = ParseCsvWithHeaderIgnoreErrors(res3);
                            ICollection<string> keys;

                            //// keep only relevant data
                            //StringCollection InvalidKeys = new StringCollection();
                            //InvalidKeys.AddRange(new string[] {
                            //    "Province/State",
                            //    "Lat",
                            //    "Long"
                            //});
                            //keys = Cov2Data_Confirmed.FirstOrDefault().Keys;
                            //if (keys != null)
                            //    foreach (var dict in Cov2Data_Confirmed)
                            //        foreach (var key in keys)
                            //            if (InvalidKeys.Contains(key) == true)
                            //                dict.Remove(key);
                            //keys = Cov2Data_Deaths.FirstOrDefault().Keys;
                            //if (keys != null)
                            //    foreach (var dict in Cov2Data_Deaths)
                            //        foreach (var key in keys)
                            //            if (InvalidKeys.Contains(key) == true)
                            //                dict.Remove(key);
                            //keys = Cov2Data_Recovered.FirstOrDefault().Keys;
                            //if (keys != null)
                            //    foreach (var dict in Cov2Data_Recovered)
                            //        foreach (var key in keys)
                            //            if (InvalidKeys.Contains(key) == true)
                            //                dict.Remove(key);

                            keys = Cov2Data_Confirmed.FirstOrDefault().Keys;
                            if (keys != null)
                            {
                                // insert confirmed entries
                                foreach (var dict in Cov2Data_Confirmed)
                                {
                                    if (dict.ContainsKey("Country/Region") == true)
                                    {
                                        string CountryRegion = dict["Country/Region"];
                                        if (ConsolidatedJHUTable.ContainsKey(CountryRegion) == false)
                                            ConsolidatedJHUTable[CountryRegion] = new Dictionary<DateTime, (int, double)>();
                                        foreach (var key in keys)
                                            if (key != "Country/Region")
                                                if (double.TryParse(dict[key], out double numValue))
                                                {
                                                    if (DateTime.TryParseExact(
                                                        key, 
                                                        new string[] { "M/d/yy", "MM/dd/yy", "MM/dd/yyyy" }, 
                                                        CultureInfo.InvariantCulture, 
                                                        DateTimeStyles.None, 
                                                        out DateTime dateResult
                                                        ))
                                                    {
                                                        DateTime DateKey = dateResult;
                                                        if (ConsolidatedJHUTable[CountryRegion].ContainsKey(DateKey) == false)
                                                            ConsolidatedJHUTable[CountryRegion][DateKey] = (0, numValue);
                                                        else
                                                        {
                                                            (int, double) tmp = ConsolidatedJHUTable[CountryRegion][DateKey];
                                                            tmp.Item2 += numValue;
                                                            ConsolidatedJHUTable[CountryRegion][DateKey] = tmp;
                                                        }
                                                    }
                                                }
                                    }
                                }

                                if (ConsolidatedJHUTable.Count > 0)
                                {
                                    // subtract deaths where entry dates match
                                    keys = Cov2Data_Deaths.FirstOrDefault().Keys;
                                    if (keys != null)
                                        foreach (var dict in Cov2Data_Deaths)
                                            if (dict.ContainsKey("Country/Region") == true)
                                            {
                                                string CountryRegion = dict["Country/Region"];
                                                foreach (var key in keys)
                                                    if (key != "Country/Region")
                                                        if (double.TryParse(dict[key], out double numValue))
                                                        {
                                                            if (DateTime.TryParseExact(
                                                                key,
                                                                new string[] { "M/d/yy", "MM/dd/yy", "MM/dd/yyyy" },
                                                                CultureInfo.InvariantCulture,
                                                                DateTimeStyles.None,
                                                                out DateTime dateResult
                                                                ))
                                                            {
                                                                DateTime DateKey = dateResult;
                                                                if (ConsolidatedJHUTable[CountryRegion].ContainsKey(DateKey) == true)
                                                                {
                                                                    (int, double) tmp = ConsolidatedJHUTable[CountryRegion][DateKey];
                                                                    tmp.Item1 = 1;
                                                                    tmp.Item2 -= numValue;
                                                                    ConsolidatedJHUTable[CountryRegion][DateKey] = tmp;
                                                                }
                                                            }
                                                        }
                                            }

                                    // subtract recovered where entry dates match and deaths were already subtracted
                                    keys = Cov2Data_Recovered.FirstOrDefault().Keys;
                                    if (keys != null)
                                        foreach (var dict in Cov2Data_Recovered)
                                            if (dict.ContainsKey("Country/Region") == true)
                                            {
                                                string CountryRegion = dict["Country/Region"];
                                                foreach (var key in keys)
                                                    if (key != "Country/Region")
                                                        if (double.TryParse(dict[key], out double numValue))
                                                        {
                                                            if (DateTime.TryParseExact(
                                                                key,
                                                                new string[] { "M/d/yy", "MM/dd/yy", "MM/dd/yyyy" },
                                                                CultureInfo.InvariantCulture,
                                                                DateTimeStyles.None,
                                                                out DateTime dateResult
                                                                ))
                                                            {
                                                                DateTime DateKey = dateResult;
                                                                if (ConsolidatedJHUTable[CountryRegion].ContainsKey(DateKey) == true)
                                                                {
                                                                    (int, double) tmp = ConsolidatedJHUTable[CountryRegion][DateKey];
                                                                    if (tmp.Item1 == 1)
                                                                    {
                                                                        tmp.Item1 = 2;
                                                                        tmp.Item2 -= numValue;
                                                                        ConsolidatedJHUTable[CountryRegion][DateKey] = tmp;
                                                                    }
                                                                }
                                                            }
                                                        }
                                            }

                                    // keep only entries where both subtractions were performed (i.e., entries with dates when all three values were available)
                                    foreach (string country in ConsolidatedJHUTable.Keys)
                                    {
                                        foreach (DateTime date in ConsolidatedJHUTable[country].Keys.ToList())  // we generate a tmp key list here for foreach purposes
                                            if (ConsolidatedJHUTable[country][date].Item1 != 2)
                                                ConsolidatedJHUTable[country].Remove(date);
                                        if (ConsolidatedJHUTable[country].Count == 0)
                                            ConsolidatedJHUTable.Remove(country);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"** GetJHUData | Exceptions raised: {ex.Message}");
                }
            }

            ShowNetworkAnomalyWarning = true;

            return ConsolidatedJHUTable;
        }


        private async Task<string> GetJHUFile(string Url, string FileName)
        {
            string res = await DoCurlAsync(Url);

            if ((WasNetAnomDetected == false) && (res != null) && (res != ""))
            {
                try
                {
                    File.WriteAllText(AppDataDir + @"\" + FileName, res, System.Text.Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"** GetJHUFile | #1 Exceptions raised: {ex.Message}");
                }

                return res;
            }
            else
            {
                if (File.Exists(AppDataDir + @"\" + FileName) == true)
                {
                    try
                    {
                        res = File.ReadAllText(AppDataDir + @"\" + FileName, System.Text.Encoding.UTF8); 
                        return res;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"** GetJHUFile | #2 Exceptions raised: {ex.Message}");
                    }
                }

                if (File.Exists(AppDir + @"\" + FileName) == true)
                {
                    try
                    {
                        res = File.ReadAllText(AppDir + @"\" + FileName, System.Text.Encoding.UTF8);
                        return res;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"** GetJHUFile | #3 Exceptions raised: {ex.Message}");
                    }
                }

                return null;
            }
        }
    }
}
