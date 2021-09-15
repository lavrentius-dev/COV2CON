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
        IEnumerable<IDictionary<string, string>> Cov2Data_HospICU;
        readonly string CovidUrlECDCHospICU = "https://opendata.ecdc.europa.eu/covid19/hospitalicuadmissionrates/csv";  // actual file name is 'data.csv' as of 2020-10-14
        readonly string CovidFilenameECDCHospICU = "COV2CON.dataset.ECDC.hospicu.csv";


        //
        // Parse ICU data from Cov2Data_HospICU
        //
        private async Task<bool> GetECDCData()
        {
            ShowNetworkAnomalyWarning = false;

            string res = await GetECDCFile(CovidUrlECDCHospICU, CovidFilenameECDCHospICU);
            if ((res != null) && (res != ""))
            {
                try
                {
                    // parse HospICU data => Cov2Data_HospICU
                    Cov2Data_HospICU = ParseCsvWithHeaderIgnoreErrors(res);

                    foreach (IDictionary<string, string> item in Cov2Data_HospICU)
                    {
                        string name = item["country"];
                        if (name == "Czechia")
                            name = "Czech Republic";

                        if (CountryToPopulationDict.ContainsKey(name) == true)
                        {
                            if (item["indicator"] == "Daily ICU occupancy")
                            {
                                if (double.TryParse(item["value"], out double tmpICUPerMillion))
                                {
                                    tmpICUPerMillion /= CountryToPopulationDict[name];  // daily ICU case per million => use available date for this data point
                                    if (tmpICUPerMillion > MaxYVar[4])
                                        MaxYVar[4] = tmpICUPerMillion;
                                }
                            }
                            else if (item["indicator"] == "Weekly new ICU admissions per 100k")
                            {
                                if (double.TryParse(item["value"], out double tmpICUPerMillion))
                                {
                                    tmpICUPerMillion *= 1.42857;  // average daily ICU case per million => use just one date for this week/data point
                                    if (tmpICUPerMillion > MaxYVar[4])
                                        MaxYVar[4] = tmpICUPerMillion;
                                }
                            }
                        }
                        else
                            Debug.WriteLine($"* GetECDCData: Country name '{name}' not found in CountryToPopulationDict");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"** UpdateDataSet | #1 Exceptions raised: {ex.Message}");
                }
            }

            ShowNetworkAnomalyWarning = true;

            return true;
        }


        private async Task<string> GetECDCFile(string Url, string FileName)
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
                    Debug.WriteLine($"** GetECDCFile | #1 Exceptions raised: {ex.Message}");
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
                        Debug.WriteLine($"** GetECDCFile | #2 Exceptions raised: {ex.Message}");
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
                        Debug.WriteLine($"** GetECDCFile | #3 Exceptions raised: {ex.Message}");
                    }
                }

                return null;
            }
        }
    }
}
