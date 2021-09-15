using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;


namespace COV2CON
{
    public partial class MainWindow : Window
    {
        IEnumerable<IDictionary<string, string>> Cov2Data;
        private readonly DateTime LastUpdate = Properties.Settings.Default.LastUpdate;
        private DateTime CurrentUpdate;
        private bool ShowNetworkAnomalyWarning = true;
        readonly string CovidUrlMain = "https://covid.ourworldindata.org/data/owid-covid-data.csv";
        //readonly string CovidUrlAlt = "https://github.com/owid/covid-19-data/blob/master/public/data/owid-covid-data.csv"; // TODO if CovidUrlMain becomes unstable


        //ref: https://medium.com/bynder-tech/c-why-you-should-use-configureawait-false-in-your-library-code-d7837dce3d7f
        public async Task<string> DoCurlAsync(string uri)
        {
            WasNetAnomDetected = false;

            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                try
                {
                    using (var httpResponse = await httpClient.GetAsync(uri))
                        return await httpResponse.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    WasNetAnomDetected = true;

                    if (ShowNetworkAnomalyWarning == true)
                    {
                        MessageBox.Show("Anomaly detected while trying to connect to the COVID-19 " +
                                        "database server, with message: " + Environment.NewLine + Environment.NewLine +
                                        "'" + ex.Message + "'" + Environment.NewLine + Environment.NewLine +
                                        "This may happen if your computer is not connected to the Internet " +
                                        "or the source data server response is abnormal. Please check your " +
                                        "network connection and, if the problem persists, try again later.",
                                        "COV2CON", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    LabelUpdate.Content = "Trying local file...";

                    return null;
                }
            }
        }


        public async Task<string> ReadDataSet()
        {
            string res = "";
            string CovidUrl = CovidUrlMain;


            IsUpdateRunning = true;
            IsUpdateRequired = true;
            CanTryManualUpdate = false;
            WasNetAnomDetected = false;
            WasLocalDataSetFound = false;
            WasCurrentUpdateRead = false;
            Cov2Data = null;
            Cov2Data_Confirmed = null; Cov2Data_Deaths = null; Cov2Data_Recovered = null;

            // check if LastUpdate is older than date of currently available update
            try
            {
                Uri myUri = new Uri(CovidUrl);
                HttpWebRequest myHttpWebRequest = (HttpWebRequest)WebRequest.Create(myUri);
                myHttpWebRequest.Method = "HEAD";  // not interested in file contents 
                HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();

                // if we were able to get the date from server
                if (myHttpWebResponse.StatusCode == HttpStatusCode.OK)
                {
                    WasCurrentUpdateRead = true;
                    CurrentUpdate = myHttpWebResponse.LastModified;
                    //Debug.WriteLine($"* LastModified = '{CurrentUpdate}'");

                    // compare dates to see if an update is required (LastUpdate setting was saved when local file was saved)
                    if (DateTime.Compare(LastUpdate, CurrentUpdate) >= 0)
                        IsUpdateRequired = false;
                }
                else
                    Debug.WriteLine($"* Unexpected HttpWebResponse: '{myHttpWebResponse.StatusDescription}'");

                myHttpWebResponse.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"** Exception getting the update's LastModified from server: '{ex.Message}'");
            }

            // Check for existing dataset file
            if (File.Exists(AppDataDir + @"\COV2CON.dataset.OWID.csv") == true)
                WasLocalDataSetFound = true;

            // Logic
            //
            //    - if ((WasCurrentUpdateRead == false) or (IsUpdateRequired == true - BASED ON DATE COMPARISON))
            //          try to get updated dataset from server
            //          if success
            //              parse update => Cov2Data
            //              save update to local file
            //              save last update setting
            //          else 
            //              if local file exists
            //                  parse local file => Cov2Data
            //              else
            //                  let user know we tried our best
            //    - else 
            //          if local file exists
            //              parse local file => Cov2Data
            //          else
            //              let user know we tried our best
            //

            if ((WasCurrentUpdateRead == false) || (IsUpdateRequired == true))
            {
                // try to get updated dataset from server
                res = await DoCurlAsync(CovidUrl);

                // if success getting update over internet
                if ((WasNetAnomDetected == false) && (res != null) && (res != ""))
                {
                    try
                    {
                        // parse update => Cov2Data
                        Cov2Data = ParseCsvWithHeaderIgnoreErrors(res);

                        // save update to local file
                        Directory.CreateDirectory(AppDataDir);
                        File.WriteAllText(AppDataDir + @"\COV2CON.dataset.OWID.csv", res, System.Text.Encoding.UTF8);

                        // store LastUpdate setting
                        Properties.Settings.Default.LastUpdate = CurrentUpdate;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"** UpdateDataSet | #1 Exceptions raised: {ex.Message}");
                    }
                }
                else
                {
                    CanTryManualUpdate = true;

                    // if local file exists
                    if (WasLocalDataSetFound == true)
                    {
                        try
                        {
                            // parse local file => Cov2Data
                            string FileName = AppDataDir + @"\COV2CON.dataset.OWID.csv";
                            res = File.ReadAllText(FileName, System.Text.Encoding.UTF8); ;
                            Cov2Data = ParseCsvWithHeaderIgnoreErrors(res);

                            FileInfo fi = new FileInfo(FileName);
                            CurrentUpdate = fi.LastWriteTime;

                            // let user know this is not optimal                         
                            MessageBox.Show("COVID-19 data could not be downloaded online, results shown are based on previous, local data. " +
                                            "Please click the 'Update' button later to try refreshing the data.",
                                            "COV2CON", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"** UpdateDataSet | #2 Exceptions raised: {ex.Message}");
                        }
                    }

                    // last resort: try installation data set
                    else
                    {
                        if (File.Exists(AppDir + @"\COV2CON.dataset.OWID.csv") == true)
                        {
                            try
                            {
                                // parse installation file => Cov2Data
                                string FileName = AppDir + @"\COV2CON.dataset.OWID.csv";
                                res = File.ReadAllText(FileName, System.Text.Encoding.UTF8); ;
                                Cov2Data = ParseCsvWithHeaderIgnoreErrors(res);

                                FileInfo fi = new FileInfo(FileName);
                                CurrentUpdate = fi.LastWriteTime;

                                // let user know this is not optimal                         
                                MessageBox.Show("COVID-19 data could not be downloaded online, results shown are based on previous, local data. " +
                                                "Please click the 'Update' button later to try refreshing the data.",
                                                "COV2CON", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"** UpdateDataSet | #3 Exceptions raised: {ex.Message}");
                            }
                        }
                        else
                        {
                            // let user know what to do 
                            MessageBox.Show("COVID-19 data neither could be downloaded online, nor was it found on this computer. " +
                                            "It is not possible to display results at this time, please try again later.",
                                            "COV2CON", MessageBoxButton.OK, MessageBoxImage.Warning);
                            LabelUpdate.Content = "Pending";
                        }
                    }
                }
            }
            else
            {
                CanTryManualUpdate = true;

                // if local file exists
                if (WasLocalDataSetFound == true)
                {
                    try
                    {
                        // parse local file => Cov2Data
                        string FileName = AppDataDir + @"\COV2CON.dataset.OWID.csv";
                        res = File.ReadAllText(FileName, System.Text.Encoding.UTF8); ;
                        Cov2Data = ParseCsvWithHeaderIgnoreErrors(res);

                        FileInfo fi = new FileInfo(FileName);
                        CurrentUpdate = fi.LastWriteTime;

                        // let user know this is not optimal                         
                        MessageBox.Show("COVID-19 data could not be obtained online, results shown are based on older local data. " +
                                        "Please click the 'Update' button later to try refreshing the data.",
                                        "COV2CON", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"** UpdateDataSet | #2 Exceptions raised: {ex.Message}");
                    }
                }

                // last resort: try installation data set
                else
                {
                    if (File.Exists(AppDir + @"\COV2CON.dataset.OWID.csv") == true)
                    {
                        try
                        {
                            // parse installation file => Cov2Data
                            string FileName = AppDir + @"\COV2CON.dataset.OWID.csv";
                            res = File.ReadAllText(FileName, System.Text.Encoding.UTF8); ;
                            Cov2Data = ParseCsvWithHeaderIgnoreErrors(res);

                            FileInfo fi = new FileInfo(FileName);
                            CurrentUpdate = fi.LastWriteTime;

                            // let user know this is not optimal                         
                            MessageBox.Show("COVID-19 data could not be obtained online, results shown are based on older local data. " +
                                            "Please click the 'Update' button later to try refreshing the data.",
                                            "COV2CON", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"** UpdateDataSet | #3 Exceptions raised: {ex.Message}");
                        }
                    }
                    else
                    {
                        // let user know what to do 
                        MessageBox.Show("COVID-19 data neither could be obtained online, nor was it found on this computer. " +
                                        "It is not possible to display results at this time, please try again later.",
                                        "COV2CON", MessageBoxButton.OK, MessageBoxImage.Warning);
                        LabelUpdate.Content = "Pending...";
                    }
                }
            }

