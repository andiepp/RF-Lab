// ****************************************************************************
// ***                                                                      ***
// *** ARDUINO:   RFLab - MCNF ARDUINO PROJECT - UART HOST CONNECTION       ***
// ***                                                                      ***
// ****************************************************************************
///
///  HEADER:
///  - Author:       Dr. Andreas Eppinger
///  - Version:      1.1
///  - File:         RFLab_Arduino_Firmware.ino
///  - Target:       AVR ATmega2560
///  - Compiler:     Arduino 2.3.5
///  - Libs:         https://github.com/PU2REO/Si5351ArduinoLite
///  - History:      - 2025-09-10: initial
/// 
///  DESCRIPTION:
///  - RFLab_Arduino_Firmware           =>    Configuration and Setup (add devices to DEVICE_ProcessMessage(...))
///  - App_Main_Loop                    =>    loop() function manages MCNF message communication
///  - App_Device                       =>    Device functions
///  MCNF core functions that should not be changed: 
///  - MCNF_COM_Rx                      =>    MCNF message receiving and package builder (do NOT change)
///  - MCNF_Defines.h                   =>    Standard MCNF Defines and Templates (do NOT change)
///  - MCNF_MessageDispatcher           =>    Message Dispatcher (do NOT change)
///  - MCNF_Node_Functions              =>    Node functions
///
///  DWD(Deutscher Wetterdienst)/NAVTEX-MESSAGE-GENERATOR:
///
///     - DDH47 long wave @ 147.3 KHz +/- 42.5 Hz FSK 
//        using RTTY (reverse) Baudot code @ 50 baud rate
///
///     - NAVTEX 490 KHz in German and NAVTEX 518 KHz in English
///       using SITOR-B format and 7-bit CCIR 476 character set encoding
///       @ 100 baud rate
///
///     - AUDIO 1.5 KHz - FSK shift, message format and baudrate
///       as defined by either DDH47 or NAVTEX mode
///
///     - User-defined center frequency and frequency shift, 
///       message format and baudrate as defined by either 
///       DDH47 or NAVTEX mode
///
///     - The following digital pattern can be sent for test purpose:
///         - 0 (space) frequency
///         - 1 (mark) frequency
///         - 1110000
///         - 0000001
///         - 1111110
///         - 1010101
///         - 10101010101010
///         
///     - the messages can be repeated r times by setting the r factor
///
///     - there is an option to generate NAVTEX messages with errors 
///       for realistic testing
///
///  FIRMWARE MODULES (Tabs):
///  - BaudotConverter    convert from ASCII character to Baudot code
///  - CCIR476Converter   convert from ASCII character to CCIR476 code
///  - DDS9850            set DDS frequency
///  - Message            generate and send messages
///     
///  HARDWARE:
///  - Arduino (ATmega328P, 16 MHz)
///  - LED at digital pin PIN_SYSTEM_LED
///     
///  LIBRARIES:
///  - Firmware by supplier of AD9910 board (GRA-AFCH.COM)
//
// ----------------------------------------------------------------------------
// LICENSE: GNU General Public License
//  Copyright (C) 2025 Andreas Eppinger
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  any later version.
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// ----------------------------------------------------------------------------

// -------------------
// ===   Includes  ===
// -------------------
#include "AppConfig.h"
#include "main.h"
#include "ad9910.h"
#include "GyverTimers.h"
#include <GParser.h>

// ------------------------
// ===   Configuration  ===
// ------------------------
#define FIRMWAREVERSION 3.20

#define LOW_FREQ_LIMIT  100000
#define HIGH_FREQ_LIMIT  600000000
// 0 - 20, 64 used for clock settings
#define M_ADR 24
#define K_ADR 28
#define H_ADR 32
#define A_ADR 36

#define MOD_INDEX_ADR 40
#define MOD_FREQK_ADR 44
#define MOD_FREQH_ADR 48
#define MOD_AM_DEPTH_ADR 52
#define MOD_FM_DEVK_ADR 56
#define MOD_FM_DEVH_ADR 60

#define SWEEP_START_FREQ_M_ADR 66
#define SWEEP_START_FREQ_K_ADR 68
#define SWEEP_START_FREQ_H_ADR 70

#define SWEEP_END_FREQ_M_ADR 72
#define SWEEP_END_FREQ_K_ADR 74
#define SWEEP_END_FREQ_H_ADR 76

#define SWEEP_TIME_ADR 78
#define SWEEP_TIME_FORMAT_ADR 80

#define S_CURVE_CARRIER_ADR 82
#define S_CURVE_BANDWITH_ADR 86

#define MAIN_SETTINGS_FLAG_ADR 100 
// ADR 101 reserved for clock settings
#define MODULATION_SETTINGS_FLAG_ADR 102

#define LO_SETTINGS_ADR 104
#define LO_SETTINGS_FLAG_ADR LO_SETTINGS_ADR + 11

#define INIT_M 100
#define INIT_K 0
#define INIT_H 0
#define INIT_A 0

#define NONE_MOD_TYPE     0
#define AM_MOD_TYPE       1
#define FM_MOD_TYPE       2
#define SWEEP_MOD_TYPE    3
#define S_CURVE_MOD_TYPE  4
#define LO_MOD_TYPE       5

#define INIT_MOD_INDEX 0
#define INIT_MFREQ_K 1
#define INIT_MFREQ_H 0
#define INIT_AM_DEPTH 50
#define INIT_FM_DEV_K 3
#define INIT_FM_DEV_H 0

#define INIT_SWEEP_START_FREQ_M 100
#define INIT_SWEEP_START_FREQ_K 0
#define INIT_SWEEP_START_FREQ_H 0

