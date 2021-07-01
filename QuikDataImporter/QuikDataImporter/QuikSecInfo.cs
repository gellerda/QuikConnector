using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FancyCandles;

namespace QuikDataImporter
{
    public class QuikSecInfo : FancyCandles.ISecurityInfo
    {
        public string SecID { get; private set; }

        public SecurityTypes SecurityType
        {
            get
            {
                if (ClassCode == "TQBR") return SecurityTypes.Stock;
                else if (ClassCode == "SPBFUT") return SecurityTypes.Futures;
                else return SecurityTypes.Undefined;
            }
        }

        public string ExchangeName { get { return "MOEX"; } }

        public string Ticker { get { return SecCode; } }

        public string SecurityName { get; private set; }

        public string ClassCode { get; private set; }

        public string SecCode { get; private set; }

        public QuikSecInfo(string classCode, string secCode, string Name)
        {
            SecID = $"{classCode}_{secCode}";
            ClassCode = classCode;
            SecCode = secCode;
            SecurityName = Name;
        }
    }
}
