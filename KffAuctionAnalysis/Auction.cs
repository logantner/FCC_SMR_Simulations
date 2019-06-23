using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using KffSimulations.Models;
using KffSimulations.AuctionModels;
using KffAuctionAnalysis.ViewModels;
using KffSimulations.AuctionModels.Exceptions;
using KffSimulations.FCCModels;

namespace KffAuctionAnalysis
{
    class Auction
    {
        #region FIELDS
        private Random rand;
        private auction auctionModel;
        private SmrForwardAuction forwardAuction;
        private List<AbstractBidder> bidders;
        private Dictionary<string, clock_item> keyToPEA;
        private AuctionStatus auctionStatus;
        private AuctionStatusViewModel auctionStatusVM;

        private int numBackupBid;
        private int numBackupBid2;
        private int firstBackupRound;
        private int firstBackup2Round;
        #endregion FIELDS

        public Auction(
            auction a,
            Dictionary<string, clock_item> keyToPEA,
            List<bidder> bidders,
            IEnumerable<bidder_assigned_strategy> strategies,
            parameter p,
            AuctionStatusViewModel auctionVM)
        {
            rand = new Random(a.seed);
            this.keyToPEA = keyToPEA;
            auctionModel = a;
            List<AuctionItem> items = CreateAuctionItems(keyToPEA.Values);
            this.bidders = CreateBidders(bidders, p.activity_requirement, strategies, items);
            forwardAuction = new SmrForwardAuction(this.bidders.ToArray(), items, p, rand);
            auctionStatus = new AuctionStatus();
            auctionStatusVM = auctionVM;
        }

        private List<AbstractBidder> CreateBidders(List<bidder> bidders, double activityReq, IEnumerable<bidder_assigned_strategy> bidStrats, List<AuctionItem> items)
        {
            Dictionary<int, int> idToStrat = bidStrats.ToDictionary(b => b.bidder_idx, b => b.bidder_strategy_id);
            Dictionary<int, bidder_strategy> strategies;
            using (var db = new kffg_simulations2Context())
            {
                strategies = db.bidder_strategy.ToDictionary(s => s.id, s => s);
            }

            List<AbstractBidder> robotBidders = new List<AbstractBidder>();
            foreach (bidder b in bidders)
            {
                string lpSolvePath = AppDomain.CurrentDomain.BaseDirectory;
                int maxEligibility = MaxEligibility(b.bidder_value, items);
                bidder_strategy strat = strategies[idToStrat[b.idx]];

                switch (strat.id)
                {
                    case 1:
                        robotBidders.Add(new LexicographicBidder(b, activityReq, maxEligibility));
                        break;
                    case 2:
                        robotBidders.Add(new OptimizedBidderSimple(b, activityReq, lpSolvePath, maxEligibility));
                        break;
                    default:
                        throw new Exception("Invalid strategy id");
                }
            }

            return robotBidders;
        }

        private int MaxEligibility(ICollection<bidder_value> values, List<AuctionItem> items)
        {
            return values.Sum(v => v.clock_item.pea.bidding_units);
        }

        private List<AuctionItem> CreateAuctionItems(IEnumerable<clock_item> clockItems)
        {
            List<AuctionItem> items = new List<AuctionItem>();
            foreach (clock_item i in clockItems)
                items.Add(new AuctionItem()
                {
                    clock_item_id = i.id,
                    pea_category_id = i.pea_category_id,
                    auction_id = auctionModel.id,
                    supply = i.supply,
                    clock_item = i
                });

            return items;
        }

        public void RunAuction()
        {
            CreateOutputFiles();

            bool errorRaised = false;
            long totalValue = 0L;
            try
            {
                RunForwardAuction();
                auctionStatusVM.Status = AuctionStatusViewModel.Statuses.CalculatingResults;
                totalValue = GetTotalValue();
            }
            catch (NewStageException)
            {
                Console.WriteLine("New Stage Reached.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error running auction. Message: " + ex.Message);
                errorRaised = true;
            }
            finally
            {
                RecordResults(errorRaised, totalValue);
            }
        }

        private void RunForwardAuction()
        {
            Console.WriteLine("Starting Forward Auction");
            var currentState = SmrForwardAuction.States.RegularRound;
            auctionStatus.IsRunning = true;

            while (auctionStatus.IsRunning)
            {
                if (currentState == SmrForwardAuction.States.RegularRound)
                    RunRound();
                else
                    forwardAuction.HandleIrregularState(currentState);

                currentState = forwardAuction.GetNextState(currentState);
                auctionStatus.IsRunning = (currentState != SmrForwardAuction.States.ConcludeClockPhase);
            }
        }

