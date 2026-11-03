// ============================================================================
// ===                                                                      ===
// ===                             D E V I C E                              ===
// ===                                                                      ===
// ============================================================================
///
///  Device drives LEDsystem. It provides functions to switch the
///  LED on and off as well as reading its current state. The mandatory 
///  standard function FUNC_READ_CONFIG reports the number of variable sets
///  as zero since we have no sets implemented and the LED's state.
///  - LED_On
///  - LED_Off
///  - LED_GetStatus
///  - FUNC_READ_CONFIG
///    - N_VarSets = 0
///    - ActiveVarSet = 0
///    - Capabilities = 0
///    - LED state (0 = off, 1 = on)

// ============================================================================
// ===                                                                      ===
// ===                            D E F I N E S                             ===
// ===                                                                      ===
// ============================================================================

#include "MCNF_Defines.h"

#define ERR_SI5351_DISCONNECTED  0x10
#define ERR_SI5351_CHANNEL       0x11

///
///  User-defined function codes:
///
#define func_LED_On              0x01      // switch LED on
#define func_LED_Off             0x02      // switch LED off
#define func_LED_GetState        0x03      // read LED state

#define func_DDS_Set_F_A         0x10      // set DDS frequency and amplitude
#define func_DDS_Set_AM          0x11      // set AM parameters and activate AM mode
#define func_DDS_Set_FM          0x12      // set FM parameters and activate FM mode
#define func_DDS_Sweep           0x13      // set SWEEP parameters and activate mode

#define func_MEASURE_ReadSensor  0x20      // read sensor value

#define func_MGEN_SetMode        0x30      // set simulator mode
#define func_MGEN_SetParameter   0x31      // set simulator parameters
#define func_MGEN_Send           0x32      // send test message / pattern

#define func_SI5351_SetCLKx      0x40      // set CLK_x of SI5351

#define DDS_SweepTimeFormatMS    0x01      // sweep time in [ms]


// ============================================================================
// ===                                                                      ===
// ===                   D E V I C E   F U N C T I O N S                    ===
// ===                                                                      ===
// ============================================================================
//  ------------------------------ 
    void initializeMeasuremets() {   
//  ------------------------------
      analogReference(INTERNAL2V56); 
    }

//  -------------------
    void readSensor() {   
//  -------------------
      SensorValue = analogRead(PIN_MEASUREMENT); 
    }



// ============================================================================
// ===                                                                      ===
// ===                  D I S P L A Y   F U N C T I O N S                   ===
// ===                                                                      ===
// ============================================================================

//  ---------------------------
    void InitalRFLabDisplay() {
//  ---------------------------
      display.clearDisplay();
      display.setTextSize(2);      
      display.setTextColor(WHITE); 
      display.setCursor(0, 0);     
      display.cp437(true);         
      display.print(F("RFLab"));
      display.setTextSize(1);      
      display.setTextColor(WHITE); 
      display.setCursor(70, 8);     
      display.println(F("Rev 1.0"));
      display.setCursor(0, 25);     
      display.print(F("- connected -"));
      display.setCursor(0, 45);     
      display.print(F("- MCNF application"));
      display.setCursor(0, 55);     
      display.print(F("- AEP 06/21/2021"));
      display.display();
    }

// ============================================================================
// ===                                                                      ===
// ===                      L E D   F U N C T I O N S                       ===
// ===                                                                      ===
// ============================================================================

//  --------------------------------- 
    void setLEDState(byte LEDState) {
//  --------------------------------- 
//  - if LEDState = 0 => set LED to off
//  - if LEDState > 0 => set LED to on
  if (LEDState)
  {
    digitalWrite(LEDgreen,HIGH);
    #if (DEBUG > 0)
       debug.println(" - LED on");
    #endif
  }
  else
  {
    digitalWrite(LEDgreen,LOW);
    #if (DEBUG > 0)
       debug.println(" - LED off");
    #endif
  }
}

//  --------------------
    byte getLEDState() {
//  -------------------- 
//  - answer 0 if LED is off else answer 1
  if( digitalRead(LEDgreen) > 0) return 1; else return 0;
}

// ============================================================================
// ===                                                                      ===
// ===                      D D S   F U N C T I O N S                       ===
// ===                                                                      ===
// ============================================================================

//  ------------------------------------------------------------------------------- 
    void setFrequencyAmplitude(uint32_t freq_output, int16_t amplitude_dB_output) {
//  ------------------------------------------------------------------------------- 
      SingleProfileFreqOut(freq_output, amplitude_dB_output);
}

// ============================================================================
// ===                                                                      ===
// ===                   D E V I C E   I N I T I A L I Z E                  ===
// ===                                                                      ===
// ============================================================================

