import socket
import threading
import time
import logging
from datetime import datetime
from pathlib import Path
import simplefix

from .simplefix_application import SimpleFIXApplication


class SimpleFIXClient:
    """
    SimpleFIX client wrapper for market data connections
    """
    
    def __init__(self, tick_processor, 
                 config_file='config/market_data.conf',
                 store_all_ticks=True,
                 save_history_to_files=True,
                 verbose=True,
                 message_log_file='messages.log'):
        
        self.tick_processor = tick_processor
        self.config_file = config_file
        self.verbose = verbose
        self.connected = False
        self.socket = None
        self.app = None
        self.receiver_thread = None
        self.heartbeat_thread = None
        self.running = False
        
        # Load configuration
        self.config = self._load_config()
        
        # Create application instance
        self.app = SimpleFIXApplication(
            self.config, 
            self.tick_processor,
            store_all_ticks=store_all_ticks,
            save_history_to_files=save_history_to_files,
            verbose=verbose,
            message_log_file=message_log_file
        )
        
        # Start the connection
        self._start_connection()
    
    def _load_config(self):
        """Load configuration from file"""
        config = {}
        try:
            with open(self.config_file, 'r') as f:
                current_section = None
                for line in f:
                    line = line.strip()
                    if not line or line.startswith('#'):
                        continue
                    if line.startswith('[') and line.endswith(']'):
                        current_section = line[1:-1]
                        config[current_section] = {}
                    elif '=' in line and current_section:
                        key, value = line.split('=', 1)
                        config[current_section][key] = value
            
            if self.verbose:
                print("[INFO] Configuration loaded successfully")
                
        except Exception as e:
            print(f"[ERROR] Failed to load configuration: {e}")
            raise
        
        return config
    
    def _start_connection(self):
        """Start the socket connection"""
        try:
            if self.verbose:
                print("[INFO] Starting SimpleFIX connection...")
            
            # Get connection details - use QUOTE SESSION for neon.conf
            session_config = self.config.get('QUOTE SESSION', {})
            host = session_config.get('SocketConnectHost', 'localhost')
            port = int(session_config.get('SocketConnectPort', 14507))
            
            # Create socket connection
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.settimeout(30)  # 30 second timeout
            
            if self.verbose:
                print(f"[DEBUG] Connecting to {host}:{port}")
            
            self.socket.connect((host, port))
            self.running = True
            
            if self.verbose:
                print(f"[DEBUG] Socket connected to {host}:{port}")
            
            # Start receiver thread
            self.receiver_thread = threading.Thread(target=self._receive_messages)
            self.receiver_thread.daemon = True
            self.receiver_thread.start()
            
            # Send logon message
            self._send_logon()
            
            # Wait for logon confirmation
            timeout = 30  # Increased timeout
            start_time = time.time()
            while not self.connected and (time.time() - start_time) < timeout:
                time.sleep(0.1)
            
            if self.connected:
                # Start heartbeat thread
                self.heartbeat_thread = threading.Thread(target=self._heartbeat_loop)
                self.heartbeat_thread.daemon = True
                self.heartbeat_thread.start()
                
                if self.verbose:
                    print("[INFO] SimpleFIX connection established")
            else:
                print("[ERROR] Connection timeout - failed to establish connection")
                print("[DEBUG] Check if server is accepting connections and credentials are correct")
                
        except Exception as e:
            print(f"[ERROR] Failed to start connection: {e}")
            raise
    
    def _send_logon(self):
        """Send logon message"""
        try:
            session_config = self.config.get('QUOTE SESSION', {})
            
            msg = simplefix.FixMessage()
            msg.append_pair(8, "FIX.4.4")  # BeginString
            msg.append_pair(35, "A")       # MsgType = Logon
            msg.append_pair(49, session_config.get('SenderCompID', ''))  # SenderCompID
            msg.append_pair(56, session_config.get('TargetCompID', ''))  # TargetCompID
            
            # Add DeliverToCompID in header section (before MsgSeqNum)
            if session_config.get('DeliverToCompID'):
                msg.append_pair(128, session_config.get('DeliverToCompID'))  # DeliverToCompID
            
            msg.append_pair(34, str(self.app.get_next_seq_num()))  # MsgSeqNum
            
            # Add SenderSubID in header section (after MsgSeqNum)
            if session_config.get('SenderSubID'):
                msg.append_pair(50, session_config.get('SenderSubID'))  # SenderSubID
            
            msg.append_utc_timestamp(52)   # SendingTime
            
            # Logon body fields
            msg.append_pair(98, "0")       # EncryptMethod
            msg.append_pair(108, "20")     # HeartBtInt
            msg.append_pair(141, "Y")      # ResetSeqNumFlag
            msg.append_pair(553, session_config.get('User', ''))  # Username (field 553)
            msg.append_pair(554, session_config.get('Password', ''))  # Password
            
            self._send_message(msg)
            
        except Exception as e:
            print(f"[ERROR] Failed to send logon: {e}")
    
    def _send_message(self, msg):
        """Send a FIX message"""
        try:
            if self.socket:
                encoded_msg = msg.encode()
                if self.verbose:
                    print(f"[SEND] {encoded_msg.decode('ascii')}")
                self.socket.send(encoded_msg)
                
        except Exception as e:
            print(f"[ERROR] Failed to send message: {e}")
    
    def _receive_messages(self):
        """Receive messages in a separate thread"""
        parser = simplefix.FixParser()
        
        while self.running:
            try:
                data = self.socket.recv(4096)
                if not data:
                    if self.verbose:
                        print("[DEBUG] No data received - server closed connection")
                    break
                
                if self.verbose:
                    print(f"[DEBUG] Received {len(data)} bytes: {data}")
                
                parser.append_buffer(data)
                
                while True:
                    msg = parser.get_message()
                    if msg is None:
                        break
                    
                    if self.verbose:
                        print(f"[RECV] {msg}")
                    
                    # Process the message
                    self.app.process_message(msg)
                    
                    # Update connection state from app
                    if not self.connected and self.app.connected:
                        self.connected = True
                    elif self.connected and not self.app.connected:
                        self.connected = False
                        break
                    
            except socket.timeout:
                continue
            except Exception as e:
                if self.running:
                    print(f"[ERROR] Error receiving message: {e}")
                break
    
    def _heartbeat_loop(self):
        """Send heartbeat messages"""
        while self.running and self.connected:
            try:
                time.sleep(20)  # Send heartbeat every 20 seconds
                
                msg = simplefix.FixMessage()
                msg.append_pair(8, "FIX.4.4")  # BeginString
                msg.append_pair(35, "0")       # MsgType = Heartbeat
                msg.append_pair(49, self.config['QUOTE SESSION'].get('SenderCompID', ''))
                msg.append_pair(56, self.config['QUOTE SESSION'].get('TargetCompID', ''))
                msg.append_pair(34, str(self.app.get_next_seq_num()))
                msg.append_utc_timestamp(52)
                
                self._send_message(msg)
                
            except Exception as e:
                print(f"[ERROR] Heartbeat error: {e}")
                break
    
    def isLoggedOn(self):
        """Check if the session is logged on"""
        return self.connected and self.app and self.app.connected
    
    def send_market_data_request(self, symbol, req_type='snapshot'):
        """Send market data request"""
        try:
            msg = simplefix.FixMessage()
            msg.append_pair(8, "FIX.4.4")  # BeginString
            msg.append_pair(35, "V")       # MsgType = MarketDataRequest
            msg.append_pair(49, self.config['QUOTE SESSION'].get('SenderCompID', ''))
            msg.append_pair(50, self.config['QUOTE SESSION'].get('SenderSubID', ''))  # SenderSubID
            msg.append_pair(56, self.config['QUOTE SESSION'].get('TargetCompID', ''))
            msg.append_pair(34, str(self.app.get_next_seq_num()))
            msg.append_utc_timestamp(52)
            
            if self.config['QUOTE SESSION'].get('DeliverToCompID'):
                msg.append_pair(128, self.config['QUOTE SESSION'].get('DeliverToCompID'))
            
            req_id = str(self.app.get_next_request_id())
            
            # Map request ID to symbol for rejection handling
            self.app._id_to_symbol[req_id] = symbol
            
            msg.append_pair(262, req_id)   # MDReqID
            
            # Request type: 0=Snapshot, 1=Snapshot+Updates
            if req_type == 'snapshot_only':
                msg.append_pair(263, "0")  # SubscriptionRequestType = Snapshot Only
            else:
                msg.append_pair(263, "1")  # SubscriptionRequestType = Snapshot + Updates
            
            # Market depth: 0=Full book, 1=Top of book
            msg.append_pair(264, "1")      # MarketDepth = Top of Book
            msg.append_pair(265, "0")      # MDUpdateType = Full Refresh
            
            # Symbol group
            msg.append_pair(146, "1")      # NoRelatedSym
            msg.append_pair(55, symbol)    # Symbol
            
            # Security type for FX instruments
            if any(fx in symbol.upper() for fx in ['EUR', 'USD', 'GBP', 'JPY', 'CHF', 'AUD', 'CAD', 'NZD']):
                msg.append_pair(460, "4")  # SecurityType = Future
                msg.append_pair(167, "FOR")  # SecurityType = FOR (Foreign Exchange)
            else:
                msg.append_pair(460, "4")  # SecurityType = Future
                msg.append_pair(167, "FUT")  # SecurityType = FUT
            
            # Entry types we want
            msg.append_pair(267, "2")      # NoMDEntryTypes
            msg.append_pair(269, "0")      # MDEntryType = Bid
            msg.append_pair(269, "1")      # MDEntryType = Offer
            
            self._send_message(msg)
            
        except Exception as e:
            print(f"[ERROR] Failed to send market data request: {e}")
    
    def send_logout(self, text="User requested logout"):
        """Send logout message"""
        try:
            if not self.connected:
                if self.verbose:
                    print("[INFO] Not connected, skipping logout")
                return
            
            session_config = self.config.get('QUOTE SESSION', {})
            
            msg = simplefix.FixMessage()
            msg.append_pair(8, "FIX.4.4")  # BeginString
            msg.append_pair(35, "5")       # MsgType = Logout
            msg.append_pair(49, session_config.get('SenderCompID', ''))  # SenderCompID
            msg.append_pair(56, session_config.get('TargetCompID', ''))  # TargetCompID
            
            # Add DeliverToCompID in header section
            if session_config.get('DeliverToCompID'):
                msg.append_pair(128, session_config.get('DeliverToCompID'))  # DeliverToCompID
            
            msg.append_pair(34, str(self.app.get_next_seq_num()))  # MsgSeqNum
            
            # Add SenderSubID in header section
            if session_config.get('SenderSubID'):
                msg.append_pair(50, session_config.get('SenderSubID'))  # SenderSubID
            
            msg.append_utc_timestamp(52)   # SendingTime
            
            # Logout body fields
            if text:
                msg.append_pair(58, text)  # Text field for logout reason
            
            if self.verbose:
                print(f"[INFO] Sending logout: {text}")
            
            self._send_message(msg)
            
            # Give server time to process logout
            time.sleep(0.5)
            
        except Exception as e:
            print(f"[ERROR] Failed to send logout: {e}")
    
    def stop(self):
        """Stop the connection"""
        try:
            if self.verbose:
                print("[INFO] Stopping SimpleFIX connection...")
            
            # Send logout message before closing
            if self.connected:
                self.send_logout("Client shutdown")
            
            self.running = False
            self.connected = False
            
            if self.socket:
                self.socket.close()
                self.socket = None
            
            if self.verbose:
                print("[INFO] SimpleFIX connection stopped")
        except Exception as e:
            print(f"[ERROR] Failed to stop connection: {e}")
    
    def __del__(self):
        """Destructor to ensure connection is closed"""
        self.stop() 