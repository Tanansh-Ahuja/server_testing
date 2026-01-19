using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices; // For AggressiveInlining
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tt_net_sdk;
using System.Globalization; // Add this namespace at the top

namespace ADL_Automation
{
    public partial class MainForm : Form
    {
        private string _appSecretKey;
        private LoadingLabel _loadingLabel = null;

        // --- SHARED DATA ---
        // Accessed by UI Thread (Read) and Simulator Thread (Write)
        // Volatile removed from double as discussed (atomic on x64, safe enough for UI display)
        public static bool _neonFeedConnected = false;
        public static double _liveNeonValue = double.NaN;

        private tt_net_sdk.Dispatcher m_disp = null;
        private TTAPI m_api = null;
        private FileHandlers _fileHandlers = null;

        private InstrumentLookup m_instrLookupRequest = null;

        // --- STATIC HOT PATH OBJECTS ---
        private static TradeSubscription m_TradeSubscription = null;
        private PriceSubscription m_priceSubscription = null;
        private IReadOnlyCollection<Account> m_accounts = null;
        public static Instrument _instrument = null;

        private string _accountName = String.Empty;
        static Account _orderAccount = null;
        private static bool _orderbookSynced = false;
        private static bool _instrumentLookedup = false;

        // Instrument Details
        private readonly string m_market = "EUREX";
        private readonly string m_product = "FCEU";
        private readonly string m_prodType = "Future";
        private readonly string m_alias = "FCEU Mar26";



        // Dummy Order Flags
        private static bool _dummyOrderSent = false;
        private static bool _dummyOrderAdded = false;
        private static bool _dummyOrderUpdated = false;
        private static bool _dummyOrderCycleCompleted = false;
        private static Price _dummyOrderUpdatePrice = Price.Invalid;
        private static OrderProfile dummyOrderProfile = null;
        private static string _dummyOrderKey = String.Empty;

        // Hot Path Profiles
        private static OrderProfile BuyOrderProfile = null;
        private static OrderProfile SellOrderProfile = null;
        public static string _buyOrderKey = string.Empty;
        public static string _sellOrderKey = string.Empty;

        // Flags (Volatile for thread safety on bools)
        public static volatile bool _buyOrderLiveInMarket = false;
        public static volatile bool _sellOrderLiveInMarket = false;

        // --- OPTIMIZATION: Hardware Math Variables ---
        // We store these as doubles to avoid slow Decimal math in the hot path
        public static double _conversionFactorDouble = 0;
        public static double _ticksizeDouble = 0;
        public static double _noOfTicksDouble = 0;

        // Keep decimals for UI/Setup only
        public static decimal _conversionFactor = decimal.MinValue;
        public static int _noOfTicks = int.MaxValue;
        public static decimal _ticksize = decimal.MaxValue;

        public MainForm(string appSecretKey)
        {
            try
            {
                _appSecretKey = appSecretKey;
                _loadingLabel = new LoadingLabel();
                _fileHandlers = new FileHandlers();
                InitializeComponent();

                RootPanel.Hide();
                StartButton.Hide();
                StopButton.Hide();
            }
            catch
            {
                Dispose();
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                this.Icon = new Icon("Logo/FinalLogo.ico");
                _loadingLabel.InitialiseLoadingLabel("Initialising TT", this, RootPanel);

                // OPTIMIZATION: Process Priority
                using (Process p = Process.GetCurrentProcess())
                {
                    p.PriorityClass = ProcessPriorityClass.High;
                }
            }
            catch (Exception ex)
            {
                HelperFunctions.ShutEverythingDown($"Error occured while loading Form.\n Message: {ex.Message}");
            }
        }

        // --- THE NEW HOT PATH ---
        // This method is called directly by the background Simulator Thread.
        // It calculates math using doubles and fires orders immediately.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OnFastPriceUpdate(double price)
        {
            _liveNeonValue = price;

            if (!_buyOrderLiveInMarket || !_sellOrderLiveInMarket) return;

            // 1. HARDWARE MATH
            double midPoint = _conversionFactorDouble + price;
            double rawBuy = midPoint - (_noOfTicksDouble * _ticksizeDouble);
            double rawSell = midPoint + (_noOfTicksDouble * _ticksizeDouble);

            // 2. ROUNDING LOGIC (Buy = Floor, Sell = Ceiling)
            // Note: We use a tiny epsilon (1e-9) to handle floating point errors 
            // where 100.0 might appear as 99.99999999 due to double precision.
            double flooredBuy = Math.Floor((rawBuy / _ticksizeDouble) + 1e-9) * _ticksizeDouble;
            double ceiledSell = Math.Ceiling((rawSell / _ticksizeDouble) - 1e-9) * _ticksizeDouble;

            // 3. CONVERT TO SDK PRICE
            Price buyPrice = Price.FromDecimal(_instrument, (decimal)flooredBuy);
            Price sellPrice = Price.FromDecimal(_instrument, (decimal)ceiledSell);

            // ... rest of the function (SEND ORDERS) ...
            // BUY SIDE
            if (BuyOrderProfile.LimitPrice != buyPrice)
            {
                BuyOrderProfile.LimitPrice = buyPrice;
                BuyOrderProfile.Action = OrderAction.Change;
                m_TradeSubscription.SendOrder(BuyOrderProfile);
            }

            // SELL SIDE
            if (SellOrderProfile.LimitPrice != sellPrice)
            {
                SellOrderProfile.LimitPrice = sellPrice;
                SellOrderProfile.Action = OrderAction.Change;
                m_TradeSubscription.SendOrder(SellOrderProfile);
            }
        }
        #region Neon Feed

