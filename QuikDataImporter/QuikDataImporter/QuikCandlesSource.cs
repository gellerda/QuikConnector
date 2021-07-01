using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using FancyCandles;

namespace QuikDataImporter
{
    internal class QCandlesSource : ObservableCollection<ICandle>, ICandlesSourceFromProvider, IResourceWithUserCounter
    {
        internal QCandlesSource(string secID, TimeFrame timeFrame, Action<string> OnNoMoreUsersAction)
        {
            UserCount = 0;
            this.secID = secID;
            this.timeFrame = timeFrame;
            this.OnNoMoreUersAction = OnNoMoreUsersAction;
        }
        //---------------------------------------------------------------------------------------------------------------------------------------
        private Action<string> OnNoMoreUersAction;
        //---------------------------------------------------------------------------------------------------------------------------------------
        private readonly TimeFrame timeFrame;
        public TimeFrame TimeFrame
        {
            get { return timeFrame; }
        }
        //---------------------------------------------------------------------------------------------------------------------------------------
        private readonly string secID;
        public string SecID
        {
            get { return secID; }
        }
        //---------------------------------------------------------------------------------------------------------------------------------------
        public int UserCount { get; private set; }

        public void IncreaseUserCount()
        {
            UserCount++;
        }

        public void DecreaseUserCount()
        {
            UserCount--;

            if (UserCount <= 0)
                OnNoMoreUersAction($"{SecID}_{(int)ConvertToQuikTimeFrame.FromTimeFrame(timeFrame)}");
        }
        //---------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------
        //---------------------------------------------------------------------------------------------------------------------------------------
    }
}
