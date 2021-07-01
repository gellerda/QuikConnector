using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Collections.ObjectModel;
using System.Windows;
using System.Diagnostics; // Debug.WriteLine("Error...");
using System.Windows.Threading; // Подключены ссылки на WindowsBase.dll и PresentationFramework.dll только ради использования Application.Current.Dispatcher. В class library обычно они не используются.
using FancyCandles;
using System.Windows.Threading;

namespace QuikDataImporter
{
    public class QuikCandlesSourceProvider : ICandlesSourceProvider
    {
        public IList<ISecurityInfo> SecCatalog { get; private set; }
        private Queue<ImporterMessage> importerMessagesToSend = new Queue<ImporterMessage>();
        //---------------------------------------------------------------------------------------------------------------------------------
        public QuikCandlesSourceProvider()
        {
            SecCatalog = new List<ISecurityInfo>();
            IsImportRunning = false;

            StartImport();
        }

        ~QuikCandlesSourceProvider()
        {
            StopImport();
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        public IList<TimeFrame> SupportedTimeFrames
        {
            get { return new List<TimeFrame>() { TimeFrame.M1, TimeFrame.M2, TimeFrame.M3, TimeFrame.M5, TimeFrame.M10, TimeFrame.M15, TimeFrame.M20, TimeFrame.M30, TimeFrame.H1, TimeFrame.H2, TimeFrame.H4, TimeFrame.Daily, TimeFrame.Weekly, TimeFrame.Monthly }; }
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        public void StartImport()
        {
            // Создаст, или подключится к уже созданной разделяемой памяти с заданным именем:
            memoryMappedFile_ImporterMessages = MemoryMappedFile.CreateOrOpen(MemoryMappedFileName_ImporterMessages, MemoryMappedFileSize_ImporterMessages, MemoryMappedFileAccess.ReadWrite);
            memoryMappedFile_QuikData = MemoryMappedFile.CreateOrOpen(MemoryMappedFileName_QuikData, MemoryMappedFileSize_QuikData, MemoryMappedFileAccess.ReadWrite);

            // Если ядро уже содержит Event с таким именем, то первый параметр игнорируется. Иначе, false = "NonSignaled" (т.е. заблокирован).
            event_ImporterMessageHasBeenSent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName_ImporterMessageHasBeenSent);
            event_ImporterMessageHasBeenSent.Reset();
            event_ImporterMessageHasBeenReceived = new EventWaitHandle(true, EventResetMode.ManualReset, EventName_ImporterMessageHasBeenReceived);

            event_QuikDataHasBeenSent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName_QuikDataHasBeenSent);
            event_QuikDataHasBeenReceived = new EventWaitHandle(true, EventResetMode.ManualReset, EventName_QuikDataHasBeenReceived);
            event_QuikDataHasBeenReceived.Set();

            IsImportRunning = true;

            //Потоки для отправки сообщений и получения данных: 
            importerMessagesThread = new Thread(new ThreadStart(ImporterMessagesThreadProc));
            importerMessagesThread.IsBackground = true; // По умолчанию поток будет работать в режиме Foreground.
            importerMessagesThread.Start();

            quikDataThread = new Thread(new ThreadStart(QuikDataThreadProc));
            quikDataThread.IsBackground = true; // По умолчанию поток будет работать в режиме Foreground.
            quikDataThread.Start();

            ImporterMessage zeroImporterMessage = new ZeroImporterMessage();
            SendImporterMessage(zeroImporterMessage);

            ImporterMessage sendSecCatalogimporterMessage = new SendSecCatalogImporterMessage();
            SendImporterMessage(sendSecCatalogimporterMessage);
        }

        private void ImporterMessagesThreadProc()
        {
            while (IsImportRunning)
            {
                if (importerMessagesToSend.Count > 0)
                {
                    bool msgHasBeenReceived = event_ImporterMessageHasBeenReceived.WaitOne(1000);

                    if (msgHasBeenReceived && IsImportRunning)
                    {
                        event_ImporterMessageHasBeenReceived.Reset();

                        ImporterMessage importerMsg = importerMessagesToSend.Dequeue();
                        WriteImporterMessageToFileViewAndFlush(importerMsg);

                        event_ImporterMessageHasBeenSent.Set();
                    }
                }
            }
            Debug.WriteLine("End of messaging.");
        }

