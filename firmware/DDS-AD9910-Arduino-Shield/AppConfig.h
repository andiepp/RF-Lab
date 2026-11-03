// ============================================================================
// ===                                                                      ===
// ===          A P P L I C A T I O N   C O N F I G U R A T I O N           ===
// ===                                                                      ===
// ============================================================================

#pragma once 
#define VERSION "1.0"

// --------------------------------------
// ===   Module Usage Configuration   ===
// --------------------------------------
// 1 = use module, 0 = ignore module
#define USE_MODULE_TEMPLATE            0
#define USE_ENCODER                    0
#define USE_SI5351                     1

//debug messsages 
// 0 - disabled, 
// 1 - enabled,
// 2 - verbuous
#define DBG                            1 

// --------------------
// ===   Includes   ===
// --------------------
#include <Arduino.h>
#include <EEPROM.h>
#include "MCNF_Defines.h"
#include "si5351_lite.h"

// ------------------------
// ===   Configuration  ===
// ------------------------
// ===   System   ===
#define LEDsystem                      A4
#define LEDred                         A10
#define LEDgreen                       A11
// ===   Communication  ===
#define ComHost                        Serial
#define BAUDRATE_HOST                  115200
///  MCNF uffer size
#define BUFFER_SIZE                    128
///
///  AnalogMeasurement Pin
///
#define PIN_MEASUREMENT                A8      

// -------------------------
// ===   MGEN-Settings   ===
// -------------------------

#define DDH47                          1
#define NAVTEX_DE                      2
#define NAVTEX_EN                      3
#define AUDIO                          4
#define USERDEFINED                    5

// center frequencies and shift:
#define DDH47_CENTER                   147300.0
#define DDH47_SHIFT                    85

#define NAVTEX_DE_CENTER               490000.0
#define NAVTEX_EN_CENTER               518000.0
#define NAVTEX_SHIFT                   170.0

// ----------------
// ===   MCNF   ===
// ----------------
#define NODE_IMPLEMENTOR            0xAE    // AEP
#define NODE_PLATFORM               0x70    // Arduino ATmega2560 MCNF id
#define NODE_APPLICATION_ID         0x10    // RFLab MCNF id
#define NODE_CONFIGURATION          0x80    // UART input

#define COM_TIMEOUT                 500

///  Error byte:
static volatile byte Error = ERR_NONE;

///  Message buffer:
static byte Buffer[BUFFER_SIZE];

///  UART input communication timeout definitions:
#define COM_TIMEOUT_DISABLED        0

///  Communication timeout start timestamp:
static volatile long COM_Timeout_0 = COM_TIMEOUT_DISABLED;
    

// -----------------------
// ===   Enumerations  ===
// -----------------------

// ---------------------------
// ===   Data Definitions  ===
// ---------------------------


// --------------------
// ===   Functions  ===
// --------------------
// === System ===
void DisplayMsg(String msg);
// === DDS AD9910 ===
void F_Sweep(uint32_t StartSweepFreq, uint32_t StopSweepFreq, uint16_t SweepTime, uint8_t SweepTimeFormat, int16_t Amplitude_dB, byte RampMode);

void initializeMeasuremets();

// ============================================================================
// ==================================  E O F  =================================
// ============================================================================