#define INIT_SWEEP_END_FREQ_M 200
#define INIT_SWEEP_END_FREQ_K 0
#define INIT_SWEEP_END_FREQ_H 0

#define INIT_SWEEP_TIME 1
#define INIT_SWEEP_TIME_FORMAT 0

#define INIT_S_CURVE_CARRIER 144000000
#define INIT_S_CURVE_BANDWITH 3 // kHz

#define MOD_MENU_TYPE_INDEX 0
#define MOD_MENU_MFREQ_K_INDEX 1
#define MOD_MENU_MFREQ_H_INDEX 2
#define MOD_MENU_DEPTH_DEV_K_INDEX 3
#define MOD_MENU_FM_DEV_H_INDEX 4

#define MOD_MENU_SWEEP_START_FREQ_M_INDEX 5
#define MOD_MENU_SWEEP_START_FREQ_K_INDEX 6
#define MOD_MENU_SWEEP_START_FREQ_H_INDEX 7

#define MOD_MENU_SWEEP_END_FREQ_M_INDEX 8
#define MOD_MENU_SWEEP_END_FREQ_K_INDEX 9
#define MOD_MENU_SWEEP_END_FREQ_H_INDEX 10

#define MOD_MENU_SWEEP_TIME_INDEX 11
#define MOD_MENU_SWEEP_TIME_FORMAT_INDEX 12

#define MOD_MENU_CARRIER_M_INDEX 13
#define MOD_MENU_CARRIER_K_INDEX 14
#define MOD_MENU_CARRIER_H_INDEX 15

#define MOD_MENU_BW_INDEX 16

#define MOD_MENU_SAVE_INDEX 17
#define MOD_MENU_EXIT_INDEX 18

#define MAIN_MENU_MHZ_INDEX         0
#define MAIN_MENU_KHZ_INDEX         1
#define MAIN_MENU_HZ_INDEX          2
#define MAIN_MENU_AMPLITUDE_INDEX   3
#define MAIN_MENU_MOD_INDEX         4
#define MAIN_MENU_LOCAL_OSC_INDEX   5

#define MAIN_MENU_LAST_INDEX MAIN_MENU_LOCAL_OSC_INDEX

#define VOLTAGE_DIVIDER_FACTOR 0.1754
#define ANALOG_REFERENCE_VOLTAGE 4950
#define INPUT_VOLTAGE_PIN A3 //VIN_MEAS 

#define DEFUALT_SWEEP_START_FREQ 100000
#define DEFUALT_SWEEP_END_FREQ 200000

#define MAX_SWEEP_FREQ 600000000UL
#define MIN_SWEEP_FREQ 100000

#define MIN_SWEEP_TIME 10 //10 nS

#define MAX_S_CURVE_CARRIER HIGH_FREQ_LIMIT
#define MIN_S_CURVE_CARRIER LOW_FREQ_LIMIT

#define MAX_S_CURVE_BANDWITH 300
#define MIN_S_CURVE_BANDWITH 3

#define LO_MENU_MHZ_INDEX       0
#define LO_MENU_KHZ_INDEX       1
#define LO_MENU_HZ_INDEX        2
#define LO_MENU_STEP_INDEX      3
#define LO_MENU_IF_SIGN_INDEX   4
#define LO_MENU_IF_INDEX        5
#define LO_MENU_EXIT_INDEX      6

#define LO_MENU_LAST_INDEX LO_MENU_EXIT_INDEX

// ----------------------
// ===   Global Data  ===
// ----------------------
Encoder myEnc(B_PIN, A_PIN);
int M, K, H, A, MenuPos;
int DBCorrection = 0;
bool isPWR_DWN = false;
int ModMenuPos = 0;
int ModIndex = 0;
bool EditMode = false;
String ModName[5] = {"None", "AM", "FM", "Sweep", "S-Curve"};
int MFreqK = 0;
int MFreqH = 0;
int AMDepth = 0;
int FMDevK = 0;
int FMDevH = 0;

int SweepStartFreqM = 0;
int SweepStartFreqK = 0;
int SweepStartFreqH = 0;
int SweepStartFreq = DEFUALT_SWEEP_START_FREQ;

int SweepEndFreqM = 0;
int SweepEndFreqK = 0;
int SweepEndFreqH = 0;
int SweepEndFreq = DEFUALT_SWEEP_END_FREQ;

int SweepTime = 1;
int SweepTimeFormat = 0;
String TimeFormatsNames[4] = {"S", "mS", "uS", "nS"};

int32_t S_CurveCarrier = 250000; //144000000; 
int32_t S_CurveBandwith = 300; //3; //in KhZ!!!

volatile int TRG_STATE = 0;

uint32_t UpButtonPressed = 0;
uint32_t DownButtonPressed = 0;
int increment = 1;
int decrement = 1;

int8_t curPos=0;

//int32_t LOCarrierFreq = 144000000;
uint32_t LOStep = 0;

int8_t LOMenuPos = 0;

uint32_t LOStepsList[] = {1000, 5000, 25000, 50000, 100000};
//int8_t LOStepsIndex = 1;
const uint8_t LOStepsQty = sizeof(LOStepsList) / sizeof(LOStepsList[0]);
uint32_t IFFreqList[] = {450000, 455000, 465000, 5500000, 6500000, 10700000, 21400000, 21700000, 38400000, 70000000, 90000000, 100000000};
//int8_t IFFreqListIndex = 0;
const uint8_t IFFreqQty = sizeof(IFFreqList) / sizeof(IFFreqList[0]);
uint32_t IFFreq = 0;
//int32_t RealCarrierFreq = 0; 
//int8_t IFSign = -1;
bool needToSaveLOSettings = false;
uint32_t lastTimeLOSettingsChanged = 0;

