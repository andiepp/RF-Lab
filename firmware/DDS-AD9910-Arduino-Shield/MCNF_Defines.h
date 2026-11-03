// ============================================================================
// ===                                                                      ===
// ===                        M C N F   D E F I N E S                       ===
// ===                                                                      ===
// ============================================================================
///
///  MCNF DEFINES:
///  - System Constants
///  - Error Types
///  - MCNF Message Constants 
///  - MCNF Message Id Types
///  - MCNF Function Types
///  - Message Source Types


#ifndef MCNF_Defines_h
#define MCNF_Defines_h

/////////////////////////
// MCNF System Constants
/////////////////////////
#define TIMEOUT 			2000 	// standard timeout

///////////////
// Error Types
///////////////
#define ERR_NONE                    	0
#define ERR_CHECKSUM                	1
#define ERR_UNKNOWN_ID     	        2
#define ERR_UNKNOWN_FUNCTION         	3
#define ERR_VARIABLE_INDEX          	4
#define ERR_VARIABLE_DATA            	5
#define ERR_MESSAGE_TYPE             	6
#define ERR_BUFFER_OVERFLOW          	7
#define ERR_SYNC_MISSING           	8
#define ERR_WRITE_TO_PORT           	9
#define ERR_CONNECTION_LOST          	10
#define ERR_RX_TIMEOUT                	11
#define ERR_PERMANENT_STORAGE_MISSING 	12
#define ERR_GATEWAY_TIMEOUT     	13
#define ERR_GATEWAY_RX           	14

//////////////////////////
// MCNF Message Constants
//////////////////////////
#define HEADERSIZE  			4	// header size incl. ID byte
#define FUNCTION_INDEX  		4	// 
#define BUFFERSIZE  			64 	// buffer size
#define BUFFERSIZE_RX 			64 	// receive buffer size
#define BUFFERSIZE_TX 			64 	// send buffer size
#define MSG_SYNC 			0xA0 	// sync pattern
#define MSG_SYNC_MASK 			0xF0	// mask to extract sync nibble
#define MSG_ID_MASK 			0x0F 	// mask to extract ID nibble
#define RESPONSE_CONNECTIVITY_TEST_CRP 	0x21 	// Connectivity Test response

/////////////////////////
// MCNF Message Id Types
/////////////////////////
#define MSG_ID_CONNECTIVITY_TEST        0x01
#define MSG_ID_SPEED_TEST 		0x03
#define MSG_ID_NODE_ADMIN 		0x04
#define MSG_ID_DEVICE_CHANNEL_1 	0x05
#define MSG_ID_DEVICE_CHANNEL_2 	0x06
#define MSG_ID_DEVICE_CHANNEL_3 	0x07
#define MSG_ID_DEVICE_CHANNEL_4 	0x08
#define MSG_ID_DEVICE_CHANNEL_X 	0x09
#define MSG_ID_GATEWAY_CHANNEL_1 	0x0A
#define MSG_ID_GATEWAY_CHANNEL_2 	0x0B
#define MSG_ID_GATEWAY_CHANNEL_3 	0x0C
#define MSG_ID_GATEWAY_CHANNEL_4 	0x0D
#define MSG_ID_GATEWAY_CHANNEL_X 	0x0E

///////////////////////
// MCNF Function Types
///////////////////////
#define FUNC_WRITE_CONFIG          	200
#define FUNC_READ_CONFIG           	201
#define FUNC_SET_VARIABLE          	202
#define FUNC_GET_VARIABLE          	203
#define FUNC_WRITE_VARSET          	204
#define FUNC_READ_VARSET           	205
#define FUNC_SAVE_VARSET          	206
#define FUNC_LOAD_VARSET           	207
#define FUNC_SET_BF_REGISTERS      	208
#define FUNC_GET_BF_REGISTERS       209
///
#define FUNC_SERVICE_REQUEST        0xF0
///
///  User-defined node function codes:
///
#define NODE_GET_NODETYPEINFO           220
#define NODE_GET_BUFFER_SIZE            221     
#define NODE_GET_ERROR                  222    

/////////////////////////
// Message Sources Types              
/////////////////////////
#define MSG_NONE  			0
#define MSG_UART  			1
#define MSG_TCP  			2
#define MSG_I2C  			3
#define MSG_SPI  			4

///////////////////////////
// EEPROM Access Templates             
///////////////////////////

template <class T> int EEPROM_writeStruct(int ee, const T& value)
{
    const byte* p = (const byte*)(const void*)&value;
    int i;
    for (i = 0; i < sizeof(value); i++) EEPROM.write(ee++, *p++);
    return i;
}

template <class T> int EEPROM_readStruct(int ee, T& value)
{
    byte* p = (byte*)(void*)&value;
    int i;
    for (i = 0; i < sizeof(value); i++) *p++ = EEPROM.read(ee++);
    return i;
}
  
#endif // MCNF_Defines_h

// ============================================================================
// ==================================  E O F  =================================
// ============================================================================
