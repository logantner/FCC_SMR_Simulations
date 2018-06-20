using KffSimulations.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace KffAuctionAnalysis.ViewModels
{
    public class LoadParametersViewModel : ViewModelBase, IDisposable
    {
        private ICommand searchCommand;
        private ICommand loadCommand;
        private ICommand cancelCommand;
        private auction[] auctionSets;
        private kffg_simulations2Context db;

        public LoadParametersViewModel(kffg_simulations2Context db)
        {
            this.db = db;
        }

        public int AuctionId { get; set; }
        public auction SelectedAuction { get; set; }
        public Action<auction> Load { get; set; }
        public Action Cancel { get; set; }

        public auction[] AuctionSets
        {
            get { return auctionSets; }
            set
            {
                if (auctionSets == value) return;
                auctionSets = value;
                OnPropertyChanged("AuctionSets");
            }
        }

        public ICommand SearchCommand
        {
            get
            {
                if (searchCommand == null)
                    searchCommand = new RelayCommand(a => SearchAuction());
                return searchCommand;
            }
        }

        public ICommand LoadCommand
        {
            get
            {
                if (loadCommand == null)
                    loadCommand = new RelayCommand(a => RaiseLoad());
                return loadCommand;
            }
        }

        public ICommand CancelCommand
        {
            get
            {
                if (cancelCommand == null)
                    cancelCommand = new RelayCommand(a => RaiseCancel());
                return cancelCommand;
            }
        }

        private void SearchAuction()
        {
            auction[] auctions = db.auctions.Where(a => a.id == AuctionId).ToArray();
            AuctionSets = auctions;
        }

        private void RaiseLoad()
        {
            if (SelectedAuction == null) return;
            Load?.Invoke(SelectedAuction);
        }

        private void RaiseCancel()
        {
            Action a = Cancel;
            if (a != null)
                a();
        }

        public void Dispose()
        {
            db.Dispose();
        }
    }
}