// ------------------
// ===   SI5351   ===
// ------------------
Si5351 si5351;
static bool si5351_connected = false;

// -------------------
// ===   MEASURE   ===
// -------------------
static int SensorValue = 0;

// ----------------
// ===   MGEN   ===
// ----------------
byte SendRequest = 0;
int Mode = DDH47;
long timestamp = 0;
bool isSendWithErrors = false;
double F_CENTER = DDH47_CENTER;
double SHIFT = DDH47_SHIFT;
int REPEATFACTOR = 1;
int BAURATE = 50;
double USER_CENTER = F_CENTER;
double USER_SHIFT = SHIFT;
double Fmark = F_CENTER + SHIFT;
double Fspace = F_CENTER - SHIFT;
int16_t PatternAmplitude = 0;


// -------------------
// ===   STRUCTS   ===
// -------------------
struct LOSettings
{
  int32_t LOCarrierFreq = 144000000;
  int8_t LOStepsIndex = 1;
  int8_t IFFreqListIndex = 0;
  int32_t RealCarrierFreq = 0; 
  int8_t IFSign = -1;
} loSettings;


// ============================================================================
// ===                                                                      ===
// ===                   S U P P O R T   F U N C T I O N S                  ===
// ===                                                                      ===
// ============================================================================

void ProcessMCNFMessage(byte rx) {
      // get byte received and call COM_Rx:
      // - if a complete MCNF message was received,
      //   the COM_Rx will call the message dispatcher
      byte N_Tx = COM_Rx(MSG_UART, rx);   
      // send bytes if available:
      if (N_Tx > 0) {     
        for (byte i=0; i<N_Tx; i++) 
        { 
          Serial.write(Buffer[i]); 
        }        
      } else {
        // if we were waiting for UART input, check timeout:
        if (COM_Timeout_0 != COM_TIMEOUT_DISABLED) {
          if ((millis() - COM_Timeout_0) > COM_TIMEOUT) {
            // timeout error:
            COM_Rx_Timeout();
          }
        }     
      }      
}

void MakeOut()
{ 
  StopSCurve();
  switch (ModIndex)
  {
    case NONE_MOD_TYPE: //модуляция отключена
      SingleProfileFreqOut(M * 1000000L + K * 1000L + H, A * -1);
      //SingleProfileFreqOut(1000, A*-1); //DELETE THIS
      break;
    case AM_MOD_TYPE: // AM амплитудная модуляция
      SaveAMWavesToRAM(M * 1000000L + K * 1000L + H, MFreqK * 1000L + MFreqH, AMDepth, A * -1);
      break;
    case FM_MOD_TYPE: // FM частотная модуляция
      SaveFMWavesToRAM(M * 1000000L + K * 1000L + H, MFreqK * 1000L + MFreqH, FMDevK * 1000L + FMDevH);
      break;
    case SWEEP_MOD_TYPE:
      Sweep(GetSweepStartFreq(), GetSweepEndFreq(), SweepTime, SweepTimeFormat);
      break;
    case LO_MOD_TYPE:
      SingleProfileFreqOut(loSettings.RealCarrierFreq, A * -1);
      break;
    case S_CURVE_MOD_TYPE:
      digitalWrite(TRG_LED_PIN, HIGH);
      Sweep(S_CurveCarrier - (S_CurveBandwith * 1000) / 2, S_CurveCarrier + (S_CurveBandwith * 1000) / 2, S_CurveBandwith, 1, false);
      //Sweep(100000, 400000, 3, 1, false);
      //Sweep(GetSweepStartFreq(), GetSweepEndFreq(), SweepTime, SweepTimeFormat);
      StartSCurve();
      break;
  }
}

int32_t Inc(int32_t val, int incr)
{
  //val = val + increment;
  //val = val + incrValue;
  val = val + incr;
  if (val > 999) val = 999;
  return val;
}

int32_t Dec(int32_t val, int decr)
{
  //val = val - decrement;
  //val = val - decrValue;
  val = val + decr;
  if (val < 0) val = 0;
  return val;
}

bool Check (int _M, int _K, int _H)
{
  long F_Val;
  F_Val = _M * 1000000L + _K * 1000L + _H;
  if ((F_Val >= LOW_FREQ_LIMIT) && (F_Val <= HIGH_FREQ_LIMIT)) return 1;
  else return 0;
}

void SaveMainSettings()
{
  EEPROM.put(M_ADR, M);
  EEPROM.put(K_ADR, K);
  EEPROM.put(H_ADR, H);
  EEPROM.put(A_ADR, A);
  EEPROM.write(MAIN_SETTINGS_FLAG_ADR, 55);
}

void LoadMainSettings()
{
  if (EEPROM.read(MAIN_SETTINGS_FLAG_ADR) != 55)
  {
    M = INIT_M;
    K = INIT_K;
    H = INIT_H;
    A = INIT_A;
    SaveMainSettings();
#if DBG==2
    Serial.println(F("Loading init values"));
    Serial.print("M=");
    Serial.println(M);
    Serial.print("K=");
    Serial.println(K);
    Serial.print("H=");
    Serial.println(H);
    Serial.print("A=");
    Serial.println(A);
#endif
  }
  else
  {
    EEPROM.get(M_ADR, M);
    EEPROM.get(K_ADR, K);
    EEPROM.get(H_ADR, H);
    EEPROM.get(A_ADR, A);
#if DBG==2
    Serial.println(F("Value from EEPROM"));
    Serial.print("M=");
    Serial.println(M);
    Serial.print("K=");
    Serial.println(K);
    Serial.print("H=");
    Serial.println(H);
    Serial.print("A=");
    Serial.println(A);
#endif
  }
}