        static void ConnectNeonFeed()
        {
            PriceSimulator.Connect();
        }

        static void StopNeonFeed()
        {
            PriceSimulator.Stop();
        }

        public static void NeonFeedConnected()
        {
            _neonFeedConnected = true;
            if (!_dummyOrderCycleCompleted) return;
            // Trigger UI update on the UI Thread
            if (Globals.loadingLabel != null && Globals.loadingLabel.InvokeRequired)
            {
                // We use BeginInvoke so we don't block the simulator thread
                Globals.loadingLabel.BeginInvoke(new MethodInvoker(UIUpdateToNeonFeedConnected));
            }
            else
            {
                UIUpdateToNeonFeedConnected();
            }
        }

        public static void UIUpdateToNeonFeedConnected()
        {
            if (!_neonFeedConnected) return;

            if (StartButton.InvokeRequired)
                StartButton.Invoke(new MethodInvoker(() => StartButton.Show()));
            else
                StartButton.Show();

            if (FeedStatusLabel.InvokeRequired)
            {
                FeedStatusLabel.Invoke(new MethodInvoker(() => {
                    FeedStatusLabel.BackColor = Color.LightGreen;
                    FeedStatusLabel.Text = "Feed Connected";
                }));
            }
            else
            {
                FeedStatusLabel.BackColor = Color.LightGreen;
                FeedStatusLabel.Text = "Feed Connected";
            }
        }

        #endregion

        #region TT API

        public void ttNetApiInitHandler(TTAPI api, ApiCreationException ex)
        {
            if (ex == null)
            {
                _loadingLabel.ChangeLoadingLabelText("TT.NET SDK INITIALIZED");
                _fileHandlers.SaveApiKey("Key.txt", _appSecretKey);

                m_api = api;
                m_api.OrderbookSynced += OnSyncingOrderBook;
                m_api.TTAPIStatusUpdate += new EventHandler<TTAPIStatusUpdateEventArgs>(m_api_TTAPIStatusUpdate);
                m_api.Start();
            }
            else
            {
                HelperFunctions.ShutEverythingDown($"TT.NET SDK Initialization Failed: {ex.Message}");
            }
        }

        private void OnSyncingOrderBook(object sender, EventArgs e)
        {
            _orderbookSynced = true;
            if (_instrumentLookedup && !_neonFeedConnected)
            {
                Console.WriteLine("orderbook synced..");
                ConnectNeonFeed();
                RootPanel.Show();
                Globals.loadingLabel.Hide();
            }
        }

        public void m_api_TTAPIStatusUpdate(object sender, TTAPIStatusUpdateEventArgs e)
        {
            if (e.IsReady == false) return;

            m_disp = tt_net_sdk.Dispatcher.Current;

            MarketId marketKey = Market.GetMarketIdFromName(m_market);
            ProductType productType = Product.GetProductTypeFromName(m_prodType);

            m_instrLookupRequest = new InstrumentLookup(tt_net_sdk.Dispatcher.Current, marketKey, productType, m_product, m_alias);
            m_instrLookupRequest.OnData += m_instrLookupRequest_OnData;
            m_instrLookupRequest.GetAsync();

            m_accounts = m_api.Accounts;
            _accountName = _fileHandlers.GetAccountName(@"UserEditableFiles\accountName.txt");
            _orderAccount = m_accounts.FirstOrDefault(a => a.AccountName == _accountName);
        }

