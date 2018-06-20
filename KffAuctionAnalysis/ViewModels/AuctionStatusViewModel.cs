using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using KffSimulations.Models;
using KffSimulations.Views;

namespace KffAuctionAnalysis.ViewModels
{
    public class AuctionStatusViewModel : ViewModelBase
    {
        private Statuses status;
        private int stage;
        private int round;
        private long revenue;
        private bool isFSRMet;

        public AuctionStatusViewModel()
        {
            status = Statuses.Initializing;
            stage = 1;
            round = 1;
            revenue = 0;
            isFSRMet = false;
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

        public int Stage
        {
            get { return stage; }
            set
            {
                if (stage == value) return;
                stage = value;
                OnPropertyChanged("Stage");
            }
        }

        public int Round
        {
            get { return round; }
            set
            {
                if (round == value) return;
                round = value;
                OnPropertyChanged("Round");
            }
        }

        public long Revenue
        {
            get { return revenue; }
            set
            {
                if (revenue == value) return;
                revenue = value;
                OnPropertyChanged("Revenue");
            }
        }

        public bool IsFSRMet
        {
            get { return isFSRMet; }
            set
            {
                if (isFSRMet == value) return;
                isFSRMet = value;
                OnPropertyChanged("IsFSRMet");
            }
        }

        public enum Statuses
        {
            Initializing,
            GatheringBids,
            ProcessingBids,
            CalculatingResults
        }
    }
}