//  --------------------------
    void DEVICE_Initialize() {   
//  --------------------------
//  - set LED to initial off
    #if (DEBUG > 0)
       debug.println(" - Device_Switch init");
    #endif
    setLEDState(0x00);      
}


// ============================================================================
// ===                                                                      ===
// ===            D E V I C E   F U N C T I O N   H A N D L E R             ===
// ===                                                                      ===
// ============================================================================
///
/// The global variable "Buffer" contains the command information:
///    - Buffer[0]     ... contains function id
///    - Buffer[1...]  ... contains function parameters
/// Implemented standard function:
///    - FUNC_READ_CONFIG   ... answer the LED state
/// Implemented user-defined functions:
///    - func_LED_On         switch LED on
///    - func_LED_Off        switch LED off
///    - func_LED_GetState   get LED state
/// The result is passed in variable "Buffer":
///    - Buffer[0]     ... contains error information 
///    - Buffer[1]     ... contains result data = LED state (bit 0)
///                    ... and SI5351 state (bit 1)

//  ------------------------------
    byte DEVICE_ProcessMessage() {
//  ------------------------------
      
      // switch to function execution: 
      switch(Buffer[FUNCTION_INDEX]) {   
        
        //-------------------------------//
        //      Standard Functions       //
        //-------------------------------//
        case(FUNC_READ_CONFIG): 
        {             
          DisplayMsg("Host connected");
          Buffer[1] = 0;                   // N_VarSets
          Buffer[2] = 0;                   // ActiveVarSet
          Buffer[3] = 0;                   // Capabilities
          Buffer[4] = 0;                   // default
          if (getLEDState())
            bitSet(Buffer[4],0);           // LED state (bit 0)
          if (si5351_connected)            // SI5351 state (bit 1)
            bitSet(Buffer[4],1);         
          return ERR_NONE;
        }

        //-----------------------------------//
        //    User-defined Functions: LED    //
        //-----------------------------------// 
              
        // --- LED_On --- //
        case(func_LED_On):                    
        {   
          setLEDState(1); 
          Buffer[1] = getLEDState(); 
          return ERR_NONE;
        }
        
        // --- LED_Off --- //
        case(func_LED_Off):                 
        {   
          setLEDState(0); 
          Buffer[1] = getLEDState(); 
          return ERR_NONE;
        }
        
        // --- LED_GetState --- //
        case(func_LED_GetState):          
        {   
          #if DEBUG==1
             debug.println(" - Device:\n\r - LED_GetState");
          #endif
          Buffer[1] = getLEDState(); 
          return ERR_NONE;
        }


        //-----------------------------------//
        //    User-defined Functions: DDS    //
        //-----------------------------------// 

        // --- DDS_Set_F_A --- //
        case(func_DDS_Set_F_A):                 
        {   
          uint32_t ddsFHz = Buffer[5]; 
          ddsFHz = 256 * ddsFHz + Buffer[6]; 
          ddsFHz = 256 * ddsFHz + Buffer[7]; 
          ddsFHz = 256 * ddsFHz + Buffer[8]; 
          int16_t ddsAdB = Buffer[9];
          ddsAdB = 256 * ddsAdB + Buffer[10];     
          ddsAdB = - ddsAdB;
          #if DEBUG==1
             debug.print(" - Device:\n\r - F[Hz] = ");
             debug.println(ddsFHz);
             debug.print(" - A[dB] = ");
             debug.println(ddsAdB);
          #endif
          // void SingleProfileFreqOut(uint32_t freq_output, int16_t amplitude_dB_output)
          SingleProfileFreqOut(ddsFHz, ddsAdB);
          return ERR_NONE;
        }

        // --- DDS_Set_AM --- //      
        case(func_DDS_Set_AM):                 
        {   
          uint32_t F_carrier = Buffer[5]; 
          F_carrier = 256 * F_carrier + Buffer[6]; 
          F_carrier = 256 * F_carrier + Buffer[7]; 
          F_carrier = 256 * F_carrier + Buffer[8]; 
          int16_t ddsAdB = Buffer[9];
          ddsAdB = 256 * ddsAdB + Buffer[10];    
          ddsAdB = - ddsAdB;
          uint32_t F_mod = Buffer[11]; 
          F_mod = 256 * F_mod + Buffer[12]; 
          F_mod = 256 * F_mod + Buffer[13]; 
          F_mod = 256 * F_mod + Buffer[14];
          uint32_t AM_depth = Buffer[15]; 
          AM_depth = 256 * AM_depth + Buffer[16]; 
          AM_depth = 256 * AM_depth + Buffer[17]; 
          AM_depth = 256 * AM_depth + Buffer[18];     
          #if DEBUG==1
             debug.print(" - Device:\n\r - F_carrier = ");
             debug.println(F_carrier);
             debug.print(" - A[dB] = ");
             debug.println(ddsAdB);
             debug.print(" - F_mod = ");
             debug.println(F_mod);
             debug.print(" - AM_depth = ");
             debug.println(AM_depth);
          #endif
          // void SaveAMWavesToRAM(uint32_t F_carrier, uint32_t F_mod, uint32_t AM_DEPTH, int16_t Amplitude_dB)
          SaveAMWavesToRAM(F_carrier, F_mod, AM_depth, ddsAdB);
          // void PlaybackAMFromRAM(uint32_t F_carrier)
          PlaybackAMFromRAM(F_carrier);
          return ERR_NONE;
        }
  
        // --- DDS_Set_FM --- //      
        case(func_DDS_Set_FM):                 
        {   
          uint32_t F_carrier = Buffer[5]; 
          F_carrier = 256 * F_carrier + Buffer[6]; 
          F_carrier = 256 * F_carrier + Buffer[7]; 
          F_carrier = 256 * F_carrier + Buffer[8]; 
          int16_t ddsAdB = Buffer[9];
          ddsAdB = 256 * ddsAdB + Buffer[10];    
          ddsAdB = - ddsAdB;
          uint32_t F_mod = Buffer[11]; 
          F_mod = 256 * F_mod + Buffer[12]; 
          F_mod = 256 * F_mod + Buffer[13]; 
          F_mod = 256 * F_mod + Buffer[14];
          uint32_t F_dev = Buffer[15]; 
          F_dev = 256 * F_dev + Buffer[16]; 
          F_dev = 256 * F_dev + Buffer[17]; 
          F_dev = 256 * F_dev + Buffer[18];     
          #if DEBUG==1
             debug.print(" - Device:\n\r - F_carrier = ");
             debug.println(F_carrier);
             debug.print(" - A[-dB] = ");
             debug.println(ddsAdB);
             debug.print(" - F_mod = ");
             debug.println(F_mod);
             debug.print(" - F_dev = ");
             debug.println(F_dev);
          #endif
          // void SaveFMWavesToRAM (uint32_t F_carrier, uint32_t F_mod, uint32_t F_dev)
          SaveFMWavesToRAM(F_carrier, F_mod, F_dev);
          // void PlaybackFMFromRAM(int16_t Amplitude_dB)
          PlaybackFMFromRAM(ddsAdB);
          return ERR_NONE;
        }

        // --- DDS_Sweep --- //      
        case(func_DDS_Sweep):                 
        {   
          uint32_t F_start = Buffer[5]; 
          F_start = 256 * F_start + Buffer[6]; 
          F_start = 256 * F_start + Buffer[7]; 
          F_start = 256 * F_start + Buffer[8]; 
          uint32_t F_stop = Buffer[9]; 
          F_stop = 256 * F_stop + Buffer[10]; 
          F_stop = 256 * F_stop + Buffer[11]; 
          F_stop = 256 * F_stop + Buffer[12];
          uint16_t SweepTime = Buffer[13]; 
          SweepTime = 256 * SweepTime + Buffer[14];      
          int16_t ddsAdB = Buffer[15];
          ddsAdB = 256 * ddsAdB + Buffer[16];    
          ddsAdB = - ddsAdB;
          byte rampMode = Buffer[17];          
          #if DEBUG==1
             debug.print(" - Device:\n\r - F_start = ");
             debug.println(F_start);
             debug.print(" - F_stop = ");
             debug.println(F_stop);
             debug.print(" - SweepTime = ");
             debug.print(SweepTime);
             debug.println(" [ms]");
             debug.print(" - A[dB] = ");
             debug.println(ddsAdB);
             debug.print(" - mode = ");
             debug.println(rampMode);
          #endif
          // SweepTimeFormat: 0 - Seconds, 1 - Milliseconds (mS), 2 - MicroSeconds (uS), 3 - NanoSeconds
          // void Sweep(uint32_t StartSweepFreq, uint32_t StopSweepFreq, uint16_t SweepTime, uint8_t SweepTimeFormat, int16_t Amplitude_dB)
          // Sweep(F_start, F_stop, SweepTime, DDS_SweepTimeFormatMS, ddsAdB, rampMode);
          F_Sweep(F_start, F_stop, SweepTime, DDS_SweepTimeFormatMS, ddsAdB, rampMode);
          return ERR_NONE;
        }
        
        //---------------------------------------//
        //    User-defined Functions: MEASURE    //
        //---------------------------------------// 
        case(func_MEASURE_ReadSensor):                 
        {   
          // uint32_t F_start = Buffer[5]; 
          readSensor();
          #if DEBUG==1
             debug.println(" - Device:\n\r - read sensor");
          #endif
          Buffer[1] = highByte(SensorValue); 
          Buffer[2] = lowByte(SensorValue); 
          return ERR_NONE;
        }

        //-------------------------------------//
        //    User-defined Functions: MGEN     //
        //-------------------------------------// 
        case(func_MGEN_SetMode):                 
        {   
          Mode = Buffer[5]; 
          return ERR_NONE;
        }

        case(func_MGEN_SetParameter):                 
        {   
          F_CENTER = Buffer[5]; 
          F_CENTER = 256 * F_CENTER + Buffer[6]; 
          F_CENTER = 256 * F_CENTER + Buffer[7]; 
          F_CENTER = 256 * F_CENTER + Buffer[8]; 
          SHIFT = Buffer[9];
          SHIFT = 256 * SHIFT + Buffer[10];     
          REPEATFACTOR = Buffer[11]; 
          REPEATFACTOR = 256 * REPEATFACTOR + Buffer[12];     
          PatternAmplitude = Buffer[13];
          PatternAmplitude = 256 * PatternAmplitude + Buffer[14];     
          PatternAmplitude = - PatternAmplitude;  
          BAURATE = Buffer[15];        
          BAURATE = 256 * BAURATE + Buffer[16];     
          #if DEBUG==1
             debug.print(" - Device:\n\r - Fcenter = ");
             debug.println(F_CENTER);
             debug.print(" - Fshift = ");
             debug.println(SHIFT);
             debug.print(" - A[dB] = ");
             debug.println(PatternAmplitude);
             debug.print(" - Baudrate = ");
             debug.println(BAURATE);
             debug.print(" - Repeat = ");
             debug.println(REPEATFACTOR);
          #endif
          Fmark = (double)F_CENTER + (SHIFT / 10.0);
          Fspace = (double)F_CENTER - (SHIFT / 10.0);        
          SingleProfileFreqOut(F_CENTER, PatternAmplitude);
          return ERR_NONE;
        }

        case(func_MGEN_Send):                 
        {   
          SendRequest = Buffer[5]; 
          REPEATFACTOR = Buffer[6]; 
          REPEATFACTOR = 256 * REPEATFACTOR + Buffer[7]; 
          #if DEBUG==1
             debug.print(" - Device:\n\r - send pattern = ");
             debug.println((char)SendRequest);
             debug.print(" - repeat = ");
             debug.println(REPEATFACTOR);
          #endif
          return ERR_NONE;    
        }       

        //---------------------------------------//
        //    User-defined Functions: SI5351     //
        //---------------------------------------// 
        case(func_SI5351_SetCLKx):                 
        { 
          if (USE_SI5351) {
            if (!si5351_connected) return ERR_SI5351_DISCONNECTED;
            uint8_t clkNo = Buffer[5]; // 0, 1 or 2
            if (clkNo > 2) return ERR_SI5351_CHANNEL;
            unsigned long long F_clk = Buffer[6]; // 64 bit MSB first
            F_clk = 256 * F_clk + Buffer[7]; 
            F_clk = 256 * F_clk + Buffer[8]; 
            F_clk = 256 * F_clk + Buffer[9]; 
            F_clk = 256 * F_clk + Buffer[10]; 
            F_clk = 256 * F_clk + Buffer[11]; 
            F_clk = 256 * F_clk + Buffer[12]; 
            F_clk = 256 * F_clk + Buffer[13]; 
            
            if (clkNo == 0) si5351.set_freq(F_clk, SI5351_CLK0);
            if (clkNo == 1) si5351.set_freq(F_clk, SI5351_CLK1);
            if (clkNo == 2) si5351.set_freq(F_clk, SI5351_CLK2);
            si5351.update_status();
            
            #if DEBUG==1
              debug.print(" - Device:\n\r - SI5351 CLK_ = ");
              debug.println(clkNo);
              debug.print(" - F_clk = ");
              uint32_t FHz = (uint32_t)(F_clk/100);
              debug.print(FHz);
              debug.println("Hz");
            #endif
          }
          return ERR_NONE;
        }
        
        // --- Default => Unknown Function --- //
        default: 
        {
          break;
        } 
        
      } // end switch(Buffer[FUNCTION_INDEX]) ...
      
      //----------------------------------------------//
      //       Return Error Code Unknown Function     //
      //----------------------------------------------//
      #if DEBUG==1
         debug.println(" - Device: Unknown Function");
      #endif
      return ERR_UNKNOWN_FUNCTION;
      
  } // byte DEVICE_ProcessMessage() { ...

// ============================================================================
// ==================================  E O F  =================================
// ============================================================================