int CalcDBCorrection()
{
  float ArmsREAL = (1 + AMDepth / 100.0) * 0.2236;
  float P_REAL = ArmsREAL * ArmsREAL / 50.0;
  DBCorrection = round(10 * log10(P_REAL / 0.001));
  return DBCorrection;
}

uint32_t GetSweepStartFreq()
{
#if DBG==2
  Serial.print("SweepStartFreqM=");
  Serial.println(SweepStartFreqM);
  Serial.print("SweepStartFreqK=");
  Serial.println(SweepStartFreqK);
  Serial.print("SweepStartFreqH=");
  Serial.println(SweepStartFreqH);
#endif
  return SweepStartFreqM * 1000000UL + SweepStartFreqK * 1000UL + SweepStartFreqH;
}

uint32_t GetSweepEndFreq()
{
  return SweepEndFreqM * 1000000UL + SweepEndFreqK * 1000UL + SweepEndFreqH;
}

void DisplayMessage(String Title, String Message)
{
  display.clearDisplay();
  display.cp437(true);
  display.setTextSize(2);
  display.setTextColor(WHITE); // Draw white text
  display.setCursor(0, 0);
  display.print(Title);
  display.setTextSize(1);
  display.setCursor(5, 28);
  display.print(Message);

  display.display();
}

void SetSweepStartFreq(uint32_t freq)
{
  SweepStartFreqH = freq % 1000;
  freq = freq / 1000;
  SweepStartFreqK = freq % 1000;
  freq = freq / 1000;
  SweepStartFreqM = freq;
}

void SetSweepEndFreq(uint32_t freq)
{
  SweepEndFreqH = freq % 1000;
  freq = freq / 1000;
  SweepEndFreqK = freq % 1000;
  freq = freq / 1000;
  SweepEndFreqM = freq;
}

bool IsSweepFreqsValid() //проверяет правильность введеных частот для свипа, и в случае обнаружения проблем исправляет их, в том числе меняет местами началььную и конечные частоты если, начальная больше конечной
{
  bool IsValid = true;
  uint32_t tempFreq;

  if (GetSweepStartFreq() > GetSweepEndFreq())
  {
    tempFreq = GetSweepStartFreq();
    SetSweepStartFreq(GetSweepEndFreq());
    SetSweepEndFreq(tempFreq);
    DisplayMessage("SWEEP", "Starting Frequency\r\n Higher Than\r\n Stop Frequency!"); //\r\n
    delay(3000);
    IsValid = false;
  }

  if (GetSweepStartFreq() > MAX_SWEEP_FREQ)
  {
    SetSweepStartFreq(MAX_SWEEP_FREQ);
    DisplayMessage("SWEEP", "Too High\r\n Start Frequency!");
    delay(2500);
    //ModMenuPos=MOD_MENU_SWEEP_START_FREQ_M_INDEX;
    IsValid = false;
  }

  if (GetSweepStartFreq() < MIN_SWEEP_FREQ)
  {
    SetSweepStartFreq(MIN_SWEEP_FREQ);
    DisplayMessage("SWEEP", "Too Low\r\n Start Frequency!");
    delay(2500);
    //ModMenuPos=MOD_MENU_SWEEP_START_FREQ_M_INDEX;
    IsValid = false;
  }

  if (GetSweepEndFreq() > MAX_SWEEP_FREQ)
  {
    SetSweepEndFreq(MAX_SWEEP_FREQ);
    DisplayMessage("SWEEP", "Too High\r\n Stop Frequency!");
    delay(2500);
    //ModMenuPos=MOD_MENU_SWEEP_END_FREQ_M_INDEX;
    IsValid = false;
  }

  if (GetSweepEndFreq() < MIN_SWEEP_FREQ)
  {
    SetSweepEndFreq(MIN_SWEEP_FREQ);
    DisplayMessage("SWEEP", "Too Low\r\n Stop Frequency!");
    delay(2500);
    //ModMenuPos=MOD_MENU_SWEEP_END_FREQ_M_INDEX;
    IsValid = false;
  }

  if (GetSweepStartFreq() == GetSweepEndFreq())
  {
    //SetSweepEndFreq(MIN_SWEEP_FREQ);
    DisplayMessage("SWEEP", "Frequencies\r\n are Equal!");
    delay(2500);
    IsValid = false;
  }
  return IsValid;
}

bool IsSweepTimeTooLong()
{
  //GetSweepStartFreq(), GetSweepEndFreq(), SweepTime, SweepTimeFormat
  uint32_t DeltaFTW = FreqToFTW(GetSweepEndFreq()) - FreqToFTW(GetSweepStartFreq());
  //float GHZ_CoreClock=DDS_Core_Clock/1E9; //незабыть заменит на CalcRealDDSCoreClockFromOffset();
  float GHZ_CoreClock = CalcRealDDSCoreClockFromOffset() / 1E9;
  uint64_t MaxPossibleNanoSweepTime = (4 / GHZ_CoreClock * DeltaFTW) * 0xFFFF; //умножаем на 0xFFFF (максимальное значение StepRate) для того чтобы узнать максиамльно возможное время свипа для заданного интервала частот (при текущей частоте ядра)
#if DBG==2
  Serial.print("MaxPossibleNanoSweepTime=");
  print64(MaxPossibleNanoSweepTime);
  Serial.print("GetSweepTime()=");
  print64(GetSweepTime());
#endif
  if (GetSweepTime() > MaxPossibleNanoSweepTime) //проверяем чтобы не получилось так чтобы введеное время не оказалось дольше максимально возможного для заданного диапазона частот и текущей частоты ядра
  {
    SetSweepTime(MaxPossibleNanoSweepTime);
    DisplayMessage("SWEEP", F("Too Long Time"));
    delay(2500);
    return true;
  }
  else
  {
    return false;
  }
}

