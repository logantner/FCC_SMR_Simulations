using System.Collections.Generic;

namespace KffAuctionAnalysis
{
    public class AuctionStatus
    {

        public AuctionStatus()
        {
            IsRunning = false;
            RoundCount = 0;
            Status = Statuses.Initializing;
        }
        
        public bool IsRunning { get; set; }
        public int RoundCount { get; set; }
        public Statuses Status { get; set; }

        public enum Statuses
        {
            Initializing,
            GatheringBids,
            ProcessingBids,
            CalculatingResults
        }
    }
}