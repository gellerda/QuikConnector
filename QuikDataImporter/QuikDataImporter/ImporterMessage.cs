using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics; // Debug.WriteLine("Error...");

namespace QuikDataImporter
{
    enum ImporterMessageCode : byte
    {
        MessageZero = 0,
        SendSecCatalog = 1,
        StartCandleExport = 2,
        StopCandleExport = 3,
        StopAllCandlesExport = 4
    }
    //******************************************************************************************************************************************
    public abstract class ImporterMessage
    {
        public readonly byte MessageCode;
        public readonly string MessageParams;

        public ImporterMessage(byte messageCode, string messageParams)
        {
            MessageCode = messageCode;
            MessageParams = messageParams;
        }
    }
    //******************************************************************************************************************************************
    public class ZeroImporterMessage : ImporterMessage
    {
        public ZeroImporterMessage()
            : base((byte)ImporterMessageCode.MessageZero, "")
        {
        }
    }
    //******************************************************************************************************************************************
    public class SendSecCatalogImporterMessage : ImporterMessage
    {
        public SendSecCatalogImporterMessage()
            : base((byte)ImporterMessageCode.SendSecCatalog, "")
        {
        }
    }
    //******************************************************************************************************************************************
    public class StartCandleExportImporterMessage : ImporterMessage
    {
        public StartCandleExportImporterMessage(IList<string> candlesSourceKeyList)  // candlesSourceKey := "classCode_secCode_quikTimeFrame"
            : base((byte)ImporterMessageCode.StartCandleExport, CreateMessageParams(candlesSourceKeyList))
        {
        }

        public StartCandleExportImporterMessage(string candlesSourceKey)  // candlesSourceKey := "classCode_secCode_quikTimeFrame"
            : base((byte)ImporterMessageCode.StartCandleExport, candlesSourceKey)
        {
        }
        //--------------------------------------------------------------------------------------------------------------------------------
        private static string CreateMessageParams(IList<string> candleSourcesKeyList) // candleSourcesKey := "classCode_secCode_quikTimeFrame"
        {
            string msgParams = string.Join(QuikCandlesSourceProvider.ObjectsDelimiterSymbol.ToString(), candleSourcesKeyList);
            Debug.WriteLine("msgParams=" + msgParams);
            return msgParams;
        }
        //--------------------------------------------------------------------------------------------------------------------------------
    }
    //******************************************************************************************************************************************
    public class StopCandleExportImporterMessage : ImporterMessage
    {
        public StopCandleExportImporterMessage(IList<string> candlesSourceKeyList)  // candlesSourceKey := "classCode_secCode_quikTimeFrame"
            : base((byte)ImporterMessageCode.StopCandleExport, CreateMessageParams(candlesSourceKeyList))
        {
        }

        public StopCandleExportImporterMessage(string candlesSourceKey)  // candlesSourceKey := "classCode_secCode_quikTimeFrame"
            : base((byte)ImporterMessageCode.StopCandleExport, candlesSourceKey)
        {
        }
        //--------------------------------------------------------------------------------------------------------------------------------
        private static string CreateMessageParams(IList<string> candleSourcesKeyList) // candleSourcesKey := "classCode_secCode_quikTimeFrame"
        {
            string msgParams = string.Join(QuikCandlesSourceProvider.ObjectsDelimiterSymbol.ToString(), candleSourcesKeyList);
            Debug.WriteLine("msgParams=" + msgParams);
            return msgParams;
        }
        //--------------------------------------------------------------------------------------------------------------------------------
    }
    //******************************************************************************************************************************************
    //******************************************************************************************************************************************
    //******************************************************************************************************************************************
    //******************************************************************************************************************************************
}