uint64_t GetSweepTime() // возвращает время в наносекунадх указанное пользователем в меню
{
  if (SweepTimeFormat == 0) return SweepTime * 1E9;
  else if (SweepTimeFormat == 1) return SweepTime * 1E6;
  else if (SweepTimeFormat == 2) return SweepTime * 1E3;
}

void SetSweepTime(uint64_t NanoSweepTime)
{
  if (NanoSweepTime < 1E3) //1000
  {
    SweepTime = NanoSweepTime;
    SweepTimeFormat = 3; //nS
  } else if (NanoSweepTime < 1E6) //1000000
  {
    SweepTime = NanoSweepTime / 1E3;
    SweepTimeFormat = 2; //uS
  } else if (NanoSweepTime < 1E9) //1000000000
  {
    SweepTime = NanoSweepTime / 1E6;
    SweepTimeFormat = 1; //uS
  } else
  {
    SweepTime = NanoSweepTime / 1E9;
    SweepTimeFormat = 0; //S
  }
}

#if DBG==2
void print64(uint64_t value)
{
  const int NUM_DIGITS = log10(value) + 1;

  char sz[NUM_DIGITS + 1];

  sz[NUM_DIGITS] =  0;
  for ( size_t i = NUM_DIGITS; i--; value /= 10)
  {
    sz[i] = '0' + (value % 10);
  }

  Serial.println(sz);
}
#endif

float getInputVoltage()
{
  uint16_t voltage=0; //mV
  analogReference(DEFAULT); 
  delay(10);
  voltage = map(analogRead(INPUT_VOLTAGE_PIN), 0, 1023, 0, ANALOG_REFERENCE_VOLTAGE); //4.95
  return float(voltage) / 1000.0 / VOLTAGE_DIVIDER_FACTOR;
}

void displayPowerWarning()
{
  static const unsigned char PROGMEM image_alert_bicubic_bits[] = {0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x01,0x80,0x00,0x00,0x00,0x00,0x07,0xe0,0x00,0x00,0x00,0x00,0x07,0xe0,0x00,0x00,0x00,0x00,0x0f,0xf0,0x00,0x00,0x00,0x00,0x0e,0x70,0x00,0x00,0x00,0x00,0x1c,0x38,0x00,0x00,0x00,0x00,0x3c,0x3c,0x00,0x00,0x00,0x00,0x38,0x1c,0x00,0x00,0x00,0x00,0x78,0x1e,0x00,0x00,0x00,0x00,0x70,0x0e,0x00,0x00,0x00,0x00,0xf0,0x0f,0x00,0x00,0x00,0x00,0xe3,0xc7,0x00,0x00,0x00,0x01,0xc3,0xc3,0x80,0x00,0x00,0x03,0xc3,0xc3,0xc0,0x00,0x00,0x03,0x83,0xc1,0xc0,0x00,0x00,0x07,0x83,0xc1,0xe0,0x00,0x00,0x07,0x03,0xc0,0xe0,0x00,0x00,0x0e,0x03,0xc0,0xf0,0x00,0x00,0x0e,0x03,0xc0,0x70,0x00,0x00,0x1c,0x03,0xc0,0x38,0x00,0x00,0x3c,0x03,0xc0,0x3c,0x00,0x00,0x38,0x03,0xc0,0x1c,0x00,0x00,0x78,0x03,0xc0,0x1e,0x00,0x00,0x70,0x03,0xc0,0x0e,0x00,0x00,0xe0,0x03,0xc0,0x07,0x00,0x01,0xe0,0x03,0xc0,0x07,0x80,0x01,0xc0,0x03,0xc0,0x03,0x80,0x03,0xc0,0x01,0x80,0x03,0xc0,0x03,0x80,0x00,0x00,0x01,0xc0,0x07,0x80,0x00,0x00,0x01,0xe0,0x07,0x00,0x01,0x80,0x00,0xe0,0x0e,0x00,0x03,0xc0,0x00,0x70,0x1e,0x00,0x03,0xc0,0x00,0x78,0x1c,0x00,0x01,0x80,0x00,0x38,0x3c,0x00,0x00,0x00,0x00,0x3c,0x38,0x00,0x00,0x00,0x00,0x1c,0x38,0x00,0x00,0x00,0x00,0x1c,0x3f,0xff,0xff,0xff,0xff,0xfc,0x1f,0xff,0xff,0xff,0xff,0xf8,0x01,0xff,0xff,0xff,0xff,0x80,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00};
  display.clearDisplay();
  display.setTextColor(1);
  display.setTextSize(2);
  display.setCursor(23, 1);
  display.print("WARNING");
  display.setTextWrap(false);
  display.setCursor(43, 17);
  display.print("7.5 VDC");
  display.setTextSize(1);
  display.setCursor(49, 35);
  display.print("POWER SUPPLY");
  display.setTextSize(2);
  display.setCursor(72, 45);
  display.print("USB");
  display.drawBitmap(1, 11, image_alert_bicubic_bits, 48, 48, 1);
  display.setTextSize(1);
  display.setCursor(55, 48);
  display.print("or");
  display.display();
}