            // clean up data dictionary
            StringCollection ValidKeys = new StringCollection();
            ValidKeys.AddRange(new string[] {
                "location",
                "date",
                "total_cases_per_million",
                "positive_rate",
                "population"
            });
            ICollection<string> keys = Cov2Data.FirstOrDefault().Keys;
            if (keys != null)
                foreach (var dict in Cov2Data)
                    foreach (var key in keys)
                        if (ValidKeys.Contains(key) == false)
                            dict.Remove(key);

            // find MinDate, MaxDate, MinYVar, MaxYVar across the entire dataset
            MinDate = DateTime.MaxValue;
            MaxDate = DateTime.MinValue;
            MaxYVar = new double[5] { double.MinValue, double.MinValue, double.MinValue, double.MinValue, double.MinValue };

            foreach (IDictionary<string, string> item in Cov2Data)
            {
                DateTime tmpDateTime = Convert.ToDateTime(item["date"]);
                if (DateTime.Compare(tmpDateTime, MinDate) < 0)
                    MinDate = tmpDateTime;

                if (DateTime.Compare(tmpDateTime, MaxDate) > 0)
                    MaxDate = tmpDateTime;

                double.TryParse(item["positive_rate"], out double tmpPositiveRate);
                if (tmpPositiveRate > MaxYVar[0])
                    MaxYVar[0] = tmpPositiveRate;

                double.TryParse(item["total_cases_per_million"], out double tmpTotalCasesPerMillion);
                if (tmpTotalCasesPerMillion > MaxYVar[1])
                    MaxYVar[1] = tmpTotalCasesPerMillion;
            }
            MinDate = Convert.ToDateTime(MinDate.ToString("yyyy-MM-dd"));
            MaxDate = Convert.ToDateTime(MaxDate.ToString("yyyy-MM-dd"));
            GraphNumDays = (MaxDate - MinDate).Days;
            SliderGraphValuePerDay = SliderGraph.Maximum / (double)GraphNumDays;
            MaxYVar[0] = Math.Round(MaxYVar[0], 3);
            MaxYVar[1] = Math.Round(MaxYVar[1], 0);

