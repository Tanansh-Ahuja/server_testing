import logging
import json
from datetime import datetime
from threading import Lock
import simplefix

from .helpers import log, setup_logger
from .history import history


class SimpleFIXApplication:
    """
    Simplified FIX application using simplefix
    """
    
    def __init__(self, config, tick_processor,
                 store_all_ticks=True,
                 save_history_to_files=True,
                 verbose=True,
                 message_log_file='logs/neon_messages.log'):
        
        self.config = config
        self.tick_processor = tick_processor
        self.store_all_ticks = store_all_ticks
        self.save_history_to_files = save_history_to_files
        self.verbose = verbose
        self.connected = False
        self.lock = Lock()
        
        # Sequence numbers
        self.next_seq_num = 1
        self.next_request_id = 1
        
        # Setup logging
        self.logger = None
        if len(message_log_file) > 0:
            self.logger = setup_logger('message_logger', message_log_file,
                                       '%(asctime)s %(levelname)s %(message)s',
                                       level=logging.INFO)
        
        # Dictionary to hold Asset Histories
        self.history_dict = {}  # format: 'EURUSD': History
        
        # Request ID mapping
        self._id_to_symbol = {}  # format: '0': 'EURUSD'
    
    def get_next_seq_num(self):
        """Get next sequence number"""
        with self.lock:
            seq = self.next_seq_num
            self.next_seq_num += 1
            return seq
    
    def get_next_request_id(self):
        """Get next request ID"""
        with self.lock:
            req_id = self.next_request_id
            self.next_request_id += 1
            return req_id
    
    def process_message(self, msg):
        """Process incoming FIX message"""
        try:
            # Get message type
            msg_type = msg.get(35)  # MsgType
            
            # Convert bytes to string if needed
            if isinstance(msg_type, bytes):
                msg_type = msg_type.decode('ascii')
            
            if self.verbose:
                print(f"[PROCESS] MsgType: {msg_type}")
            
            log(self.logger, f'Message = {msg}')
            
            # Debug: print message content for connection issues
            if msg_type in ['3', '5', 'j']:  # Reject, Logout, BusinessMessageReject
                print(f"[DEBUG] Problem message received: {msg}")
            
            if msg_type == 'A':  # Logon
                self._handle_logon(msg)
            elif msg_type == '0':  # Heartbeat
                self._handle_heartbeat(msg)
            elif msg_type == '1':  # Test Request
                self._handle_test_request(msg)
            elif msg_type == '5':  # Logout
                self._handle_logout(msg)
            elif msg_type == 'W':  # Market Data Snapshot
                self._handle_market_data_snapshot(msg)
            elif msg_type == 'X':  # Market Data Incremental Refresh
                self._handle_market_data_incremental(msg)
            elif msg_type == 'Y':  # Market Data Request Reject
                self._handle_market_data_reject(msg)
            elif msg_type == 'i':  # Mass Quote
                self._handle_mass_quote(msg)
            elif msg_type == '3':  # Reject
                self._handle_reject(msg)
            else:
                if self.verbose:
                    print(f"[WARN] Unhandled message type: {msg_type}")
                    
        except Exception as e:
            print(f"[ERROR] Error processing message: {e}")
    
    def _handle_logon(self, msg):
        """Handle logon response"""
        self.connected = True
        # Remove print to stdout, only log
        log(self.logger, '[INFO] Logon successful')
    
    def _handle_heartbeat(self, msg):
        """Handle heartbeat message"""
        if self.verbose:
            print("[INFO] Heartbeat received")
    
    def _handle_test_request(self, msg):
        """Handle test request - should respond with heartbeat"""
        test_req_id = msg.get(112)  # TestReqID
        if self.verbose:
            print(f"[INFO] Test request received: {test_req_id}")
        # Note: In a full implementation, we should respond with a heartbeat
    
    def _handle_logout(self, msg):
        """Handle logout message"""
        self.connected = False
        print("[INFO] Logout received")
        log(self.logger, 'Logout received')
        
        # Notify tick processor of logout
        if self.tick_processor and hasattr(self.tick_processor, 'on_logout'):
            self.tick_processor.on_logout()
    
    def _handle_reject(self, msg):
        """Handle reject message"""
        reject_reason = msg.get(58, 'Unknown')  # Text
        print(f"[ERROR] Message rejected: {reject_reason}")
        log(self.logger, f'Message rejected: {reject_reason}')
    
    def _handle_market_data_snapshot(self, msg):
        """Handle Market Data Snapshot Full Refresh"""
        try:
            symbol = msg.get(55)  # Symbol
            if isinstance(symbol, bytes):
                symbol = symbol.decode('ascii')
                
            if not symbol:
                print("[ERROR] Market data snapshot without symbol")
                return
            
            # Initialize history if needed
            if symbol not in self.history_dict:
                self.history_dict[symbol] = history(symbol)
            
            # Process MD entries - parse repeating groups properly
            bid_price = None
            ask_price = None
            bid_size = None
            ask_size = None
            
            # Number of MD entries
            num_entries = msg.get(268)  # NoMDEntries
            if isinstance(num_entries, bytes):
                num_entries = int(num_entries.decode('ascii'))
            elif isinstance(num_entries, str):
                num_entries = int(num_entries)
            elif num_entries is None:
                num_entries = 0
            
            if self.verbose:
                print(f"[DEBUG] Processing {num_entries} MD entries for {symbol}")
                print(f"[DEBUG] Raw message: {msg}")
            
            # Parse repeating groups by accessing all field values
            # Extract all instances of MD entry fields
            md_entries = self._extract_market_data_entries(msg)
            
            if self.verbose:
                print(f"[DEBUG] Extracted {len(md_entries)} MD entries: {md_entries}")
            
            # Process each MD entry
            for entry in md_entries:
                entry_type = entry.get('type')
                entry_px = entry.get('price')
                entry_size = entry.get('size')
                
                if entry_type == '0':  # Bid
                    bid_price = entry_px
                    bid_size = entry_size
                    if self.verbose:
                        print(f"[DEBUG] BID extracted: {bid_price} @ {bid_size}")
                elif entry_type == '1':  # Offer/Ask
                    ask_price = entry_px
                    ask_size = entry_size
                    if self.verbose:
                        print(f"[DEBUG] ASK extracted: {ask_price} @ {ask_size}")
            
            # Log parsing results
            if self.verbose:
                print(f"[DEBUG] Final parsed values - BID: {bid_price} @ {bid_size}, ASK: {ask_price} @ {ask_size}")
            
            # Only use fallback data if parsing completely failed
            if bid_price is None and ask_price is None:
                print(f"[WARNING] No valid market data extracted for {symbol} - parsing failed")
                return  # Don't use fallback data, just skip this update
            
            # Ensure we have both bid and ask (at minimum one should be present)  
            if bid_price is None or ask_price is None:
                print(f"[WARNING] Incomplete market data for {symbol} - BID: {bid_price}, ASK: {ask_price}")
                # Still proceed with partial data for now, but log the issue
            
            # Update history with extracted data
            if bid_price is not None:
                self.history_dict[symbol].BID_TOB = bid_price
                if bid_size is not None:
                    self.history_dict[symbol].BID_SIZE = bid_size
                    
            if ask_price is not None:
                self.history_dict[symbol].ASK_TOB = ask_price
                if ask_size is not None:
                    self.history_dict[symbol].ASK_SIZE = ask_size
            
            # Only proceed with tick processing if we have both bid and ask
            if bid_price and ask_price:
                # Store tick if enabled
                if self.store_all_ticks:
                    tick_data = {
                        'TIME': datetime.now(),
                        'symbol': symbol,
                        'bid': bid_price,
                        'ask': ask_price,
                        'bid_size': bid_size,
                        'ask_size': ask_size,
                        'spread': ask_price - bid_price,
                        'mid': (bid_price + ask_price) / 2
                    }
                    self.history_dict[symbol].HISTORY.append(tick_data)
                
                # Notify tick processor of successful market data
                if self.tick_processor and hasattr(self.tick_processor, 'on_market_data_success'):
                    self.tick_processor.on_market_data_success(symbol)
                
                # Call tick processor
                if self.tick_processor and hasattr(self.tick_processor, 'on_tick'):
                    self.tick_processor.on_tick(symbol, self)
            
            if self.verbose:
                spread = ask_price - bid_price if (ask_price and bid_price) else 0
                mid = (bid_price + ask_price) / 2 if (ask_price and bid_price) else 0
                print(f"[SNAPSHOT] {symbol:8} | BID: {bid_price:8.5f} | ASK: {ask_price:8.5f} | MID: {mid:8.5f} | SPREAD: {spread:6.5f}")
                
        except Exception as e:
            print(f"[ERROR] Error processing market data snapshot: {e}")
            import traceback
            traceback.print_exc()
    
    def _handle_market_data_incremental(self, msg):
        """Handle Market Data Incremental Refresh"""
        if self.verbose:
            print("[INFO] Market data incremental refresh received")
        # Similar to snapshot but for incremental updates
    
    def _handle_market_data_reject(self, msg):
        """Handle Market Data Request Reject"""
        try:
            req_id = msg.get(262)  # MDReqID
            reason = msg.get(58)   # Text (reason for rejection)
            reject_code = msg.get(281)  # MDReqRejReason
            
            if isinstance(reason, bytes):
                reason = reason.decode('ascii')
            if isinstance(reject_code, bytes):
                reject_code = reject_code.decode('ascii')
            
            symbol = "Unknown"
            if req_id in self._id_to_symbol:
                symbol = self._id_to_symbol[req_id]
            
            print(f"[REJECT] Market data request rejected for {symbol}")
            print(f"[REJECT] Reason: {reason}")
            print(f"[REJECT] Code: {reject_code}")
            
            # Notify tick processor of rejection
            if self.tick_processor and hasattr(self.tick_processor, 'on_market_data_reject'):
                self.tick_processor.on_market_data_reject(symbol)
            
            # Provide specific guidance based on the error
            if "InvalidCurrencyPair" in str(reason):
                print(f"[HELP] Symbol '{symbol}' is not supported by this server")
                print(f"[HELP] Check with your provider for valid symbol formats")
            elif "SenderSubIDNotSet" in str(reason):
                print(f"[HELP] Configuration issue - check SenderSubID in neon.conf")
            else:
                print(f"[HELP] Server rejected the request - check symbol format and permissions")
                
        except Exception as e:
            print(f"[ERROR] Error processing market data reject: {e}")
            print(f"[DEBUG] Raw message: {msg}")
    
    def _handle_mass_quote(self, msg):
        """Handle Mass Quote message"""
        if self.verbose:
            print("[INFO] Mass quote received")
        # Process mass quote similar to market data snapshot
    
    def _extract_market_data_entries(self, msg):
        """Extract all market data entries from FIX message"""
        entries = []
        try:
            # Parse the message string directly since simplefix might not expose all field instances
            msg_str = str(msg)
            if self.verbose:
                print(f"[DEBUG] Parsing message string: {msg_str[:200]}...")
            
            # Split by SOH character (|) to get individual fields
            fields = msg_str.split('|')
            
            # Extract all MD entry related fields in order
            entry_types = []  # 269 values
            entry_prices = []  # 270 values  
            entry_sizes = []  # 271 values
            
            for field in fields:
                if '=' in field:
                    try:
                        tag, value = field.split('=', 1)
                        tag_int = int(tag)
                        
                        if tag_int == 269:  # MDEntryType
                            entry_types.append(value)
                            if self.verbose:
                                print(f"[DEBUG] Found MDEntryType: {value}")
                        elif tag_int == 270:  # MDEntryPx
                            try:
                                price = float(value)
                                entry_prices.append(price)
                                if self.verbose:
                                    print(f"[DEBUG] Found MDEntryPx: {price}")
                            except ValueError:
                                print(f"[ERROR] Invalid price value: {value}")
                                continue
                        elif tag_int == 271:  # MDEntrySize
                            try:
                                size = float(value)
                                entry_sizes.append(size)
                                if self.verbose:
                                    print(f"[DEBUG] Found MDEntrySize: {size}")
                            except ValueError:
                                print(f"[ERROR] Invalid size value: {value}")
                                continue
                    except (ValueError, IndexError):
                        continue
            
            if self.verbose:
                print(f"[DEBUG] Extracted types={entry_types}, prices={entry_prices}, sizes={entry_sizes}")
            
            # Combine the extracted values into entries
            # They should appear in the same order: type, price, size for each entry
            min_len = min(len(entry_types), len(entry_prices), len(entry_sizes))
            
            for i in range(min_len):
                entries.append({
                    'type': entry_types[i],
                    'price': entry_prices[i],
                    'size': entry_sizes[i]
                })
                if self.verbose:
                    print(f"[DEBUG] Created entry {i}: type={entry_types[i]}, price={entry_prices[i]}, size={entry_sizes[i]}")
                
        except Exception as e:
            print(f"[ERROR] Error extracting market data entries: {e}")
            import traceback
            traceback.print_exc()
        
        return entries
    
    def _parse_repeating_group(self, msg, group_tag, entry_tags):
        """Parse repeating group from message"""
        # This is a simplified implementation
        # In a real implementation, we'd need more sophisticated parsing
        entries = []
        try:
            group_count_val = msg.get(group_tag, 0)
            if isinstance(group_count_val, bytes):
                group_count_val = group_count_val.decode('ascii')
            
            # Handle case where group_count might be 'Unknown' or invalid
            try:
                group_count = int(group_count_val)
            except (ValueError, TypeError):
                print(f"[ERROR] Invalid group count: {group_count_val}")
                return []
            
            for _ in range(group_count):
                entry = {}
                for tag in entry_tags:
                    value = msg.get(tag)
                    if value:
                        entry[tag] = value
                entries.append(entry)
        except Exception as e:
            print(f"[ERROR] Error parsing repeating group: {e}")
        
        return entries 