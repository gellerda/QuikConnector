using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics; // Debug.WriteLine("Error...");
using System.Collections.ObjectModel;
using System.Globalization;
using FancyCandles;
using System.Windows.Threading;

namespace QuikDataImporter
{
    enum QuikDataCode : byte
    {
        DataZero = 0,
        SecCatalog = 1,
        Candles = 2
    };
    //******************************************************************************************************************************************
    public class QuikData
    {
        public readonly byte dataCode;
        public readonly string dataString;

        //---------------------------------------------------------------------------------------------------------------------------------
        public QuikData(byte dataCode, string dataString)
        {
            this.dataCode = dataCode;
            this.dataString = dataString;
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        public static List<ISecurityInfo> ProcessSecCatalog(string dataString)
        {
            List<ISecurityInfo> newSecCatalog = new List<ISecurityInfo>();

            string[] secs = dataString.Split(QuikCandlesSourceProvider.ObjectsDelimiterSymbol);
            for (int i = 0; i < secs.Length; i++)
            {
                string[] sec_params = secs[i].Split(QuikCandlesSourceProvider.ParamsDelimiterSymbol);

                if (sec_params.Length != 3)
                    throw new ArgumentException("There must be 3 parameters for every sec.");

                QuikSecInfo secInfo = new QuikSecInfo(sec_params[0], sec_params[1], sec_params[2]);
                newSecCatalog.Add(secInfo);
            }

            return newSecCatalog;
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        internal static void ProcessCandles(string dataString, Dictionary<string, QCandlesSource> candleCollections)
        {
            string[] candleStrings = dataString.Split(QuikCandlesSourceProvider.ObjectsDelimiterSymbol);
            for (int i = 0; i < candleStrings.Length; i++)
            {
                string[] candle_params = candleStrings[i].Split(QuikCandlesSourceProvider.ParamsDelimiterSymbol);

                if (candle_params.Length != 14)
                    throw new ArgumentException("There must be 14 parameters for every candle.");

                //int.TryParse(candle_params[0], out int secIndex);
                //int.TryParse(candle_params[1], out int timeFrame);
                int.TryParse(candle_params[1], out int candleIndex);
                int.TryParse(candle_params[2], out int year);
                int.TryParse(candle_params[3], out int month);
                int.TryParse(candle_params[4], out int day);
                int.TryParse(candle_params[5], out int hour);
                int.TryParse(candle_params[6], out int minute);
                int.TryParse(candle_params[7], out int sec);
                int.TryParse(candle_params[8], out int ms);

                NumberStyles style = NumberStyles.Float;
                CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");
                double.TryParse(candle_params[9], style, culture, out double O);
                double.TryParse(candle_params[10], style, culture, out double H);
                double.TryParse(candle_params[11], style, culture, out double L);
                double.TryParse(candle_params[12], style, culture, out double C);
                double.TryParse(candle_params[13], style, culture, out double V);

                DateTime t = new DateTime(year, month, day, hour, minute, sec, ms);
                Candle newCandle = new Candle(t, O, H, L, C, V);

                string candleCollections_key = candle_params[0]; // Key для candleSources в формате "classCode_secCode_quikTimeFrame"
                //int candleSourcesKey = QuikCandlesSourceProvider.CandleSourcesKey(secIndex, (QuikTimeFrame)timeFrame);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!candleCollections.ContainsKey(candleCollections_key)) return;
                    //candleCollectionsInUse.Add(candleCollectionsInUse_key, new CandlesSourceWithUserCounter());

                    IList<ICandle> candleCollection = candleCollections[candleCollections_key];

                    if (candleIndex >= candleCollection.Count)
                    {
                        int dN = candleIndex - candleCollection.Count;
                        for (int j = 0; j < dN; j++)
                            candleCollection.Add(new Candle(new DateTime(), 0, 0, 0, 0, 0));

                        candleCollection.Add(newCandle);
                    }
                    else
                        candleCollection[candleIndex] = newCandle;
                });
            }
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------
    }
    //******************************************************************************************************************************************
    //******************************************************************************************************************************************
}