            // get country list as dummyDict Keys collection
            CountryToPopulationDict = new Dictionary<string, double>();
            string loc = "";
            foreach (var dict in Cov2Data)
            {
                if (dict["location"] != loc)
                {
                    loc = dict["location"];
                    if (double.TryParse(dict["population"], out double dummy0))
                        CountryToPopulationDict[loc] = dummy0 / 1e6;  // million people
                    else
                        CountryToPopulationDict[loc] = -1;
                }
            }
            DatasetCountryName = CountryToPopulationDict.Keys.OrderBy(q => q).ToList();

            //
            #region -- ECDC ICU cases integration
            //

            // data is reported inconsistently across the EU region as of 2020-10-15; implementation postponed until improved reporting becomes available
            //await GetECDCData();

            //
            #endregion -- ECDC ICU cases integration
            //

            //
            #region -- JHU active cases integration 1 of 3
            //

            // get absolute active cases by country and then by date, from JHU data sets
            Dictionary<string, Dictionary<DateTime, (int, double)>> ConsolidatedJHUTable = await GetJHUData();

            // divide active case count by population [million people]
            foreach (string CountryRegion in ConsolidatedJHUTable.Keys.ToList())  // we generate a tmp key list here for foreach purposes
            {
                switch (CountryRegion)
                {
                    case "Cabo Verde":
                        loc = "Cape Verde";
                        break;
                    case "Congo (Kinshasa)":
                        loc = "Democratic Republic of Congo";
                        break;
                    case "Czechia":
                        loc = "Czech Republic";
                        break;
                    case "Holy See":
                        loc = "Vatican";
                        break;
                    case "Korea, South":
                        loc = "South Korea";
                        break;
                    case "North Macedonia":
                        loc = "Macedonia";
                        break;
                    case "Timor-Leste":
                        loc = "Timor";
                        break;
                    case "US":
                        loc = "United States";
                        break;
                    default:
                        loc = CountryRegion;
                        break;
                }

                if (CountryToPopulationDict.ContainsKey(loc))
                    foreach (DateTime date in ConsolidatedJHUTable[CountryRegion].Keys.ToList())  // we generate a tmp key list here for foreach purposes
                    {
                        (int, double) tmp = ConsolidatedJHUTable[CountryRegion][date];
                        tmp.Item2 /= CountryToPopulationDict[loc];
                        ConsolidatedJHUTable[CountryRegion][date] = tmp;
                    }
                else
                    ConsolidatedJHUTable.Remove(CountryRegion);  // drop data for CountryRegion not in the OWID data set
            }