        //private SmrForwardAuction.States RunRound(SmrForwardAuction.States nextState)
        //{
        //    UpdateAuctionVMState();
        //    auctionStatus.RoundCount++;

        //    Dictionary<string, IProductAuction> initialProducts = forwardAuction.Products.ToDictionary(p => p.product_key, p => (IProductAuction)p.Clone());

        //    List<BidSet> bids = GetRobotBids(forwardAuction.Stage, forwardAuction.Round, forwardAuction.Bidders);

        //    Dictionary<string, int>[] prevProcessedDemand = ExtractBidderProcessedDemand(forwardAuction.Products);

        //    nextState = forwardAuction.ConductRound(bids);
        //    //if (nextState == SmrForwardAuction.States.ConcludeClockPhase)
        //    //    auctionStatus.IsRunning = false;

        //    WriteBidResults(bids, bidders, prevProcessedDemand, initialProducts);

        //    return nextState;
        //}

        private void RunRound()
        {
            UpdateAuctionVMState();
            auctionStatus.RoundCount++;

            Dictionary<string, IProductAuction> initialProducts = forwardAuction.Products.ToDictionary(p => p.product_key, p => (IProductAuction)p.Clone());
            Dictionary<string, int>[] prevProcessedDemand = ExtractBidderProcessedDemand(forwardAuction.Products);

            List<BidSet> bids = GetRobotBids(forwardAuction.Stage, forwardAuction.Round, forwardAuction.Bidders);

            auctionStatusVM.Status = AuctionStatusViewModel.Statuses.ProcessingBids;
            forwardAuction.ConductRound(bids);

            WriteBidResults(bids, bidders, prevProcessedDemand, initialProducts);
        }

        private void UpdateAuctionVMState()
        {
            auctionStatusVM.Stage = forwardAuction.Stage;
            auctionStatusVM.Round = forwardAuction.Round;
            auctionStatusVM.IsFSRMet = forwardAuction.isFinalStage;
        }

        private Dictionary<string, int>[] ExtractBidderProcessedDemand(IEnumerable<IProductAuction> products)
        {
            Dictionary<string, int>[] bidderProcessedDemands = new Dictionary<string, int>[bidders.Count];
            foreach (AbstractBidder b in bidders)
            {
                bidderProcessedDemands[b.Idx] = new Dictionary<string, int>();
                foreach (IProductAuction p in forwardAuction.Products)
                {
                    int demand = p.Demand(b.Idx);
                    if (demand > 0)
                        bidderProcessedDemands[b.Idx].Add(p.product_key, demand);
                }
            }
            return bidderProcessedDemands;
        }

        /// <summary> Queries each robot for a bid. </summary>
        private List<BidSet> GetRobotBids(int stage, int round, IEnumerable<AbstractBidder> robotBidders)
        {
            auctionStatusVM.Status = AuctionStatusViewModel.Statuses.GatheringBids;

            List<BidSet> bids = new List<BidSet>();
            foreach (RobotBidder b in robotBidders)
            {
                //if (b.Idx == 3)
                //    Console.WriteLine("klfhsdkfh");

                SmrRoundResult bidderResult = new SmrRoundResult(b.Idx, forwardAuction.ProductAuctions);
                RobotBidSet bs = (RobotBidSet)b.GetBid(stage, round, bidderResult);
                bids.Add(bs);

                UpdateBackupData(round, bs);
            }

            return bids;
        }

        private void UpdateBackupData(int round, RobotBidSet bs)
        {
            if (bs.IsBackupBid2)
            {
                numBackupBid2++;
                if (firstBackup2Round == 0)
                    firstBackup2Round = round;
            }
            else if (bs.IsBackupBid)
            {
                numBackupBid++;
                if (firstBackupRound == 0)
                    firstBackupRound = round;
            }
        }

