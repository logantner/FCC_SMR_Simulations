using KffAuctionAnalysis.ViewModels;
using KffAuctionAnalysis.Views;
using KffSimulations.AuctionModels;
using KffSimulations.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace KffAuctionAnalysis
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private AuctionStatusViewModel statusVM;
        private LoadParametersViewModel loadParametersVM;
        private auction selectedAuction;
        

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        #region Methods

        private void HandleLoadAuction(auction a)
        {
            selectedAuction = a;
            loadParametersVM.Dispose();
            ClosePage();

            LoadButton.IsEnabled = false;
            StartButton.IsEnabled = true;
        }

        private void CancelLoadParameters()
        {
            

            loadParametersVM.Dispose();
            ClosePage();
        }

        private void SetupLoadAuction()
        {
            try
            {
                loadParametersVM = new LoadParametersViewModel(new kffg_simulations2Context());
                loadParametersVM.Load += HandleLoadAuction;
                loadParametersVM.Cancel += CancelLoadParameters;

                LoadAuctionView v = new LoadAuctionView();
                v.DataContext = loadParametersVM;

                ChangeGridControl(v);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                if (ex.InnerException != null)
                    MessageBox.Show(ex.InnerException.Message);
            }
        }

        private void ClosePage()
        {
            MainGrid.Children.Clear();
        }

        private void ChangeGridControl(UIElement c)
        {
            MainGrid.Children.Clear();
            MainGrid.Children.Add(c);
        }

        private void StartAuction()
        {
            SetupAuctionStatus();
            auctionWorker = new BackgroundWorker();
            auctionWorker.DoWork += RunAuction;
            auctionWorker.RunWorkerCompleted += AuctionComplete;

            auctionWorker.RunWorkerAsync(CreateAuction(selectedAuction));
        }

        private Auction CreateAuction(auction a)
        {
            using (kffg_simulations2Context db = new kffg_simulations2Context())
            {
                simulation simModel = db.simulations.First(s => s.id == a.simulation_id);
                parameter parameters = db.parameters.First(p => p.id == simModel.parameters_id);
                Dictionary<string, clock_item> keyToPEA = db.clock_item.Where(ci => ci.pea_group_id == parameters.pea_group_id && ci.item_set_id == parameters.item_set_id)
                                                            .ToList()
                                                            .ToDictionary(ci => ForwardAuction.ProductKey(ci), ci => ci);

                List<bidder> bidders = db.bidders.Where(b => b.simulation_id == a.simulation_id).ToList();
                IEnumerable<bidder_assigned_strategy> strats = db.bidder_assigned_strategy.Where(s => s.auction_id == a.id);

                statusVM = new AuctionStatusViewModel();
                return new Auction(a, keyToPEA, bidders, strats, parameters, statusVM);
            }
        }

        private BackgroundWorker auctionWorker;
        private void RunAuction(object sender, DoWorkEventArgs e)
        {
            Auction auction = (Auction)e.Argument;
            auction.RunAuction();
            e.Result = statusVM.Revenue;
        }

        private void AuctionComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("Auction Complete");
        }

        private void SetupAuctionStatus()
        {
            statusVM = new AuctionStatusViewModel();
            StatusView v = new StatusView()
            {
                DataContext = statusVM
            };
            ChangeGridControl(v);
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            SetupLoadAuction();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            StartAuction();
        }

        #endregion Methods

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            this.VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                PropertyChangedEventArgs e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        #endregion //INotifyPropertyChanged
        
        #region Debugging Aides

        /// <summary>
        /// Warns the developer if this object does not have
        /// a public property with the specified name. This 
        /// method does not exist in a Release build.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public void VerifyPropertyName(string propertyName)
        {
            // Verify that the property name matches a real,  
            // public, instance property on this object.
            if (TypeDescriptor.GetProperties(this)[propertyName] == null)
            {
                string msg = "Invalid property name: " + propertyName;

                if (this.ThrowOnInvalidPropertyName)
                    throw new Exception(msg);
                else
                    Debug.Fail(msg);
            }
        }

        /// <summary>
        /// Returns whether an exception is thrown, or if a Debug.Fail() is used
        /// when an invalid property name is passed to the VerifyPropertyName method.
        /// The default value is false, but subclasses used by unit tests might 
        /// override this property's getter to return true.
        /// </summary>
        protected virtual bool ThrowOnInvalidPropertyName { get; private set; }

#endregion // Debugging Aides
    }
}
