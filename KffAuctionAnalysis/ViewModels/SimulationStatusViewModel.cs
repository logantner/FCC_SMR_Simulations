using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KffAuctionAnalysis.ViewModels
{
    public class SimulationStatusViewModel : ViewModelBase
    {
        private Statuses status;
        private int auctionNumber;
        private int numberOfAuctions;

        public SimulationStatusViewModel(int numberOfAuctions)
        {
            auctionNumber = 0;
            this.numberOfAuctions = numberOfAuctions;
        }

        public Statuses Status
        {
            get { return status; }
            set
            {
                if (status == value) return;
                status = value;
                OnPropertyChanged("Status");
            }
        }

        public int AuctionNumber
        {
            get { return auctionNumber; }
            set
            {
                if (auctionNumber == value) return;
                auctionNumber = value;
                OnPropertyChanged("AuctionNumber");
            }
        }

        public int NumberOfAuctions
        {
            get { return numberOfAuctions; }
        }

        public enum Statuses
        {
            Initializing,
            ReadyToStart
        }
    }
}
