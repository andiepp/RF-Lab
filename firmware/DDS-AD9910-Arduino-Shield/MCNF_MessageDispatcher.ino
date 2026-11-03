// ============================================================================
// ===                                                                      ===
// ===            M C N F   M E S S A G E    D I S P A T C H E R            ===
// ===                                                                      ===
// ============================================================================
///
///  MCNF MESSAGE DISPATCHER:
///  - the message dispatcher is called by the data receiving routines
///    when a complete MCNF message was received
///  - it returns a byte defining the number of response bytes to be sent
///    and with the buffer filled with response bytes
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
///
///  - handle the two special message ids:
///    - on MSG_ID_CONNECTIVITY_TEST answer the connectivity response byte
///    - on MSG_ID_SPEED_TEST echoes the bytes received as response data
///  - all other messages are routed to the respective handler after
///    the checksum was checked:
///	- ID_NodeAdmin => calls NODE_ProcessMessage()
///	- ID_DeviceChannel_1...4 => calls DEVICE_ProcessMessage(DeviceChannel)
///	- ID_GatewayChannel_1...4 => calls GATEWAY_ProcessMessage(GatewayChannel)
/// 	- the message content is passed with the global byte array "buffer"
///  - these handlers are expected to return with an error indication and
///    the buffer filled with response bytes:
///   	- buffer[0] is error code, 
///	- buffer[1...R-1] is response data;
///    finally, the checksum is calculated and added before sending the response. 
    
    
// ============================================================================
// ===                                                                      ===
// ===                      M S G   D i s p a t c h e r                     ===
// ===                                                                      ===
// ============================================================================
///
///  MSG_Dispatcher():
///  - the MCNF message data is expected in Buffer 
///  - the message id after discarding the SYNC nibble is passed as a parameter
///    (Buffer[0] will hold the id plus the SYNC nibble for checksum test !)
///
//  -----------------------------------------------
    byte MSG_Dispatcher(byte source, byte MSG_Id) {    
//  -----------------------------------------------   
    // switch according to MCNF message id in Buffer[0]:
    switch (MSG_Id) {
      
      // on receiving 'MSG_ID_CONNECTIVITY_TEST' send byte value RESPONSE_CONNECTIVITY_TEST :
      case MSG_ID_CONNECTIVITY_TEST:
        {
          Buffer[0] = RESPONSE_CONNECTIVITY_TEST_CRP;  
          return 1;     
        }
        
      // on receiving 'MSG_ID_SPEED_TEST' echoe the bytes received
      // after subtracting 2 from each byte: 
      case MSG_ID_SPEED_TEST: 
        {
          byte speedTestSize = Buffer[1];
          for (byte i=0; i < speedTestSize; i++) Buffer[i] = Buffer[i+2] - 2;
          return speedTestSize; 
        }      
        
      //----------------------------------//
      // Node / Device / Gateway commands: 
      //----------------------------------//
      default: 
        {
          byte MSG_N = Buffer[1];
          byte MSG_R = Buffer[2];
          byte MSG_T = Buffer[3];

          // test checksum and return with error data set if test failed:
          if (Buffer[HEADERSIZE + MSG_N - 1] != checksum(HEADERSIZE + MSG_N - 1)) 
          {
      	    // checksum error => set error and return with expected response size:
            Error = ERR_CHECKSUM;
            // in case of error, response with error byte will be sent:
            Buffer[0] = Error;
      	    return MSG_R;
          }
          
          // dispatch according to message id:
          byte err = ERR_NONE;
          switch(MSG_Id) 
    	    {
    	      case MSG_ID_NODE_ADMIN:
    	        {
    	          err = NODE_ProcessMessage();
                break; // continue with response => checksum and send data 
    	        }
    
    	      case MSG_ID_DEVICE_CHANNEL_1:
    	      case MSG_ID_DEVICE_CHANNEL_2:
    	      case MSG_ID_DEVICE_CHANNEL_3:
    	      case MSG_ID_DEVICE_CHANNEL_4:
    	        { // first device accessed with index 1:
                err = DEVICE_ProcessMessage(MSG_Id - MSG_ID_DEVICE_CHANNEL_1 + 1);
    	          break; // continue with response => checksum and send data 
    	        }
       
    	      default:
    	        {     	          
                Error = ERR_UNKNOWN_ID;
                // in case of error, response with error byte will be sent:
  	            Buffer[0] = Error;
  	            return MSG_R;
    	        }
    	    } // switch (MSG_Id)...default:
  
          // set response error byte in buffer:
          Buffer[0] = err;
          if (err == ERR_NONE) {
            // calculate response checksum and store in buffer:
            Buffer[MSG_R - 1] = checksum(MSG_R - 1);
          } else {
            // in case of error, save error byte in Error:
            Error = err;
          }      	          
          // return response length MSG_R:
          return MSG_R;
        } // switch(MSG_Id) ...
      }  // switch (MSG_Id) ... 
    return 0;    
    
  } // byte MSG_Dispatcher(byte MSG_Id) { ...


///
///  Calculate checksum across a byte array:
///
//  ---------------------------
    byte checksum(int length) {
//  ---------------------------
//   Calculate Checksum for length bytes starting from Buffer[0],
//   invert result and answer it.
      byte cs = Buffer[0];
      for (int i = 1; i < length; i++) { cs = cs + Buffer[i]; }
      cs = ~cs;
      return cs;
    }
    
// ============================================================================
// ==================================  E O F  =================================
// ============================================================================
