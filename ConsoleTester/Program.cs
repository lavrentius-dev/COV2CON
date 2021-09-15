using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ConsoleTester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            for (DateTime date = DateTime.Today.AddDays(-2); date <= DateTime.Today.AddDays(9); date = date.AddDays(1))
                Console.WriteLine($"{date}");

            Console.ReadLine();

            //CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
            //culture.Calendar.TwoDigitYearMax = 2099;


            //string StrDate11 = "2020-03-12";
            //string StrDate12 = "2020-03-13";
            //string StrDate21 = "3/12/20";
            //string StrDate22 = "3/13/20";

            //if (DateTime.TryParse(StrDate11, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime Result11))
            //    Debug.WriteLine($"* '{StrDate11}' => '{Result11}'");
            //else
            //    Debug.WriteLine($"* Cannot parse '{StrDate11}'");

            //if (DateTime.TryParse(StrDate12, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime Result12))
            //    Debug.WriteLine($"* '{StrDate12}' => '{Result12}'");
            //else
            //    Debug.WriteLine($"* Cannot parse '{StrDate12}'");

            //if (DateTime.TryParseExact(StrDate21, new string[] { "M/d/yy", "M/d/yyyy", "MM/dd/yy", "MM/dd/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime Result21))
            //    Debug.WriteLine($"* '{StrDate21}' => '{Result21}'");
            //else
            //    Debug.WriteLine($"* Cannot parse '{StrDate21}'");

            //if (DateTime.TryParseExact(StrDate22, new string[] { "M/d/yy", "M/d/yyyy", "MM/dd/yy", "MM/dd/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime Result22))
            //    Debug.WriteLine($"* '{StrDate22}' => '{Result22}'");
            //else
            //    Debug.WriteLine($"* Cannot parse '{StrDate22}'");
        }
    }


    public class CountryACPOInfo
    {
        public Dictionary<DateTime, double> DictPositiveRate, DictActiveCasesPerMillion, DictACPO;


        public CountryACPOInfo()
        {
            DictPositiveRate = new Dictionary<DateTime, double>();
            DictActiveCasesPerMillion = new Dictionary<DateTime, double>();
            DictACPO = new Dictionary<DateTime, double>();
        }
    }
}