        private long GetTotalValue()
        {
            long totalValue = 0L;
            Dictionary<int, Dictionary<string, int>> usedValues = forwardAuction.Bidders.ToDictionary(b => b.Idx, b => new Dictionary<string, int>());
            foreach (IProductAuction p in forwardAuction.Products)
            {
                foreach (RobotBidder b in forwardAuction.Bidders)
                {
                    int q = p.Demand(b.Idx);
                    if (q > 0)
                    {
                        RobotDemand demand = b.GetRobotDemand(p.product_key);
                        string valueKey = RobotBidder.ParseValueKey(p.product_key);
                        Dictionary<string, int> usedCount = usedValues[b.Idx];

                        if (!usedCount.ContainsKey(valueKey))
                            usedCount.Add(valueKey, 0);

                        for (int i = usedCount[valueKey]; i < usedCount[valueKey] + q; ++i)
                        {
                            long v = i < demand.Values.Length ? demand.Values[i] : 0;
                            if (v == 0)
                                Console.WriteLine("won item with no value");
                            long adjVal = (long)Math.Round(b.AdjustedValue(p, demand, v), 0);
                            long adjPrice = (long)Math.Round(b.AdjustedPrice(p), 0);
                            b.AssignItem(new ItemAssignment()
                            {
                                auction_id = auctionModel.id,
                                clock_item_id = p.Model.clock_item_id,
                                pea_category_id = SmrForwardAuction.CategoryToId(p.category),
                                price = p.PostedPrice,
                                value = v,
                                value_discounted = adjVal,
                                price_discounted = adjPrice,
                                profit = (long)Math.Round(b.CalculateLicenseProfit(p, demand, v))
                            });
                            totalValue += adjVal;
                        }

                        usedCount[valueKey] += q;
                    }
                }
            }

            return totalValue;
        }

        #region FILE_RECORDINGS

        private static void CreateOutputFiles()
        {
            using (TextWriter writer = new StreamWriter("Bids.csv"))
            {
                writer.WriteLine("Round,Bidder,PEA,Category,BidType,Quantity,Price,Switch To Category,PrevProcessedDemand,CurrentProcessedDemand,PostedPrice,ClockPrice,ExcessDemand,FinalPrice,FinalClockPrice,FinalExcessDemand,Budget,TotalExposure");
            }
            using (TextWriter writer = new StreamWriter("BidderResults.csv"))
            {
                writer.WriteLine("Simulation,Auction,Bidder,Budget,TotalWinningValue,TotalWinningValueDiscounted,TotalWinningCost,TotalWinningCostAdjusted,TotalProfit,FirstBackupRound,FirstBackup2Round,NumBackups,NumBackup2s");
            }
        }

        /// <summary>
        /// Headers: "Round,Bidder,PEA,Category,BidType,Quantity,Price,Switch To Category,PrevProcessedDemand,CurrentProcessedDemand,PostedPrice,ClockPrice,ExcessDemand,FinalPrice,FinalClockPrice,FinalExcessDemand"
        /// </summary>
        private void WriteBidResults(List<BidSet> bids, List<AbstractBidder> bidders, Dictionary<string, int>[] prevBidderDemand, Dictionary<string, IProductAuction> initProducts)
        {
            Dictionary<string, int>[] currentDemands = ExtractBidderProcessedDemand(forwardAuction.Products);
            Dictionary<int, AbstractBidder> idxToBidder = bidders.ToDictionary(b => b.Idx, b => b);

            using (TextWriter writer = new StreamWriter("Bids.csv", true))
            {
                foreach (RobotBidSet b in bids)
                {
                    Dictionary<string, int> desiredQuantity = new Dictionary<string, int>();
                    foreach (BidderUploadRecord d in b.Demand)
                    {
                        IProductAuction product = forwardAuction.GetProduct(d.product_key);
                        desiredQuantity.Add(d.product_key, d.quantity);

                        StringBuilder sb = new StringBuilder();
                        sb.Append(forwardAuction.Round - 1).Append(",");
                        sb.Append(b.BidderIdx).Append(",");
                        sb.Append(d.product_key.Replace('|', ',')).Append(",");
                        sb.Append(d.bid_type).Append(",");
                        sb.Append(d.quantity).Append(",");
                        sb.Append(d.bid_amount).Append(",");
                        sb.Append(d.switch_to_category).Append(",");
                        sb.Append(prevBidderDemand[b.BidderIdx].ContainsKey(d.product_key) ? prevBidderDemand[b.BidderIdx][d.product_key] : 0).Append(",");
                        sb.Append(currentDemands[b.BidderIdx].ContainsKey(d.product_key) ? currentDemands[b.BidderIdx][d.product_key] : 0).Append(",");

                        if (initProducts.ContainsKey(product.product_key))
                        {
                            IProductAuction ip = initProducts[product.product_key];
                            sb.Append(ip.PostedPrice).Append(",");
                            sb.Append(ip.ClockPrice).Append(",");
                            sb.Append(Math.Max(ip.AggregateDemand - ip.supply, 0)).Append(",");
                        }
                        else
                            sb.Append("0,0,0,");

                        sb.Append(product.PostedPrice).Append(",");
                        sb.Append(product.ClockPrice).Append(",");
                        sb.Append(Math.Max(product.AggregateDemand - product.supply, 0)).Append(",");
                        sb.Append(((RobotBidder)bidders[b.BidderIdx]).budget).Append(",");
                        sb.Append(forwardAuction.Products.Sum(p => p.Demand(b.BidderIdx) * p.PostedPrice));

                        if (d is RobotBid)
                        {
                            RobotBid rb = d as RobotBid;
                            sb.Append(",").Append(string.Join("; ", rb.Values));
                        }

                        writer.WriteLine(sb.ToString());
                    }

                    // create implied bids
                    foreach (string key in prevBidderDemand[b.BidderIdx].Keys)
                    {
                        if (!desiredQuantity.ContainsKey(key))
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append(forwardAuction.Round).Append(",");
                            sb.Append(b.BidderIdx).Append(",");
                            sb.Append(key.Replace('|', ',')).Append(",");
                            sb.Append(BidderUploadRecord.BidTypes.Simple).Append(",");
                            sb.Append("0,0,,");
                            sb.Append(prevBidderDemand[b.BidderIdx][key]).Append(",");
                            sb.Append(currentDemands[b.BidderIdx].ContainsKey(key) ? currentDemands[b.BidderIdx][key] : 0);
                        }
                    }
                }
            }
        }

