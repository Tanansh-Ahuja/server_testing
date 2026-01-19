#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
    neon_client.py
    Multi-currency focused Neon market data client for full snapshots
    
    This client requests full market data snapshots for multiple currency pairs
    and tests different symbol formats to find what works with your Neon server
"""

from time import sleep, time
from datetime import datetime
import os
import sys
import argparse
import signal
import threading
import json
from marketdata.simplefix_client import SimpleFIXClient
import logging


class MultiCurrencyTickProcessor():
    """
    Multi-currency tick processor for full market data snapshots
    """

    def __init__(self, currency_pairs=None):
        # Set up logger
        start_time = datetime.now()
        timestamp_str = start_time.strftime("%d-%m-%Y:%H-%M-%S")
        log_file_name = f'logs/neon_{timestamp_str}.log'
        os.makedirs('logs', exist_ok=True)
        self.logger = logging.getLogger('neon_logger')
        self.logger.setLevel(logging.INFO)
        file_handler = logging.FileHandler(log_file_name)
        formatter = logging.Formatter('%(asctime)s %(levelname)s %(message)s')
        file_handler.setFormatter(formatter)
        self.logger.addHandler(file_handler)
        self.logger.info("[INFO] Starting Multi-Currency Neon market data client for FULL SNAPSHOTS")
        self.logger.info("[INFO] This client will request full market depth (not just top of book)")
        self.logger.info(f"[INFO] Log file: {log_file_name}")

        self.client = SimpleFIXClient(self, 
                                     config_file='config/neon.conf',
                                     store_all_ticks=True,
                                     save_history_to_files=True,
                                     verbose=False,
                                     message_log_file=log_file_name)
        self.successful_symbols = []
        self.failed_symbols = []
        self.received_snapshots = []
        self.total_tested = 0
        self.currency_positions = {}
        self.minute_marker_positions = {}
        # Store previous bid/ask prices to only send updates when prices change
        self.previous_prices = {}
        # Staleness check attributes
        self.last_update_times = {}  # Track last update time for each symbol
        self.staleness_threshold = 2.0  # 2 seconds staleness threshold
        self.staleness_checker_running = False
        self.staleness_checker_thread = None
        if currency_pairs is None:
            self.currency_pairs = ['EUR/USD']
        else:
            self.currency_pairs = currency_pairs
        if self.client.isLoggedOn():
            self.logger.info("[INFO] Connected to Neon market data feed")
            self.send_multi_currency_full_snapshot_requests()
            # Start staleness checker
            self.start_staleness_checker()
        else:
            self.logger.error("[ERROR] Cannot connect to Neon market data feed")
            self.print_connection_help()

    def send_multi_currency_full_snapshot_requests(self):
        currency_pairs = self.currency_pairs
        self.logger.info(f"[INFO] Testing {len(currency_pairs)} currency pairs for FULL SNAPSHOTS...")
        self.logger.info("[INFO] Each request will ask for full market depth (not just top of book)")
        self.logger.info("=" * 70)
        self.total_tested = len(currency_pairs)
        for i, symbol in enumerate(currency_pairs, 1):
            self.logger.info(f"[{i:2d}/{len(currency_pairs)}] Testing: {symbol}")
            try:
                self.client.send_market_data_request(symbol, 'snapshot_plus_updates')
                sleep(0.3)
            except Exception as e:
                self.logger.error(f"[ERROR] Failed to request {symbol}: {e}")
                self.failed_symbols.append(symbol)
        self.logger.info("[INFO] Waiting for server responses...")
        sleep(5)
        self.print_summary()

    def print_connection_help(self):
        self.logger.info("\n" + "="*70)
        self.logger.info("CONNECTION TROUBLESHOOTING:")
        self.logger.info("="*70)
        self.logger.info("1. Check if stunnel is running:")
        self.logger.info("   pgrep stunnel")
        self.logger.info("2. Test port connectivity:")
        self.logger.info("   telnet localhost 14508")
        self.logger.info("3. Check configuration:")
        self.logger.info("   cat config/neon.conf")
        self.logger.info("4. Verify credentials are correct")
        self.logger.info("5. Check if Neon server is accessible")
        self.logger.info("="*70)

    def print_summary(self):
        self.logger.info("=" * 70)
        self.logger.info(f"[SUMMARY] Tested {self.total_tested} currency pairs")
        self.logger.info(f"[SUMMARY] Connection successful: {len(self.successful_symbols)}")
        self.logger.info(f"[SUMMARY] Received snapshots: {len(self.received_snapshots)}")
        self.logger.info(f"[SUMMARY] Failed requests: {len(self.failed_symbols)}")
        if self.received_snapshots:
            self.logger.info(f"[SUCCESS] Received full snapshots for: {', '.join(self.received_snapshots)}")
            self.logger.info("[INFO] Success! You now have full market data snapshots!")
            self.logger.info("[INFO] Check the message log for detailed snapshot data")
        elif self.successful_symbols:
            self.logger.info(f"[PARTIAL] Connected but no snapshots for: {', '.join(self.successful_symbols)}")
            self.logger.info("[INFO] Connection works but check symbol formats or permissions")
        else:
            self.logger.info("[INFO] No currency pairs worked with this server")
            self.logger.info("[INFO] This means the Neon server may not support these currency pairs")
            self.logger.info("[INFO] Possible solutions:")
            self.logger.info("  1. Contact your Neon provider for supported symbols")
            self.logger.info("  2. Ask for a different server that supports these currency pairs")
            self.logger.info("  3. Check if you need different credentials/permissions")
            self.logger.info("  4. Verify the server configuration")
            self.logger.info("  5. Check if the server uses different symbol formats")
            self.logger.info("  6. Some servers may only support specific currency pairs")

    def start_staleness_checker(self):
        """Start the background staleness checker thread"""
        if not self.staleness_checker_running:
            self.staleness_checker_running = True
            self.staleness_checker_thread = threading.Thread(
                target=self.staleness_checker_loop,
                daemon=True,
                name="StalenessChecker"
            )
            self.staleness_checker_thread.start()
            self.logger.info("[INFO] Staleness checker started - monitoring for stale data (2s threshold)")

    def staleness_checker_loop(self):
        """Background loop to check for stale data every second"""
        while self.staleness_checker_running:
            try:
                self.check_staleness()
                sleep(1)  # Check every second
            except Exception as e:
                self.logger.error(f"[ERROR] Staleness checker error: {e}")
                sleep(1)  # Continue even on errors

    def check_staleness(self):
        """Check for stale data and send error messages if needed"""
        current_time = time()
        
        for symbol in self.currency_pairs:
            # Check if we have ever received data for this symbol
            if symbol in self.last_update_times:
                time_since_update = current_time - self.last_update_times[symbol]
                
                # If data is stale (older than threshold), send error message
                if time_since_update > self.staleness_threshold:
                    self.send_staleness_error(symbol, time_since_update)

    def send_staleness_error(self, symbol, staleness_duration):
        """Send JSON error message for stale data"""
        error_msg = f"No data received in last {staleness_duration:.1f} seconds"
        output = {
            "ticker": symbol,
            "bid": None,
            "ask": None,
            "midprice": None,
            "spread": None,
            "err": error_msg
        }
        print(json.dumps(output))
        # Log at debug level to avoid flooding the log
        if staleness_duration % 5 < 1:  # Log every ~5 seconds to reduce noise
            self.logger.warning(f"[STALENESS] {symbol}: {error_msg}")

    def stop_staleness_checker(self):
        """Stop the staleness checker thread gracefully"""
        if self.staleness_checker_running:
            self.staleness_checker_running = False
            if self.staleness_checker_thread and self.staleness_checker_thread.is_alive():
                self.staleness_checker_thread.join(timeout=1)
            self.logger.info("[INFO] Staleness checker stopped")

    def on_market_data_success(self, symbol):
        if symbol not in self.successful_symbols:
            self.successful_symbols.append(symbol)
            self.logger.info(f"[SUCCESS] {symbol} connection successful!")

    def on_market_data_reject(self, symbol):
        if symbol not in self.failed_symbols:
            self.failed_symbols.append(symbol)
            self.logger.error(f"[FAILED] {symbol} rejected by server")

    def on_logout(self):
        """Handle logout message by sending empty JSON with LOGOUT error"""
        import json
        output = {
            "ticker": "",
            "bid": None,
            "ask": None,
            "midprice": None,
            "spread": None,
            "err": "LOGOUT"
        }
        print(json.dumps(output))
        self.logger.info("[LOGOUT] Logout message received - sending empty JSON output")

    def on_tick(self, symbol, app):
        err = None
        if symbol not in app.history_dict:
            err = f"No history for symbol {symbol}"
        else:
            bid = app.history_dict[symbol].BID_TOB
            ask = app.history_dict[symbol].ASK_TOB
            if bid and ask:
                # Record timestamp of this update for staleness checking
                self.last_update_times[symbol] = time()
                
                # Check if bid or ask prices have changed
                previous = self.previous_prices.get(symbol, {})
                prev_bid = previous.get('bid')
                prev_ask = previous.get('ask')
                
                # Only output if bid or ask price has changed
                if prev_bid != bid or prev_ask != ask:
                    spread = ask - bid
                    mid_price = (bid + ask) / 2
                    # Update previous prices
                    self.previous_prices[symbol] = {
                        'bid': bid,
                        'ask': ask
                    }
                    # Only print JSON to stdout when prices change
                    output = {
                        "ticker": symbol,
                        "bid": float(bid),
                        "ask": float(ask),
                        "midprice": float(mid_price),
                        "spread": float(spread),
                        "err": None
                    }
                    print(json.dumps(output))
                # If prices haven't changed, don't output anything
            else:
                err = f"Incomplete data for {symbol}: bid={bid}, ask={ask}"
        if err:
            output = {
                "ticker": symbol,
                "bid": None,
                "ask": None,
                "midprice": None,
                "spread": None,
                "err": err
            }
            print(json.dumps(output))
            self.logger.error(err)

    def check_minute_marker_opportunities(self, symbol, mid_price):
        """
        Check for minute marker opportunities across all currency pairs
        This is where you would implement your minute marker logic
        """
        current_time = datetime.now()
        hour = current_time.hour
        minute = current_time.minute
        second = current_time.second
        
        # Check for minute marker activation periods
        activation_periods = [
            (10, 29, 30),  # 10:30 markers
            (12, 29, 30),  # 12:30 markers  
            (14, 29, 30),  # 14:30 markers
            (16, 29, 30),  # 16:30 markers
            (19, 28, 30),  # 19:30 TAS
        ]
        
        for target_hour, start_min, end_min in activation_periods:
            if hour == target_hour and start_min <= minute <= end_min:
                # Calculate activation progress
                total_seconds = (end_min - start_min) * 60
                elapsed_seconds = (minute - start_min) * 60 + second
                progress = min(elapsed_seconds / total_seconds, 1.0)
                
                self.logger.info(f"[MINUTE MARKER] {target_hour}:{end_min:02d} activation: {progress:.2%} | {symbol} @ {mid_price:.5f}")
                
                # Here you would implement your minute marker logic
                # - Calculate required position adjustments for each currency pair
                # - Apply gradual hedging changes
                # - Update delta ratios
                # - Track minute marker positions across all pairs

    def get_currency_summary(self):
        """
        Get a summary of all active currency pairs
        """
        if not self.currency_positions:
            return "No active currency positions"
        
        summary = f"Active Currency Pairs ({len(self.currency_positions)}):\n"
        for symbol, position in self.currency_positions.items():
            summary += f"  {symbol}: {position['mid_price']:.5f} (spread: {position['spread']:.5f})\n"
        return summary


# Global variables for signal handling
processor = None
logger = None
temp_logger = None

def signal_handler(signum, frame):
    """Handle interrupt signals gracefully"""
    global processor, logger, temp_logger
    
    signal_name = signal.Signals(signum).name
    current_logger = logger if logger else temp_logger
    
    if current_logger:
        current_logger.info(f"[INFO] Received {signal_name} signal - initiating graceful shutdown...")
    else:
        print(f"[INFO] Received {signal_name} signal - initiating graceful shutdown...")
    
    if processor and processor.client:
        if current_logger:
            current_logger.info("[INFO] Sending logout message to server...")
        else:
            print("[INFO] Sending logout message to server...")
        
        try:
            # Stop the staleness checker first
            processor.stop_staleness_checker()
            
            # Send explicit logout message
            processor.client.send_logout(f"Client shutdown due to {signal_name}")
            
            # Stop the client
            processor.client.stop()
            
            if current_logger:
                current_logger.info("[INFO] Logout sent successfully")
                current_logger.info("[INFO] Multi-Currency client stopped gracefully")
            else:
                print("[INFO] Logout sent successfully")
                print("[INFO] Multi-Currency client stopped gracefully")
                
        except Exception as e:
            if current_logger:
                current_logger.error(f"[ERROR] Error during shutdown: {e}")
            else:
                print(f"[ERROR] Error during shutdown: {e}")
    
    # Exit cleanly
    sys.exit(0)

##############################################################################

if __name__ == "__main__":
    # Set up signal handlers for graceful shutdown
    signal.signal(signal.SIGINT, signal_handler)   # Ctrl+C
    signal.signal(signal.SIGTERM, signal_handler)  # Termination signal
    
    # Remove all non-JSON stdout prints, log them instead
    start_banner = "=" * 70
    log_lines = [
        start_banner,
        "Multi-Currency NEON MARKET DATA CLIENT - FULL SNAPSHOTS",
        start_banner,
        "Requesting FULL market depth (not just top of book)",
        "Focused on multiple currency pairs",
        "Will test different symbol formats to find what works",
        "Log file will be created with timestamp (DD-MM-YYYY:HH-MM-SS format)",
        start_banner
    ]
    # Temporary logger for startup before processor is created
    os.makedirs('logs', exist_ok=True)
    temp_logger = logging.getLogger('neon_startup_logger')
    temp_logger.setLevel(logging.INFO)
    temp_log_file = 'logs/neon_startup.log'
    if not temp_logger.handlers:
        temp_handler = logging.FileHandler(temp_log_file)
        temp_formatter = logging.Formatter('%(asctime)s %(levelname)s %(message)s')
        temp_handler.setFormatter(temp_formatter)
        temp_logger.addHandler(temp_handler)
    for line in log_lines:
        temp_logger.info(line)

    # Parse command line arguments
    parser = argparse.ArgumentParser(description='Neon Market Data Client')
    parser.add_argument('--instruments', type=str, default='EUR/USD',
                        help='Comma-separated list of instruments/currency pairs to request (e.g. EUR/USD,GBP/USD,USD/CHF)')
    args = parser.parse_args()
    instruments = [s.strip() for s in args.instruments.split(',') if s.strip()]

    # Create the Multi-Currency tick processor
    try:
        processor = MultiCurrencyTickProcessor(currency_pairs=instruments)
        # Use processor.logger for all further logs
        logger = processor.logger
        if processor.client.isLoggedOn():
            logger.info("[INFO] Multi-Currency client is running")
            logger.info("[INFO] Monitoring for full market data snapshots...")
            logger.info("[INFO] Perfect for minute marker/TAS analysis")
            logger.info("[INFO] Press Ctrl+C to stop gracefully (will send logout)")
            logger.info(start_banner)
            while processor.client.isLoggedOn():
                sleep(1)  # Check connection every second for more responsive shutdown
        else:
            logger.error("[ERROR] Connection failed - exiting")
    except Exception as e:
        current_logger = logger if 'logger' in locals() else temp_logger
        current_logger.error(f"[ERROR] Unexpected error: {e}")
        import traceback
        traceback.print_exc()
        
        # Clean shutdown even on error
        if 'processor' in locals() and processor and processor.client:
            try:
                processor.client.send_logout("Client error shutdown")
                processor.client.stop()
            except Exception:
                pass 