void selectClockSrcPath(uint8_t path)
{
  switch (path)
  {
    case CLOCK_SOURCE_XO_INDEX:
      digitalWrite(TCXO_POWER_PIN, HIGH);
      digitalWrite(TCXO_PATH_PIN, LOW);
      digitalWrite(EXTERANL_SRC_PATH_PIN, LOW);
    break;
    case CLOCK_SOURCE_TCXO_INDEX:
      digitalWrite(TCXO_POWER_PIN, HIGH);
      digitalWrite(EXTERANL_SRC_PATH_PIN, LOW);
      digitalWrite(TCXO_PATH_PIN, HIGH);
    break;
    case CLOCK_SOURCE_EXT_TCXO_INDEX:
      digitalWrite(TCXO_POWER_PIN, LOW);
      digitalWrite(TCXO_PATH_PIN, LOW);
      digitalWrite(EXTERANL_SRC_PATH_PIN, HIGH);
    break;
    case CLOCK_SOURCE_EXT_OSC_INDEX:
      digitalWrite(TCXO_POWER_PIN, LOW);
      digitalWrite(TCXO_PATH_PIN, LOW);
      digitalWrite(EXTERANL_SRC_PATH_PIN, HIGH);
    break;    
  }
}

ISR(TIMER1_A) {  
  
  switch (TRG_STATE)
  {
    case 0:
      Timer1.pause();
      RestartDRG();
      Timer1.restart();
      PORTL &= ~(1 << PORTL1); //OFF TRG_OUT
    break;
    case 1:
      PORTL |= (1 << PORTL1); //ON RG_OUT
    break;
  }

  TRG_STATE =! TRG_STATE;
}

void StartSCurve()
{
  Timer1.setPeriod(S_CurveBandwith * 1000 / 2);     
  Timer1.enableISR(CHANNEL_A);
}

void StopSCurve()
{
  Timer1.stop();
  PORTL &= ~(1 << PORTL1); //OFF
}

#if DBG==2
void readRegs()
{
    //*********DBG*********
    // DDS_GPIO_Init();
    // HAL_GPIO_WritePin(DDS_MASTER_RESET_GPIO_PORT, DDS_MASTER_RESET_PIN, GPIO_PIN_SET);   // RESET = 1
    // //HAL_Delay(10);
    // delay(100);
    // HAL_GPIO_WritePin(DDS_MASTER_RESET_GPIO_PORT, DDS_MASTER_RESET_PIN, GPIO_PIN_RESET); // RESET = 0
    // HAL_GPIO_WritePin(DDS_IO_UPDATE_GPIO_PORT, DDS_IO_UPDATE_PIN, GPIO_PIN_RESET);       // IO_UPDATE = 0   
    // HAL_GPIO_WritePin(DDS_SPI_CS_GPIO_PORT, DDS_SPI_CS_PIN, GPIO_PIN_SET);               // CS = 1
    // HAL_GPIO_WritePin(DDS_OSK_GPIO_PORT, DDS_OSK_PIN, GPIO_PIN_SET);                     // OSK = 1
    // HAL_GPIO_WritePin(DDS_PROFILE_0_GPIO_PORT, DDS_PROFILE_0_PIN, GPIO_PIN_RESET);
    // HAL_GPIO_WritePin(DDS_PROFILE_1_GPIO_PORT, DDS_PROFILE_1_PIN, GPIO_PIN_RESET);
    // HAL_GPIO_WritePin(DDS_PROFILE_2_GPIO_PORT, DDS_PROFILE_2_PIN, GPIO_PIN_RESET);
    // DDS_SPI_Init();
    // HAL_GPIO_WritePin(DDS_SPI_CS_GPIO_PORT, DDS_SPI_CS_PIN, GPIO_PIN_RESET);    
    // SPI.transfer(CFR1_addr);
    // SPI.transfer(0); //strBuffer[1] = 0;// RAM_enable;//RAM_Playback_Amplitude;// | RAM_enable;//0x00; 
    // SPI.transfer(0); //strBuffer[2] = 0;//Inverse_sinc_filter_enable;//0; //Continuous_Profile_0_1; //0;//0x80;//0x00;
    // SPI.transfer(0); //strBuffer[3] = 0; //OSK_enable | Select_auto_OSK;//0x00;
    // SPI.transfer(SDIO_input_only); //strBuffer[4] = SDIO_input_only ;
    // //  HAL_SPI_Transmit(&hspi1, (uint8_t*)strBuffer, 5, 1000);
    // HAL_GPIO_WritePin(DDS_SPI_CS_GPIO_PORT, DDS_SPI_CS_PIN, GPIO_PIN_SET);
    // DDS_UPDATE();
    // readRegs();
  //********************
  HAL_GPIO_WritePin(DDS_SPI_CS_GPIO_PORT, DDS_SPI_CS_PIN, GPIO_PIN_RESET);    
      //Serial.println(SPI.transfer(CFR1_addr | B10000000));
      Serial.println("CFR1");
      SPI.transfer(CFR1_addr | B10000000);
      Serial.println(SPI.transfer(0), HEX);
      Serial.println(SPI.transfer(0), HEX);
      Serial.println(SPI.transfer(0), HEX);
      Serial.println(SPI.transfer(0), HEX);
  HAL_GPIO_WritePin(DDS_SPI_CS_GPIO_PORT, DDS_SPI_CS_PIN, GPIO_PIN_SET); 
  HAL_GPIO_WritePin(DDS_SPI_CS_GPIO_PORT, DDS_SPI_CS_PIN, GPIO_PIN_RESET);    
      //Serial.println(SPI.transfer(CFR2_addr | B10000000));
      Serial.println("CFR2");
      SPI.transfer(CFR2_addr | B10000000);
      Serial.println(SPI.transfer(0), HEX);
      Serial.println(SPI.transfer(0), HEX);
      Serial.println(SPI.transfer(0), HEX);
      Serial.println(SPI.transfer(0), HEX);
  HAL_GPIO_WritePin(DDS_SPI_CS_GPIO_PORT, DDS_SPI_CS_PIN, GPIO_PIN_SET); 
  HAL_GPIO_WritePin(DDS_SPI_CS_GPIO_PORT, DDS_SPI_CS_PIN, GPIO_PIN_RESET);    
      //Serial.println(SPI.transfer(CFR3_addr | B10000000));
      Serial.println("CFR3");
      SPI.transfer(CFR3_addr | B10000000);
      Serial.println(SPI.transfer(0), HEX);
      Serial.println(SPI.transfer(0), HEX);
      Serial.println(SPI.transfer(0), HEX);
      Serial.println(SPI.transfer(0), HEX);
  HAL_GPIO_WritePin(DDS_SPI_CS_GPIO_PORT, DDS_SPI_CS_PIN, GPIO_PIN_SET); 

}
#endif