        private void m_instrLookupRequest_OnData(object sender, InstrumentLookupEventArgs e)
        {
            if (e.Event == ProductDataEvent.Found)
            {
                Console.WriteLine("Instrument found: " );
                _instrument = e.InstrumentLookup.Instrument;
                _instrumentLookedup = true;

                // OPTIMIZATION: Cache TickSize as double immediately
                if (_instrument.InstrumentDetails != null)
                {
                    _ticksizeDouble = (double)_instrument.InstrumentDetails.TickSize;
                    _ticksize = _instrument.InstrumentDetails.TickSize;
                }

                if (_orderbookSynced && !_neonFeedConnected)
                {
                    ConnectNeonFeed();
                    RootPanel.Show();
                    Globals.loadingLabel.Hide();
                }

                // Initialize Profiles
                dummyOrderProfile = new OrderProfile(_instrument)
                {
                    BuySell = BuySell.Buy,
                    Account = _orderAccount,
                    OrderQuantity = Quantity.FromDecimal(_instrument, 1.0m),
                    OrderType = OrderType.Limit,
                    Action = OrderAction.Add,
                };

                BuyOrderProfile = new OrderProfile(_instrument)
                {
                    BuySell = BuySell.Buy,
                    Account = _orderAccount,
                    OrderQuantity = Quantity.FromDecimal(_instrument, 1.0m),
                    OrderType = OrderType.Limit,
                    TimeInForce = TimeInForce.GoodTillCancel
                };

                SellOrderProfile = new OrderProfile(_instrument)
                {
                    BuySell = BuySell.Sell,
                    Account = _orderAccount,
                    OrderQuantity = Quantity.FromDecimal(_instrument, 1.0m),
                    OrderType = OrderType.Limit,
                    TimeInForce = TimeInForce.GoodTillCancel
                };

                m_TradeSubscription = new TradeSubscription(tt_net_sdk.Dispatcher.Current, true);
                m_TradeSubscription.OrderUpdated += m_instrumentTradeSubscription_OrderUpdated;
                m_TradeSubscription.OrderAdded += m_instrumentTradeSubscription_OrderAdded;
                m_TradeSubscription.OrderDeleted += m_instrumentTradeSubscription_OrderDeleted;
                m_TradeSubscription.OrderFilled += m_instrumentTradeSubscription_OrderFilled;
                m_TradeSubscription.Start();

                m_priceSubscription = new PriceSubscription(_instrument, tt_net_sdk.Dispatcher.Current);
                m_priceSubscription.Settings = new PriceSubscriptionSettings(PriceSubscriptionType.InsideMarket);
                m_priceSubscription.FieldsUpdated += m_priceSubscription_FieldsUpdated;
                m_priceSubscription.Start();
            }
            else
            {
                Console.WriteLine($"Instrument Error: {e.Message}");
            }
        }

        // --- DUMMY ORDER CYCLE (Warmup) ---
        private void m_priceSubscription_FieldsUpdated(object sender, FieldsUpdatedEventArgs e)
        {
            if (_dummyOrderSent) return;

            if (e.Error == null && _neonFeedConnected)
            {
                _dummyOrderSent = true;
                Price bestBidPrice = e.Fields.GetBestBidPriceField().Value;
                if (_orderbookSynced && bestBidPrice != null && bestBidPrice.IsValid && bestBidPrice.IsTradable)
                {
                    decimal bidVal = Convert.ToDecimal(bestBidPrice);
                    decimal dummyPrice = bidVal - (50.0m * _ticksize);
                    decimal updatePrice = bidVal - (51.0m * _ticksize);
                    _dummyOrderUpdatePrice = Price.FromDecimal(_instrument, updatePrice);
                    dummyOrderProfile.LimitPrice = Price.FromDecimal(_instrument, dummyPrice);

                    dummyOrderProfile.Action = OrderAction.Add;
                    m_TradeSubscription.SendOrder(dummyOrderProfile);
                    _dummyOrderKey = dummyOrderProfile.SiteOrderKey;
                }
            }
        }

        // --- ORDER HANDLERS ---
        void m_instrumentTradeSubscription_OrderAdded(object sender, OrderAddedEventArgs e)
        {
            if (e.Order.SiteOrderKey == _dummyOrderKey && !_dummyOrderAdded)
            {
                _dummyOrderAdded = true;
                dummyOrderProfile.Action = OrderAction.Change;
                dummyOrderProfile.LimitPrice = _dummyOrderUpdatePrice;
                m_TradeSubscription.SendOrder(dummyOrderProfile);
            }
        }

        void m_instrumentTradeSubscription_OrderUpdated(object sender, OrderUpdatedEventArgs e)
        {
            if (e.OldOrder.SiteOrderKey == _dummyOrderKey && !_dummyOrderUpdated)
            {
                _dummyOrderUpdated = true;
                dummyOrderProfile.Action = OrderAction.Delete;
                m_TradeSubscription.SendOrder(dummyOrderProfile);
            }
        }

