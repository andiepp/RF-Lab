// ============================================================================
// ===                                                                      ===
// ===                  M C N F   N O D E   F U N C T I O N S               ===
// ===                                                                      ===
// ============================================================================
///
///  Process Node Messages:
///  - NODE_Initialize() initializes node settings
///  - byte NODE_ProcessMessage() processes node messages and returns error byte
///    - NODE_GET_NODETYPEINFO sneds the 4 bytes node tyoe info based on
///      values in NODE_IMPLEMENTOR, NODE_PLATFORM, NODE_APPLICATION_ID 
///     and NODE_CONFIGURATION
///    - NODE_GET_BUFFER_SIZE sends the Buffer size as one byte message
///    - NODE_GET_ERROR sends the latest error stored in Error and resets Error
///

// ============================================================================
// ===                                                                      ===
// ===                     N O D E   I N I T I A L I Z E                    ===
// ===                                                                      ===
// ============================================================================

//  ------------------------ 
    void NODE_Initialize() {   
//  ------------------------
    }


// ============================================================================
// ===                                                                      ===
// ===               P R O C E S S   N O D E   M E S S A G E S              ===
// ===                                                                      ===
// ============================================================================
///
///  Process Node Messages:
///  - NODE_GET_BUFFER_SIZE answer the Buffer size
///  - NODE_GET_ERROR answer the latest error stored in Error and reset Error
///
//  ----------------------------
    byte NODE_ProcessMessage() {
//  ----------------------------

      // switch to node function execution: 
      switch (Buffer[FUNCTION_INDEX])
      {
          ///////////////////////////////
          // node admin function codes //
          ///////////////////////////////
          case NODE_GET_NODETYPEINFO: // --- Get NodeTypeInfo --- //
              {
                 Buffer[1] = NODE_IMPLEMENTOR;  
                 Buffer[2] = NODE_PLATFORM;
                 Buffer[3] = NODE_APPLICATION_ID;
                 Buffer[4] = NODE_CONFIGURATION;
                 return ERR_NONE ;
              }
          
                   
          case NODE_GET_BUFFER_SIZE: // --- Get Buffer Size --- //
              {
                 Buffer[1] = BUFFERSIZE;
                 return ERR_NONE ;
              }
          
          case NODE_GET_ERROR: // --- Get Error byte and reset Error --- //
              {
                 // answer the Error byte and reset Error to ERR_NONE:
                 Buffer[1] = Error;
                 Error = ERR_NONE;
                 return ERR_NONE ;
              }
              
          /////////////////////////////////
          // add other functions here:
          /////////////////////////////////
          
          default:
              {
                break;
              }
      }
      return ERR_UNKNOWN_FUNCTION;
    }
    
// ============================================================================
// ==================================  E O F  =================================
// ============================================================================