void TurnDDSPowerOFF()
{
  Serial.println(F("Output Disabled"));
  digitalWrite(DDS_PWR_DWN_PIN, HIGH);
  isPWR_DWN = true;
}

void TurnDDSPowerON()
{
  Serial.println(F("Output Enabled"));
  digitalWrite(DDS_PWR_DWN_PIN, LOW);
  delay(500);
  MakeOut();
  isPWR_DWN = false;
}

void DisplayInit(String msg)
{
  display = Adafruit_SSD1306(128, 64, &Wire);
  display.begin(SSD1306_SWITCHCAPVCC, 0x3C);
  display.clearDisplay();
  display.cp437(true);
  display.setTextSize(2);
  display.setTextColor(WHITE); 
  display.setCursor(0, 25);
  display.print("RF-LAB 0.1");
  display.setTextSize(1);
  display.setTextColor(WHITE);
  display.setCursor(0, 50);
  display.print(msg);
  display.display();
}

void DisplayMsg(String msg)
{
  display.clearDisplay();
  display.setTextSize(2);
  display.setTextColor(WHITE); 
  display.setCursor(0, 25);
  display.print("RF-LAB 0.1");
  display.setTextSize(1);
  display.setTextSize(1);
  display.setTextColor(WHITE);
  display.setCursor(0, 50);
  display.print(msg);
  display.display();
}


// ============================================================================
// ===                                                                      ===
// ===                              S E T U P                               ===
// ===                                                                      ===
// ============================================================================
void setup()
{
  pinMode(LEDred, OUTPUT);
  pinMode(LEDgreen, OUTPUT);

  pinMode(TCXO_POWER_PIN, OUTPUT);
  pinMode(TCXO_PATH_PIN, OUTPUT);
  pinMode(EXTERANL_SRC_PATH_PIN, OUTPUT);
  digitalWrite(TCXO_POWER_PIN, LOW);
  digitalWrite(TCXO_PATH_PIN, LOW);
  digitalWrite(EXTERANL_SRC_PATH_PIN, LOW);

  pinMode(TRG_OUT_PIN, OUTPUT);
  pinMode(TRG_LED_PIN, OUTPUT);
  digitalWrite(TRG_OUT_PIN, LOW);
  digitalWrite(TRG_LED_PIN, LOW);

  delay(1000);

  Serial.begin(BAUDRATE_HOST);

  if (DBG == 2) {
    Serial.println(F("DDS AD9910 Arduino Shield v3.5 by GRA & AFCH. (gra-afch.com)"));
    Serial.print(F("Firmware v.:"));
    Serial.println(FIRMWAREVERSION);
    Serial.print(F("Input voltage = "));
    Serial.println(getInputVoltage());
  }

  if ((getInputVoltage() < 4) ||
      (getInputVoltage() > 8)) 
      {
        displayPowerWarning();
        while(1);
      }

  RFOutButton.debounceTime   = 25;   // Debounce timer in ms
  RFOutButton.multiclickTime = 1;  // Time limit for multi clicks
  RFOutButton.longClickTime  = 1000; // time until "held-down clicks" register

  modeButton.Update();
  if (modeButton.depressed == true) 
  {
    EEPROM.write(CLOCK_SETTINGS_FLAG_ADR, 255); //flag that force save default clock settings to EEPROM
    EEPROM.write(MAIN_SETTINGS_FLAG_ADR, 255); //flag that force save default main settings to EEPROM
    EEPROM.write(MODULATION_SETTINGS_FLAG_ADR, 255); //flag that force save default modulation settings to EEPROM
    EEPROM.write(LO_SETTINGS_FLAG_ADR, 255); 
  }

  LoadMainSettings();
  LoadClockSettings();
  LoadModulationSettings();
  LoadLOSettings();

  MakeOut();

  MenuPos = 0;
  modeButton.debounceTime   = 25;   // Debounce timer in ms
  modeButton.multiclickTime = 1;  // Time limit for multi clicks
  modeButton.longClickTime  = 1000; // time until "held-down clicks" register

  initializeMeasuremets();

  // -----------------
  // ===   SI5351  ===
  // -----------------
  si5351_connected = false;
  if (USE_SI5351) {
    bool i2c_found = si5351.init(SI5351_CRYSTAL_LOAD_8PF, 0, 0);
    // Set CLK0 to output 1 MHz
    si5351.set_freq(100000000ULL, SI5351_CLK0);
    // Set CLK1 to output 2 MHz
    si5351.set_ms_source(SI5351_CLK1, SI5351_PLLB);
    si5351.set_freq_manual(200000000ULL, 70000000000ULL, SI5351_CLK1);
    // Set CLK2 to output 25 MHz
    si5351.set_ms_source(SI5351_CLK2, SI5351_PLLB);
    si5351.set_freq_manual(2500000000ULL, 70000000000ULL, SI5351_CLK2);
    si5351.update_status();
    si5351_connected = i2c_found;
  }

  if (si5351_connected) 
    DisplayInit("Setup with SI5351");
  else
    DisplayInit("Setup w/o SI5351");
  digitalWrite(LEDred, LOW); 
  digitalWrite(LEDgreen, HIGH);
}