        private void RecordResults(bool errorCaught, long totalValue)
        {
            WriteRoundResult(errorCaught);
            
            if (forwardAuction.isFinalStage && !errorCaught)
                WriteBidderResults();
        }

        private void WriteRoundResult(bool errorCaught)
        {
            using (TextWriter writer = new StreamWriter("Results.csv"))
            {
                writer.WriteLine("Auction ID,Rounds,Error Raised,FSR Satisfied,Revenue,Sum Winning Value,Num Backup Bids,Num Backup2 Bids,First Backup Round,First Backup2 Round");
                StringBuilder sb = new StringBuilder();
                sb.Append(auctionModel.id).Append(",");
                sb.Append(auctionStatus.RoundCount).Append(",");
                sb.Append(errorCaught).Append(",");
                sb.Append(forwardAuction.isFinalStage).Append(",");
                sb.Append(forwardAuction.Bidders.Sum(b => ((RobotBidder)b).Assignments.Sum(a => a.price_discounted))).Append(",");
                sb.Append(forwardAuction.Bidders.Sum(b => ((RobotBidder)b).Assignments.Sum(a => a.value_discounted))).Append(",");
                sb.Append(numBackupBid).Append(",");
                sb.Append(numBackupBid2).Append(",");
                sb.Append(firstBackupRound).Append(",");
                sb.Append(firstBackup2Round).Append(",");
                writer.WriteLine(sb.ToString());
            }
        }

        private void WriteBidderResults()
        {
            using (TextWriter writer = new StreamWriter("BidderResults.csv", true))
            {
                foreach (RobotBidder b in forwardAuction.Bidders)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(auctionModel.simulation_id).Append(",");
                    sb.Append(auctionModel.id).Append(",");
                    sb.Append(b.Idx).Append(",");
                    sb.Append(b.budget).Append(",");
                    sb.Append(b.Assignments.Sum(a => a.value)).Append(",");
                    sb.Append(b.Assignments.Sum(a => a.value_discounted)).Append(",");
                    sb.Append(b.Assignments.Sum(a => a.price)).Append(",");
                    sb.Append(b.Assignments.Sum(a => a.price_discounted)).Append(",");
                    sb.Append(b.Assignments.Sum(a => a.profit)).Append(",");
                    sb.Append(b.FirstBackupRound).Append(",");
                    sb.Append(b.FirstBackup2Round).Append(",");
                    sb.Append(b.NumBackupBids).Append(",");
                    sb.Append(b.NumBackup2Bids).Append(",");
                    writer.WriteLine(sb.ToString());
                }
            }
        }

        #endregion FILE_RECORDINGS

    }
}