        void m_instrumentTradeSubscription_OrderDeleted(object sender, OrderDeletedEventArgs e)
        {
            if (e.OldOrder.SiteOrderKey == _dummyOrderKey && !_dummyOrderCycleCompleted)
            {
                _dummyOrderCycleCompleted = true;

               

                // Safe UI Update
                if (Globals.loadingLabel != null && Globals.loadingLabel.InvokeRequired)
                    Globals.loadingLabel.BeginInvoke(new MethodInvoker(UIUpdateToNeonFeedConnected));
                else
                    UIUpdateToNeonFeedConnected();
            }
            else
            {
                if (e.OldOrder.SiteOrderKey == _buyOrderKey) _buyOrderKey = string.Empty;
                else if (e.OldOrder.SiteOrderKey == _sellOrderKey) _sellOrderKey = string.Empty;
            }
        }

        private void m_instrumentTradeSubscription_OrderFilled(object sender, OrderFilledEventArgs e)
        {
            _buyOrderLiveInMarket = false;
            _sellOrderLiveInMarket = false;

            BuyOrderProfile.Action = OrderAction.Delete;
            m_TradeSubscription.SendOrder(BuyOrderProfile);

            SellOrderProfile.Action = OrderAction.Delete;
            m_TradeSubscription.SendOrder(SellOrderProfile);

            if (StopButton.InvokeRequired)
                StopButton.BeginInvoke(new MethodInvoker(() => StopButton.Hide()));
            else
                StopButton.Hide();
        }

        // --- UI BUTTONS ---
        private void StartButton_Click(object sender, EventArgs e)
        {
            string rawInput = ConversionFactorTextBox.Text.Trim();
            string normalizedInput = rawInput.Replace(",", ".");

            if (!decimal.TryParse(normalizedInput, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal conversionFactor))
            {
                MessageBox.Show($"Invalid Conversion Factor. Input detected: '{rawInput}'");
                return;
            }
            if (!int.TryParse(NoOfTicksTextBox.Text.Trim(), out int noOfTicks) || noOfTicks < 1)
            {
                MessageBox.Show("Invalid No of Ticks"); return;
            }

            // Save State
            _conversionFactor = conversionFactor;
            _noOfTicks = noOfTicks;
            _conversionFactorDouble = (double)conversionFactor;
            _noOfTicksDouble = (double)noOfTicks;

            // --- CALCULATION LOGIC ---

            // 1. Calculate Raw Values (Decimal is precise, no epsilon needed)
            decimal midPoint = _conversionFactor + Convert.ToDecimal(_liveNeonValue);
            decimal rawBuy = midPoint - (_noOfTicks * _ticksize);
            decimal rawSell = midPoint + (_noOfTicks * _ticksize);

            // 2. APPLY ROUNDING (Floor Buy, Ceiling Sell)
            decimal flooredBuy = Math.Floor(rawBuy / _ticksize) * _ticksize;
            decimal ceiledSell = Math.Ceiling(rawSell / _ticksize) * _ticksize;

            // 3. Create Price Objects
            Price buyPrice = Price.FromDecimal(_instrument, flooredBuy);
            Price sellPrice = Price.FromDecimal(_instrument, ceiledSell);

            // --- SEND ORDERS ---

            _buyOrderLiveInMarket = true;
            _sellOrderLiveInMarket = true;

            BuyOrderProfile.Action = OrderAction.Add;
            BuyOrderProfile.LimitPrice = buyPrice;
            m_TradeSubscription.SendOrder(BuyOrderProfile);
            _buyOrderKey = BuyOrderProfile.SiteOrderKey;

            SellOrderProfile.Action = OrderAction.Add;
            SellOrderProfile.LimitPrice = sellPrice;
            m_TradeSubscription.SendOrder(SellOrderProfile);
            _sellOrderKey = SellOrderProfile.SiteOrderKey;

            // Update UI
            NoOfTicksTextBox.Enabled = false;
            ConversionFactorTextBox.Enabled = false;
            StartButton.Hide();
            StopButton.Show();
        }
        private void StopButton_Click(object sender, EventArgs e)
        {
            _buyOrderLiveInMarket = false;
            _sellOrderLiveInMarket = false;

            BuyOrderProfile.Action = OrderAction.Delete;
            m_TradeSubscription.SendOrder(BuyOrderProfile);

            SellOrderProfile.Action = OrderAction.Delete;
            m_TradeSubscription.SendOrder(SellOrderProfile);

            NoOfTicksTextBox.Enabled = true;
            ConversionFactorTextBox.Enabled = true;
            StopButton.Hide();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            BuyOrderProfile.Action = OrderAction.Delete;
            m_TradeSubscription.SendOrder(BuyOrderProfile);

            SellOrderProfile.Action = OrderAction.Delete;
            m_TradeSubscription.SendOrder(SellOrderProfile);

            if (_neonFeedConnected) StopNeonFeed();
            TTAPI.Shutdown();
        }
        #endregion
    }
}