// ============================================================================
// ===                                                                      ===
// ===                           M A I N   L O O P                          ===
// ===                                                                      ===
// ============================================================================
void loop ()
{
  int functionUpButton = 0;
  int functionDownButton = 0;
  static int LastUpButtonState = 1;
  static int LastDownButtonState = 1;

  while (1)
  {
    // -------------------------
    // ===   UART Interface  ===
    // -------------------------

    if (Serial.available()) {
      byte rx = Serial.peek();
      if ((rx & MSG_SYNC_MASK) == MSG_SYNC) {
        byte N_Tx = 0;
        while (N_Tx == 0) {
          while (!Serial.available()) {;}
          rx = Serial.read();
          // get byte received and call COM_Rx:
          // - if a complete MCNF message was received,
          //   the COM_Rx will call the message dispatcher
          byte N_Tx = COM_Rx(MSG_UART, rx);   
          // send bytes if available:
          if (N_Tx > 0) {     
            for (byte i=0; i<N_Tx; i++) 
            { 
              Serial.write(Buffer[i]); 
            }        
          } else {
            // if we were waiting for UART input, check timeout:
            if (COM_Timeout_0 != COM_TIMEOUT_DISABLED) {
              if ((millis() - COM_Timeout_0) > COM_TIMEOUT) {
                // timeout error:
                COM_Rx_Timeout();
              }
            }     
          }
        }
      } else {
        ProcessSerialCommand();
      }
    }

    // ------------------------------
    // ===   Buttons and Encoder  ===
    // ------------------------------
    if (USE_ENCODER)
    {
    curPos=0;
    curPos=myEnc.read();
    RFOutButton.Update();
    modeButton.Update(); 
    if (modeButton.clicks > 0) 
    {
      switch(MenuPos)
      {
        case MAIN_MENU_MOD_INDEX: 
          Modultaion_Menu();
        break;
        case MAIN_MENU_LOCAL_OSC_INDEX: 
          LocalOscillator_Menu();
        break;
        default:
          EditMode = !EditMode;
      }
    }

    if (modeButton.clicks < 0) {
      // long click button encoder
      DDS_Clock_Config_Menu(); 
    }

    if (RFOutButton.clicks > 0) 
    {
      isPWR_DWN = !isPWR_DWN;
      if (isPWR_DWN) TurnDDSPowerOFF(); 
        else TurnDDSPowerON();
    }

    if (EditMode == false) 
    { 
      if (curPos > 0) 
      {
        MenuPos++;
        if (MenuPos > MAIN_MENU_LAST_INDEX) MenuPos = MAIN_MENU_MHZ_INDEX;
      }
      if (curPos < 0) 
      {
        MenuPos--;
        if (MenuPos < MAIN_MENU_MHZ_INDEX) MenuPos = MAIN_MENU_LAST_INDEX;
      }
    } 
    else 
    {
      if (curPos > 0) 
      {
        if (MenuPos == 0) {
          if (Check(M + curPos, K, H)) M = Inc(M, curPos);
        }
        if (MenuPos == 1) {
          if (Check(M, K + curPos, H)) K = Inc(K, curPos);
        }
        if (MenuPos == 2) {
          if (Check(M, K, H + curPos)) H = Inc(H, curPos);
        }
        if (MenuPos == 3)
        {
          A = A - curPos;
          if (A < 0) A = 0;
        }
      }
      if (curPos < 0) 
      {
        if (MenuPos == 0) {
          if (Check(M + curPos, K, H)) M = Dec(M, curPos);
        }
        if (MenuPos == 1) {
          if (Check(M, K + curPos, H)) K = Dec(K, curPos);
        }
        if (MenuPos == 2) {
          if (Check(M, K, H + curPos)) H = Dec(H, curPos);
        }
        if (MenuPos == 3)
        {
          A = A + curPos*-1;
          if (A > 72) A = 72; // Amplitude low limit
        }
      }
    }

    if (curPos != 0) 
    {
      SaveMainSettings();
      MakeOut(); 
    }

    DrawMainMenu();

    } // if (USE_ENCODER) {...}

  } // while (1) 

} // Loop()


// ============================================================================
// ===                                                                      ===
// ===           D E V I C E   M E S S A G E   D I S P A T C H E R          ===
// ===                                                                      ===
// ============================================================================

//  -----------------------------------------------
    byte DEVICE_ProcessMessage(byte DeviceNumber) {
//  -----------------------------------------------
//  
      // dispatch Device messages:
      if (DeviceNumber == 1) return DEVICE_ProcessMessage();
      return ERR_UNKNOWN_ID;
    }

// ============================================================================
// ==================================  E O F  =================================
// ============================================================================