        private void QuikDataThreadProc()
        {
            while (IsImportRunning)
            {
                bool qDataHasBeenSent = event_QuikDataHasBeenSent.WaitOne(1000);

                if (qDataHasBeenSent)
                {
                    QuikData qData = ReadQuikDataFromFileView();
                    Debug.WriteLine($"QuikData=({qData.dataCode}, {qData.dataString})");

                    if (SecCatalog.Count > 0 || qData.dataCode == (byte)QuikDataCode.SecCatalog)
                    {
                        if (qData.dataCode == (byte)QuikDataCode.SecCatalog)
                            SecCatalog = QuikData.ProcessSecCatalog(qData.dataString);
                        else if (qData.dataCode == (byte)QuikDataCode.Candles)
                            QuikData.ProcessCandles(qData.dataString, candlesSourcesWithUserCounter);
                        else if (qData.dataCode == (byte)QuikDataCode.DataZero)
                        {
                            importerMessagesToSend.Clear();

                            ImporterMessage sendSecCatalogimporterMessage = new SendSecCatalogImporterMessage();
                            SendImporterMessage(sendSecCatalogimporterMessage);

                            ImporterMessage startCandleExportimporterMessage = new StartCandleExportImporterMessage(candlesSourcesWithUserCounter.Keys.ToList());
                            SendImporterMessage(startCandleExportimporterMessage);
                        }
                    }

                    event_QuikDataHasBeenReceived.Set();
                }
            }
            Debug.WriteLine("End of data exporting.");
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        public void StopImport()
        {
            IsImportRunning = false;

            memoryMappedFile_ImporterMessages.Dispose();
            memoryMappedFile_QuikData.Dispose();
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        public ISecurityInfo GetSecFromCatalog(string secID)
        {
            foreach (ISecurityInfo secInfo in SecCatalog)
            {
                if (secInfo.SecID == secID)
                    return secInfo;
            }
            throw new ArgumentException($"There is no security with SecID={secID} in SecCatalog.");
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        private Dictionary<string, QCandlesSource> candlesSourcesWithUserCounter = new Dictionary<string, QCandlesSource>(); //Ключ - строка в формате "classCode_secCode_quikTimeFrame", где "classCode_secCode"=ISecurityInfo.SecID

        public ICandlesSourceFromProvider GetCandlesSource(string secID, TimeFrame timeFrame)
        {
            QuikTimeFrame quikTimeFrame = ConvertToQuikTimeFrame.FromTimeFrame(timeFrame);
            string candlesSources_key = $"{secID}_{(int)quikTimeFrame}";

            if (!candlesSourcesWithUserCounter.ContainsKey(candlesSources_key))
            {
                QCandlesSource candlesSourceWithUserCounter = new QCandlesSource(secID, timeFrame, EndCandlesSourceUsing);
                candlesSourcesWithUserCounter.Add(candlesSources_key, candlesSourceWithUserCounter);

                ImporterMessage importerMessageToSend = new StartCandleExportImporterMessage(candlesSources_key);
                SendImporterMessage(importerMessageToSend);

                return candlesSourceWithUserCounter;
            }
            else
                return candlesSourcesWithUserCounter[candlesSources_key];
        }

        public void EndCandlesSourceUsing(string candlesSources_key)
        {
            if (!candlesSourcesWithUserCounter.ContainsKey(candlesSources_key))
                throw new ArgumentException($"Candle collection with key={candlesSources_key} is not being used.");

            ImporterMessage importerMessageToSend = new StopCandleExportImporterMessage(candlesSources_key);
            SendImporterMessage(importerMessageToSend);

            candlesSourcesWithUserCounter.Remove(candlesSources_key);
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        public void SendImporterMessage(ImporterMessage importerMessage)
        {
            importerMessagesToSend.Enqueue(importerMessage);
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        public static readonly char ObjectsDelimiterSymbol = '*';
        public static readonly char ParamsDelimiterSymbol = ';';
        //---------------------------------------------------------------------------------------------------------------------------------
        // Разделяемая память.

        public static readonly string MemoryMappedFileName_QuikData = "LuaFileMapping_QuikData"; // Имя для выделенной памяти
        public static readonly string MemoryMappedFileName_ImporterMessages = "LuaFileMapping_ImporterMessages"; // Имя для выделенной памяти

        MemoryMappedFile memoryMappedFile_QuikData;
        MemoryMappedFile memoryMappedFile_ImporterMessages;

        public static readonly uint MemoryMappedFileSize_QuikData = 8000000; // DWORD in the C language
        public static readonly uint MemoryMappedFileSize_ImporterMessages = 30000;
        private char[] readQuikDataBuffer = new char[MemoryMappedFileSize_QuikData];
        //---------------------------------------------------------------------------------------------------------------------------------
        //Именованные Event с автосбросом для импорта/экспорта из Квика:

        public static readonly string EventName_QuikDataHasBeenSent = "LuaEvent_QuikDataHasBeenSent";
        public static readonly string EventName_QuikDataHasBeenReceived = "LuaEvent_QuikDataHasBeenReceived";
        public static readonly string EventName_ImporterMessageHasBeenSent = "LuaEvent_ImporterMessageHasBeenSent";
        public static readonly string EventName_ImporterMessageHasBeenReceived = "LuaEvent_ImporterMessageHasBeenReceived";

        EventWaitHandle event_QuikDataHasBeenSent;
        EventWaitHandle event_QuikDataHasBeenReceived;
        EventWaitHandle event_ImporterMessageHasBeenSent;
        EventWaitHandle event_ImporterMessageHasBeenReceived;
        //---------------------------------------------------------------------------------------------------------------------------------
        public bool IsImportRunning { get; private set; }

        private Thread importerMessagesThread;
        private Thread quikDataThread;
        //---------------------------------------------------------------------------------------------------------------------------------
        private void WriteImporterMessageToFileViewAndFlush(ImporterMessage importerMsg)
        {
            using (BinaryWriter binaryWriter = new BinaryWriter(memoryMappedFile_ImporterMessages.CreateViewStream()))
            {
                //binaryWriter.BaseStream.Seek(0, SeekOrigin.Begin);
                binaryWriter.Write(importerMsg.MessageCode);
                binaryWriter.Write(importerMsg.MessageParams.Length);
                binaryWriter.Flush(); // Очищает все буферы для streamWriterFileView и вызывает запись всех данных буфера в основной поток
            }

            using (StreamWriter streamWriter = new StreamWriter(memoryMappedFile_ImporterMessages.CreateViewStream(), System.Text.Encoding.Default))
            {
                streamWriter.BaseStream.Seek(5, SeekOrigin.Begin);
                streamWriter.Write(importerMsg.MessageParams);
                streamWriter.Flush(); // Очищает все буферы для streamWriterFileView и вызывает запись всех данных буфера в основной поток
            }
        }

        private QuikData ReadQuikDataFromFileView()
        {
            byte quikDataCode;
            int quikDataStringLength;
            string quikDataString;

            using (BinaryReader binaryReader = new BinaryReader(memoryMappedFile_QuikData.CreateViewStream()))
            {
                quikDataCode = binaryReader.ReadByte();
                quikDataStringLength = binaryReader.ReadInt32();
            }

            using (StreamReader streamReader = new StreamReader(memoryMappedFile_QuikData.CreateViewStream(), System.Text.Encoding.Default))
            {
                streamReader.Read(readQuikDataBuffer, 0, quikDataStringLength + 5);
                quikDataString = new string(readQuikDataBuffer, 5, quikDataStringLength);
            }

            return new QuikData(quikDataCode, quikDataString);
        }
        //---------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------
    }
}