            // get MaxYVar[2]
            foreach (string country in ConsolidatedJHUTable.Keys)
                foreach (DateTime date in ConsolidatedJHUTable[country].Keys)
                    if (ConsolidatedJHUTable[country][date].Item2 > MaxYVar[2])
                        MaxYVar[2] = ConsolidatedJHUTable[country][date].Item2;
            MaxYVar[2] = Math.Round(MaxYVar[2], 0);

            //Debug.WriteLine($"* {ConsolidatedJHUTable.Count} {CountryToPopulationDict.Count} {ConsolidatedJHUTable.FirstOrDefault().Value.Count} {GraphNumDays}");

            //
            #endregion -- JHU active cases integration 1 of 3
            //

            SetupGraph(1);  // getting graph location, size
            double GraphXStick = GraphWidth / (double)GraphNumDays;

            // init CountryGraph and ACPO data structure
            DatasetCountryGraph = new Dictionary<string, CountryGraphInfo>();
            ConcurrentDictionary<string, CountryACPOInfo> ACPO = new ConcurrentDictionary<string, CountryACPOInfo>();

            await Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(DatasetCountryName, (name) =>
                {
                    // find the first occurence of name, then assume that entries for name are in sequence and keep parsing entries until different name found or end-of-list
                    CountryGraphInfo TmpInfo = new CountryGraphInfo(name);
                    ACPO[name] = new CountryACPOInfo();

                    IDictionary<string, string> LastEntry = new Dictionary<string, string>();
                    bool found = false;
                    foreach (var entry in Cov2Data)
                    {
                        // find country name in Cov2Data
                        if (found == false)
                        {
                            if (entry["location"] == name)
                                found = true;
                        }
                        else
                        {
                            if (entry["location"] != name)
                                break;
                        }

                        if (found == true)
                        {
                            LastEntry = (Dictionary<string, string>)entry;

                            // get date
                            DateTime currDate = Convert.ToDateTime(LastEntry["date"]);
                            int dayCnt = (currDate - MinDate).Days;

                            // populate graph for Positive rate: raw data is LastEntry["positive_rate"]                    
                            if (double.TryParse(LastEntry["positive_rate"], out double dummy1))
                            {
                                TmpInfo.pth[0].Add(new PointInfo(
                                    currDate.ToString("yyyy-MM-dd"),  //LastEntry["date"],
                                    new Point(GraphLeft + (double)dayCnt * GraphXStick, 506 - GraphBottom - dummy1 / MaxYVar[0] * GraphHeight),
                                    Math.Round(dummy1, 3)
                                    ));

                                ACPO[name].DictPositiveRate[currDate] = dummy1;
                            }

                            // populate graph for Total cases per million: raw data is LastEntry["total_cases_per_million"]
                            if (double.TryParse(LastEntry["total_cases_per_million"], out double dummy2))
                                TmpInfo.pth[1].Add(new PointInfo(
                                    currDate.ToString("yyyy-MM-dd"),  //LastEntry["date"],
                                    new Point(GraphLeft + (double)dayCnt * GraphXStick, 506 - GraphBottom - dummy2 / MaxYVar[1] * GraphHeight),
                                    Math.Round(dummy2, 0)
                                    ));
                        }
                    }

                    if (LastEntry.Count > 0)
                    {
                        //
                        #region -- JHU active cases integration 2 of 3
                        //

                        string CountryRegion;

                        switch (name)
                        {
                            case "Cape Verde":
                                CountryRegion = "Cabo Verde";
                                break;
                            case "Democratic Republic of Congo":
                                CountryRegion = "Congo (Kinshasa)";
                                break;
                            case "Czech Republic":
                                CountryRegion = "Czechia";
                                break;
                            case "Vatican":
                                CountryRegion = "Holy See";
                                break;
                            case "South Korea":
                                CountryRegion = "Korea, South";
                                break;
                            case "Macedonia":
                                CountryRegion = "North Macedonia";
                                break;
                            case "Timor":
                                CountryRegion = "Timor-Leste";
                                break;
                            case "United States":
                                CountryRegion = "US";
                                break;
                            default:
                                CountryRegion = name;
                                break;
                        }

                        // populate graph for Active cases per million: per CountryRegion, per date raw data is ConsolidatedJHUTable[CountryRegion][date].Item2
                        if (ConsolidatedJHUTable.ContainsKey(CountryRegion) == true)
                        {
                            foreach (DateTime date in ConsolidatedJHUTable[CountryRegion].Keys.ToList())
                            {
                                int dayCnt = (date - MinDate).Days;
                                double dummy3 = ConsolidatedJHUTable[CountryRegion][date].Item2;
                                TmpInfo.pth[2].Add(new PointInfo(
                                    date.ToString("yyyy-MM-dd"),
                                    new Point(GraphLeft + (double)dayCnt * GraphXStick, 506 - GraphBottom - dummy3 / MaxYVar[2] * GraphHeight),
                                    Math.Round(dummy3, 0)
                                    ));

                                ACPO[name].DictActiveCasesPerMillion[date] = dummy3;
                            }
                        }

                        //
                        #endregion -- JHU active cases integration 2 of 3
                        //

                        DatasetCountryGraph.Add(name, TmpInfo);
                    }
                    else
                        Debug.WriteLine("** LastEntry is empty");
                });
            });

            //
            #region -- JHU active cases integration 3 of 3
            //

            // compute DictACPOs
            int ACPOWindowSize = 14;
            int ACPOWindowMargin = 3;
            foreach (string country in ACPO.Keys.ToList())
                for (DateTime date = MinDate.AddDays(ACPOWindowSize - 1); date <= MaxDate; date = date.AddDays(1))
                {                   
                    double AvgPO = 0; int cnt = 0;
                    for (DateTime slidingDate = date.AddDays(-ACPOWindowSize + 1); slidingDate <= date; slidingDate = slidingDate.AddDays(1))
                        if (ACPO[country].DictPositiveRate.ContainsKey(slidingDate))
                        {
                            AvgPO += ACPO[country].DictPositiveRate[slidingDate];
                            cnt++;
                        }

                    if (cnt > 0)  // found at least 1 positive rate value within window
                    {
                        AvgPO /= cnt;

                        if (AvgPO > 0)
                        {
                            double MinAC = 0; cnt = 0;
                            for (DateTime slidingDate = date.AddDays(-ACPOWindowSize + 1); slidingDate <= date.AddDays(-ACPOWindowSize + ACPOWindowMargin); slidingDate = slidingDate.AddDays(1))
                                if (ACPO[country].DictActiveCasesPerMillion.ContainsKey(slidingDate))
                                {
                                    MinAC += ACPO[country].DictActiveCasesPerMillion[slidingDate];
                                    cnt++;
                                }

                            if (cnt > 0)  // found at least 1 active cases per million value within left window margin
                            {
                                MinAC /= cnt;

                                double MaxAC = 0; cnt = 0;
                                for (DateTime slidingDate = date.AddDays(-ACPOWindowMargin + 1); slidingDate <= date; slidingDate = slidingDate.AddDays(1))
                                    if (ACPO[country].DictActiveCasesPerMillion.ContainsKey(slidingDate))
                                    {
                                        MaxAC += ACPO[country].DictActiveCasesPerMillion[slidingDate];
                                        cnt++;
                                    }

                                if (cnt > 0)  // found at least 1 active cases per million value within right window margin
                                {
                                    MaxAC /= cnt;

                                    if (MaxAC > MinAC)
                                        ACPO[country].DictACPO[date] = Math.Sqrt(AvgPO * (MaxAC - MinAC) / (ACPOWindowSize - ACPOWindowMargin + 1));
                                    else
                                        ACPO[country].DictACPO[date] = 0;
                                }
                            }
                        }
                    }
                }

            // get MaxYVar[3]
            foreach (string country in ACPO.Keys.ToList())
                foreach (DateTime date in ACPO[country].DictACPO.Keys)
                    if (ACPO[country].DictACPO[date] > MaxYVar[3])
                        MaxYVar[3] = ACPO[country].DictACPO[date];
            MaxYVar[3] = Math.Round(MaxYVar[3], 3);
            if (MaxYVar[3] == 0)
                MaxYVar[3] = 1;

            // generate 4th graphs
            await Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(DatasetCountryName, (name) =>
                {
                    if (ACPO[name].DictACPO.Count > 0)
                    {
                        foreach (DateTime date in ACPO[name].DictACPO.Keys)
                        {
                            int dayCnt = (date - MinDate).Days;
                            double dummy4 = ACPO[name].DictACPO[date];
                            DatasetCountryGraph[name].pth[3].Add(new PointInfo(
                                date.ToString("yyyy-MM-dd"),
                                new Point(GraphLeft + (double)dayCnt * GraphXStick, 506 - GraphBottom - dummy4 / MaxYVar[3] * GraphHeight),
                                Math.Round(dummy4, 3)
                                ));
                        }
                    }
                });
            });

            //
            #endregion -- JHU active cases integration 3 of 3
            //

            // cleanup
            Cov2Data = null;
            Cov2Data_Confirmed = null; Cov2Data_Deaths = null; Cov2Data_Recovered = null;
            IsUpdateRunning = false;

            return res;
        }


        public int GetCountryNewIndex(string name)
        {
            int res = -1;

            if ((name != null) && (CountryItemToName != null) && (CountryItemToName.Count > 0))
            {
                // find in dictionary key that matches name
                string TheKey = null;
                foreach (var item in CountryItemToName)
                    if (name == item.Value)
                    {
                        TheKey = item.Key;
                        break;
                    }

                if (TheKey != null)
                {
                    // find in ListViewCountries item that matches key and return its index
                    for (int i = 0; i < ListViewCountries.Items.Count; i++)
                    {
                        string tmpStr = ListViewCountries.Items[i].ToString();
                        foreach (var item in CountryItemToName)
                            if (tmpStr == TheKey)
                            {
                                res = i;
                                break;
                            }
                    }
                }
            }

            return res;
        }


        public void UpdateCountryListAndMap(int idx)
        {
            if (DatasetCountryGraph?.Count > 0)
            {
                // update CountryViewListItemAndName
                CountryItemToName.Clear();

                if (DatasetCountryName?.Count > 0)
                {
                    // reset SVGCountryMap colours and tooltips
                    foreach (var item in SVGCountryMap)
                    {
                        item.Value.pth.Fill = defaultCountryFillBrush;
                        item.Value.pth.ToolTip = item.Key + " - (no data)";
                    }

                    Dictionary<string, double> tmpDict = new Dictionary<string, double>();
                    foreach (var key in DatasetCountryName)
                    {
                        if (DatasetCountryGraph.ContainsKey(key) == true)
                        {
                            double tmp = double.MinValue;
                            foreach (var item in DatasetCountryGraph[key].pth[idx])
                            {
                                if (item.date == SliderDate.ToString("yyyy-MM-dd"))
                                {
                                    tmp = item.val;

                                    // paint country in heat map                
                                    if (SVGCountryMap.ContainsKey(key) == true)
                                    {
                                        double offset = tmp / MaxYVar[idx];
                                        SolidColorBrush theBrush = new SolidColorBrush
                                        {
                                            Color = gsc.GetRelativeColor(offset)
                                        };
                                        SVGCountryMap[key].pth.Fill = theBrush;
                                        SVGCountryMap[key].pth.ToolTip = key + " - " + tmp.ToString();

                                        break;
                                    }
                                    else
                                    {
                                        //Debug.WriteLine($"* {key} not found in SVG map");
                                        break;
                                    }
                                }
                            }
                            tmpDict.Add(key, tmp);
                        }
                    }

                    List<KeyValuePair<string, double>> tmpDictSorted = tmpDict.ToList();
                    tmpDictSorted.Sort((x, y) => y.Value.CompareTo(x.Value));
                    foreach (KeyValuePair<string, double> item in tmpDictSorted)
                        CountryItemToName.Add(
                            item.Key + " - " + (item.Value != double.MinValue ? item.Value.ToString() : "(no data)"),
                            item.Key
                        );

                    // apply list filter
                    ListViewCountries.Items.Clear();  // this triggers ListViewCountries_SelectionChanged

                    if (KeepPreviousSelection == true)
                        TempSelection = new List<string>(PreviousSelection.Select(x => x.Clone() as string));

                    // populate ListViewCountries
                    foreach (var item in CountryItemToName)
                        if (item.Key.ToUpper().Contains(filter.ToUpper()))
                            ListViewCountries.Items.Add(item.Key);

                    // update selection                   
                    if ((ListViewCountries.Items != null) && (ListViewCountries.Items.Count > 0))
                    {
                        if (KeepPreviousSelection == true)
                        {
                            bool IsSelEmpty = true;
                            if ((TempSelection != null) && (TempSelection.Count > 0))
                            {
                                IterativelyUpdatingSelection = true;
                                for (int i = 0; i < TempSelection.Count; i++)
                                {
                                    // performance optimization: do not refresh plot until the last ListViewCountries_SelectionChanged
                                    if (i == TempSelection.Count - 1)
                                        IterativelyUpdatingSelection = false;

                                    int index = GetCountryNewIndex(TempSelection[i]);
                                    if (index != -1)
                                    {
                                        ListViewCountries.SelectedItems.Add(ListViewCountries.Items[index]);  // this triggers ListViewCountries_SelectionChanged
                                        IsSelEmpty = false;
                                    }
                                }
                            }
                            if (IsSelEmpty == true)
                            {
                                ListViewCountries.SelectedIndex = 0;
                            }
                        }
                        else
                        {
                            int index = GetCountryNewIndex(LastName);
                            if (index == -1)
                            {
                                ListViewCountries.SelectedIndex = 0;
                            }
                            else
                            {
                                ListViewCountries.SelectedIndex = index;
                            }
                        }

                        ListViewCountries.ScrollIntoView(ListViewCountries.SelectedItem);
                    }
                    else
                        Debug.WriteLine("* ListViewCountries is empty");
                }
                else
                    Debug.WriteLine("** DatasetCountryName is empty");
            }
            else
                Debug.WriteLine("* DatasetCountryGraph is null");
        }


        // ref: https://github.com/22222/CsvTextFieldParser
        public static IEnumerable<IDictionary<string, string>> ParseCsvWithHeaderIgnoreErrors(string csvInput)
        {
            using (var csvReader = new StringReader(csvInput))
            using (var parser = new NotVisualBasic.FileIO.CsvTextFieldParser(csvReader))
            {
                parser.TrimWhiteSpace = true;

                if (parser.EndOfData)
                    yield break;

                string[] headerFields;
                try
                {
                    headerFields = parser.ReadFields();
                }
                catch (NotVisualBasic.FileIO.CsvMalformedLineException ex)
                {
                    Debug.WriteLine($"** ParseCsvWithHeaderIgnoreErrors #1 | Failed to parse header line {ex.LineNumber}: '{parser.ErrorLine}'");
                    yield break;
                }

                while (!parser.EndOfData)
                {
                    string[] fields;

                    try
                    {
                        fields = parser.ReadFields();
                    }
                    catch (NotVisualBasic.FileIO.CsvMalformedLineException ex)
                    {
                        Debug.WriteLine($"** ParseCsvWithHeaderIgnoreErrors #2 | Failed to parse line {ex.LineNumber}: '{parser.ErrorLine}'");
                        continue;
                    }

                    int fieldCount = Math.Min(headerFields.Length, fields.Length);
                    IDictionary<string, string> fieldDictionary = new Dictionary<string, string>(fieldCount);

                    for (var i = 0; i < fieldCount; i++)
                    {
                        string headerField = headerFields[i];
                        string field = fields[i];
                        fieldDictionary[headerField] = field;
                    }

                    yield return fieldDictionary;
                }
            }
        }
    }
}
