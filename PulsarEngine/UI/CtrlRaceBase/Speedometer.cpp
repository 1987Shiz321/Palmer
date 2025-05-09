#include <MarioKartWii/Kart/KartManager.hpp>
#include <UI/CtrlRaceBase/Speedometer.hpp>
#include <Settings/Settings.hpp>

namespace Pulsar {
namespace UI {
u32 CtrlRaceSpeedo::Count() {
    if(Settings::Mgr::Get().GetSettingValue(Settings::SETTINGSTYPE_RACE, SETTINGRACE_RADIO_SOM) == RACESETTING_SOM_DISABLED) return 0;
    const RacedataScenario& scenario = Racedata::sInstance->racesScenario;
    u32 localPlayerCount = scenario.localPlayerCount;
    const SectionId sectionId = SectionMgr::sInstance->curSection->sectionId;
    if(sectionId >= SECTION_WATCH_GHOST_FROM_CHANNEL && sectionId <= SECTION_WATCH_GHOST_FROM_MENU) localPlayerCount += 1;
    if(localPlayerCount == 0 && (scenario.settings.gametype & GAMETYPE_ONLINE_SPECTATOR)) localPlayerCount = 1;
    return localPlayerCount;
}
void CtrlRaceSpeedo::Create(Page& page, u32 index, u32 count) {
    u8 speedoType = (count == 3) ? 4 : count;
    for(int i = 0; i < count; ++i) {
        CtrlRaceSpeedo* som = new(CtrlRaceSpeedo);
        page.AddControl(index + i, *som, 0);
        char variant[0x20];
        int pos = i;
        if(Settings::Mgr::Get().GetSettingValue(Settings::SETTINGSTYPE_RACE, SETTINGRACE_RADIO_SOM) == RACESETTING_SOM_RIGHT){
            //右に置いた場合の処理.
            if(count == 1){
                //一人用における処理.
                pos = 1;
            }            
            snprintf(variant, 0x20, "Speedo_%1d_%1d", speedoType, pos);
        } else if(Settings::Mgr::Get().GetSettingValue(Settings::SETTINGSTYPE_RACE, SETTINGRACE_RADIO_SOM) == RACESETTING_SOM_LEFT){
            //左に置いた場合の処理
            if(count == 1){
                //一人用における処理.
                pos = 1;
            }  
            snprintf(variant, 0x20, "Speedo2_%1d_%1d", speedoType, pos);
        }
        som->Load(variant, i);
    }
}
static CustomCtrlBuilder SOM(CtrlRaceSpeedo::Count, CtrlRaceSpeedo::Create);

void CtrlRaceSpeedo::Load(const char* variant, u8 id) {
    this->hudSlotId = id;
    ControlLoader loader(this);
    const char* anims[] ={
        "Hundreds", "Hundreds", nullptr,
        "Tens", "Tens", nullptr,
        "Units", "Units", nullptr,
        "Dot", "Dot",nullptr,
        "Tenths", "Tenths", nullptr,
        "Hundredths", "Hundredths", nullptr,
        "Thousandths", "Thousandths", nullptr,
        nullptr
    };

    if(Settings::Mgr::Get().GetSettingValue(Settings::SETTINGSTYPE_RACE, SETTINGRACE_RADIO_SOM) == RACESETTING_SOM_RIGHT){
        //右に置いた場合の処理
        loader.Load(UI::raceFolder, "PULSpeedo", variant, anims);
    } else if(Settings::Mgr::Get().GetSettingValue(Settings::SETTINGSTYPE_RACE, SETTINGRACE_RADIO_SOM) == RACESETTING_SOM_LEFT){
        //左に置いた場合の処理
        loader.Load(UI::raceFolder, "PULSpeedo2", variant, anims);
    }

    this->Animate();
    return;
}

void CtrlRaceSpeedo::Init() {
    //this->HudSlotColorEnable("speed0", true);
    //this->HudSlotColorEnable("speed1", true);
    //this->HudSlotColorEnable("speed2", true);
    //this->HudSlotColorEnable("speed3", true);
    //this->HudSlotColorEnable("speed4", true);
    //this->HudSlotColorEnable("speed5", true);
    //this->HudSlotColorEnable("speed6", true);
    //this->HudSlotColorEnable("kmh", true);
    LayoutUIControl::Init();
    return;
}

void CtrlRaceSpeedo::OnUpdate() {
    this->UpdatePausePosition();
    const u8 digits = Settings::Mgr::Get().GetSettingValue(Settings::SETTINGSTYPE_RACE, SETTINGRACE_SCROLL_SOM);
    const Kart::Pointers& pointers = Kart::Manager::sInstance->players[this->GetPlayerId()]->pointers;
    const Kart::Physics* physics = pointers.kartBody->kartPhysicsHolder->physics;

    Vec3 sum;
    MTX::PSVECAdd(&physics->engineSpeed, &physics->speed2, &sum);
    MTX::PSVECAdd(&physics->speed3, &sum, &sum);
    float speed = MTX::PSVECMag(&sum);
    float speedCap = pointers.kartMovement->hardSpeedLimit;
    if(speed > speedCap) speed = speedCap;


    const u32 speedValue = static_cast<u32>(speed * 1000.0f);

    //スピードメーターの数値計算
    u32 raw_hundreds = speedValue / 100000 % 10;
    u32 raw_tens = speedValue / 10000 % 10;
    u32 raw_units = speedValue / 1000 % 10;
    u32 raw_tenths = speedValue / 100 % 10;
    u32 raw_hundredths = speedValue / 10 % 10;
    u32 raw_thousandths = speedValue % 10;

    u32 dot = 11;
    u32 speed0 = 10;
    u32 speed1 = 10;
    u32 speed2 = 10;
    u32 speed3 = 10; 
    u32 speed4 = 10;
    u32 speed5 = 10;
    u32 speed6 = 10;
    
    //右に置いた場合の処理
    if (Settings::Mgr::Get().GetSettingValue(Settings::SETTINGSTYPE_RACE, SETTINGRACE_RADIO_SOM) == RACESETTING_SOM_RIGHT) {
        if (digits == 1) {
            if (speedValue < 10000) {
                // 0.0 ~ 9.9km/h → "00X.Y"
                speed4 = raw_units;
                speed5 = dot;
                speed6 = raw_tenths;
            } else if (speedValue < 100000) {
                // 10.0 ~ 99.9km/h → "0XY.Z"
                speed3 = raw_tens;
                speed4 = raw_units;
                speed5 = dot;
                speed6 = raw_tenths;
            } else {
                // 100.0km/h以上 → "XYZ.W"
                speed2 = raw_hundreds;
                speed3 = raw_tens;
                speed4 = raw_units;
                speed5 = dot;
                speed6 = raw_tenths;
            }
        }
        else if (digits == 2) {
            if (speedValue < 10000) {
                //表示例:0.00km/h
                speed3 = raw_units;
                speed4 = dot;
                speed5 = raw_tenths;
                speed6 = raw_hundredths;
            } else if (speedValue < 100000) {
                //表示例:12.34km/h
                speed2 = raw_tens;
                speed3 = raw_units;
                speed4 = dot;
                speed5 = raw_tenths;
                speed6 = raw_hundredths;
            } else {
                //表示例:123.45km/h
                speed1 = raw_hundreds;
                speed2 = raw_tens;
                speed3 = raw_units;
                speed4 = dot;
                speed5 = raw_tenths;
                speed6 = raw_hundredths;
            }
        }
        else if (digits == 3) {
            if (speedValue < 10000) {
                //表示例:0.000km/h
                speed2 = raw_units;
                speed3 = dot;
                speed4 = raw_tenths;
                speed5 = raw_hundredths;
                speed6 = raw_thousandths;
            } else if (speedValue < 100000) {
                //表示例:12.345km/h
                speed1 = raw_tens;
                speed2 = raw_units;
                speed3 = dot;
                speed4 = raw_tenths;
                speed5 = raw_hundredths;
                speed6 = raw_thousandths;
            } else {
                //表示例:123.456km/h
                speed0 = raw_hundreds;
                speed1 = raw_tens;
                speed2 = raw_units;
                speed3 = dot;
                speed4 = raw_tenths;
                speed5 = raw_hundredths;
                speed6 = raw_thousandths;
            }
        }
        else if (digits == 0){
            if (speedValue < 10000) {
                //表示例:0km/h
                speed6 = raw_units;
            } else if (speedValue < 100000) {
                //表示例:12km/h
                speed5 = raw_tens;
                speed6 = raw_units;
            } else {
                //表示例:123km/h
                speed4 = raw_hundreds;
                speed5 = raw_tens;
                speed6 = raw_units;
            }
        }
        else{
            //スピードメーター非表示の場合、ドットを非表示に。
            dot = 10;
        }
    } else if(Settings::Mgr::Get().GetSettingValue(Settings::SETTINGSTYPE_RACE, SETTINGRACE_RADIO_SOM) == RACESETTING_SOM_LEFT) {
        //左揃えの場合を作ります.
        if (digits == 1) {
            if (speedValue < 10000) {
                // 0.0 ~ 9.9km/h → "00X.Y"
                speed0 = raw_units;
                speed1 = dot;
                speed2 = raw_tenths;
            } else if (speedValue < 100000) {
                // 10.0 ~ 99.9km/h → "0XY.Z"
                speed0 = raw_tens;
                speed1 = raw_units;
                speed2 = dot;
                speed3 = raw_tenths;
            } else {
                // 100.0km/h以上 → "XYZ.W"
                speed0 = raw_hundreds;
                speed1 = raw_tens;
                speed2 = raw_units;
                speed3 = dot;
                speed4 = raw_tenths;
            }
        }
        else if (digits == 2) {
            if (speedValue < 10000) {
                //表示例:0.00km/h
                speed0 = raw_units;
                speed1 = dot;
                speed2 = raw_tenths;
                speed3 = raw_hundredths;
            } else if (speedValue < 100000) {
                //表示例:12.34km/h
                speed0 = raw_tens;
                speed1 = raw_units;
                speed2 = dot;
                speed3 = raw_tenths;
                speed4 = raw_hundredths;
            } else {
                //表示例:123.45km/h
                speed0 = raw_hundreds;
                speed1 = raw_tens;
                speed2 = raw_units;
                speed3 = dot;
                speed4 = raw_tenths;
                speed5 = raw_hundredths;
            }
        }
        else if (digits == 3) {
            if (speedValue < 10000) {
                //表示例:0.000km/h
                speed0 = raw_units;
                speed1 = dot;
                speed2 = raw_tenths;
                speed3 = raw_hundredths;
                speed4 = raw_thousandths;
            } else if (speedValue < 100000) {
                //表示例:12.345km/h
                speed0 = raw_tens;
                speed1 = raw_units;
                speed2 = dot;
                speed3 = raw_tenths;
                speed4 = raw_hundredths;
                speed5 = raw_thousandths;
            } else {
                //表示例:123.456km/h
                speed0 = raw_hundreds;
                speed1 = raw_tens;
                speed2 = raw_units;
                speed3 = dot;
                speed4 = raw_tenths;
                speed5 = raw_hundredths;
                speed6 = raw_thousandths;
            }
        }
        else if (digits == 0){
            if (speedValue < 10000) {
                //表示例:0km/h
                speed0 = raw_units;
            } else if (speedValue < 100000) {
                //表示例:12km/h
                speed0 = raw_tens;
                speed1 = raw_units;
            } else {
                //表示例:123km/h
                speed0 = raw_hundreds;
                speed1 = raw_tens;
                speed2 = raw_units;
            }
        }
        else{
            //スピードメーター非表示の場合、ドットを非表示に.
            dot = 10;
        }
    }

 
    

    /*
    //10 means empty, 11 dot
    u32 hundreds = speedValue % 1000000 / 100000;
    u32 tens = speedValue % 100000 / 10000;
    u32 units = speedValue % 10000 / 1000;
    u32 dot = digits >= 1 ? 11 : 10;
    u32 tenths = digits >= 1 ? speedValue % 1000 / 100 : 10;
    u32 hundredths = digits >= 2 ? speedValue % 100 / 10 : 10;
    u32 thousandths = digits == 3 ? speedValue % 100 / 10 : 10;

    if(speedValue < 10000) { //shift everything by 2 to the left
        hundreds = units;
        tens = dot;
        units = tenths;
        dot = hundredths;
        tenths = thousandths;
        hundredths = 10;
        thousandths = 10;
    }
    else if(speedValue < 100000) {
        hundreds = tens;
        tens = units;
        units = dot;
        dot = tenths;
        tenths = hundredths;
        hundredths = thousandths;
        thousandths = 10;
    }
    */

    SpeedArg args(speed0, speed1, speed2, speed3, speed4, speed5, speed6);
    this->Animate(&args);
    return;
}

void CtrlRaceSpeedo::Animate(const SpeedArg* args) {
    for(int i = 0; i < 7; ++i) {
        AnimationGroup& group = this->animator.GetAnimationGroupById(i);
        float frame = 0.0f;
        if(args != nullptr) frame = static_cast<float>(args->values[i]);
        group.PlayAnimationAtFrameAndDisable(0, frame);
    }
}
}//namespace UI
}//namespace Pulsar