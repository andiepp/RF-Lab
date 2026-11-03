// ============================================================================
// ===                                                                      ===
// ===                             C O M   R x                              ===
// ===                                                                      ===
// ============================================================================
///
///  COM INPUT:
///  - collect received bytes and save them into Buffer.
///  - analyze received data and built data package according to the message id 
///  - call message dispacher when the MCNF message build is completed; 
///    the MCNF message data is passed in Buffer.
///  - the message dispatcher returns with the number of bytes to be sent and
///    the response bytes in Buffer with error byte in Buffer[0].
///  - return with Buffer filled and number of bytes to be sent as response.
///  - support timeout check
///  - set Error byte in case an error was detected.
///
///     MCNF Message Layout:
///     <==   header  ==> <=============   body   =============>
///     +----+---+---+---+---------------------------+----------+
///     | ID | N | R | T |       C O M M A N D       | Checksum |
///     +----+---+---+---+---------------------------+----------+
///                   ---+----------+----------------+----------+
///                      | Function |   Parameter    | Checksum |
///                   ---+----------+----------------+----------+
///                                 <----- N-2 ----->
///     HEADERSIZE H = 4 <----------------- N ------------------>
///        0   1   2   3      4       5   ... N+2        N+3       indizes for H=4  
///                   H-1     H       H+1 ... H+N-2      H+N-1     indizes


// ============================================================================
// ===                                                                      ===
// ===                         G L O B A L   D A T A                        ===
// ===                                                                      ===
// ============================================================================
///
///  COM input communication state definitions:
///
#define COM_INITIAL                    0
#define COM_GET_LENGTH                 1
#define COM_GET_DATA                   2
///
///  Byte input communication state byte:
///
static volatile byte COMstate = COM_INITIAL;
///
///  Message id (after discarding the SYNC nibble):
///
static volatile byte MSG_Id;
///
///  Message length:
///
static volatile byte MSG_Length_N;
///
///  Response offset:
///
static volatile byte Offset;


// ============================================================================
// ===                                                                      ===
// ===                             C O M   R X                              ===
// ===                                                                      ===
// ============================================================================
///
///  byte COM_Rx(byte source, byte rx):
///  - process the byte received based on the COM state
///  - call message dispatcher to process message when completely received
///  - answer the number of bytes to be sent (response bytes must be in Buffer)
///  - receiving a complete MCNF message is implemented using a state-based
///    approach with COMstate as state index and counter
///  - connectivity test response handled directly by COM_Rx 
///
//  -----------------------------------
    byte COM_Rx(byte source, byte rx) {
//  -----------------------------------   
    // branch according to communication state:
    switch (COMstate) {
      
      // COM_INITIAL 
      // - we expect a valid message id byte:
      // - first nibble must be SYNC pattern
      // - start COMtimeout check
      case COM_INITIAL: 
        {
          // save message id incl. SYNC nibble in Buffer[0]:
          Buffer[0] = rx; 
          // check for valid SYNC nibble:
          if ((rx & MSG_SYNC_MASK) == MSG_SYNC) {
            // Get message id after discarding the SYNC nibble:
            MSG_Id = rx & MSG_ID_MASK;
            // branch according to MSG_Id: 
            // - connectivity test and node type requests can be 
            //   handled in MSG_Dispatcher based on id byte only
            // - all others will receive length data next, so we
            // - save the current time stamp for timeout check
           switch (MSG_Id) {              
              case MSG_ID_CONNECTIVITY_TEST:                 {
                  return MSG_Dispatcher(source, MSG_Id);
                }
              default: // all others incl. speed test will continue
                {
                  COMstate = COM_GET_LENGTH;
                  // --- start timeout ---:
                  COM_Timeout_0 = millis();
                  return 0;
                }
            }  // switch (MSG_Id) . . .
          } else {
            Error = ERR_SYNC_MISSING;
            COMstate = COM_INITIAL;
            COM_Timeout_0 = COM_TIMEOUT_DISABLED;
            return 0;     
          }
        }
        
      // COM_GET_LENGTH 
      // - get the message length byte
      // - and check against buffer size
      // - save length byte in buffer
      case COM_GET_LENGTH: 
        {
          MSG_Length_N = rx;
          if ((MSG_Length_N + HEADERSIZE) > BUFFER_SIZE) {
            Error = ERR_BUFFER_OVERFLOW;
            COMstate = COM_INITIAL;
            // --- stop timeout ---:
            COM_Timeout_0 = COM_TIMEOUT_DISABLED;
            return 0;     
          } else {
            Buffer[1] = MSG_Length_N; 
            COMstate = COM_GET_DATA;
            // Speed Test message has no header bytes,
            // this is considered by setting a different Offset value:
            if (MSG_Id == MSG_ID_SPEED_TEST) {
              Offset = 1;
            } else {  
              Offset = (HEADERSIZE-1);
            }
          }
          return 0;     
        }

      // COM_GET_DATA 
      // - receive data byte and save it in buffer
      // - if enough bytes received, reset state and call message dispatcher 
      default: 
        {
          Buffer[COMstate] = rx;                
	        if (COMstate < (MSG_Length_N + Offset)) {
            COMstate++;
            return 0;
          } else { 
            // we call message dispatcher first
            // ! timeout check will include message execution time !
            ////////////////////////////////////////////
            byte n_tx =  MSG_Dispatcher(source, MSG_Id);   
            ////////////////////////////////////////////        
            COMstate = COM_INITIAL;
            // --- stop timeout ---:
            COM_Timeout_0 = COM_TIMEOUT_DISABLED;
            // return number of bytes to be sent:
            return n_tx;
          }
        }  // switch (COMstate) ...default:    
    }  // switch (COMstate) ...       
  } // byte UART_Rx(byte rx) {

    
// ============================================================================
// ===                                                                      ===
// ===                   C O M   R X   T I M E O U T                        ===
// ===                                                                      ===
// ============================================================================
///
///  COM_Rx_Timeout():
///  - reset COMstate
///  - disable further timeout
///  - set Error to timeout error
///
//  -----------------------
    void COM_Rx_Timeout() {
//  -----------------------
      #if (DEBUG > 0)
         debug.println(" => COM Timeout !");
      #endif
      COMstate = COM_INITIAL;
      COM_Timeout_0 = COM_TIMEOUT_DISABLED;
      Error = ERR_RX_TIMEOUT;
    }

// ============================================================================
// ==================================  E O F  =================================
// ============================================================================
