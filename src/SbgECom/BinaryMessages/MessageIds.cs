namespace SbgECom.BinaryMessages;

public enum MessageClass : byte { LogECom0 = 0x00, LogECom1 = 0x01, LogNmea0 = 0x02, LogNmea1 = 0x03, LogThirdParty0 = 0x04, LogNmeaGnss = 0x05, Command = 0x10 }

public enum BinaryLog : byte
{
    Status=1, UtcTime=2, ImuData=3, Mag=4, MagCalib=5, EkfEuler=6, EkfQuaternion=7, EkfNavigation=8,
    ShipMotion=9, Gnss1Velocity=13, Gnss1Position=14, Gnss1Heading=15, Gnss2Velocity=16, Gnss2Position=17,
    Gnss2Heading=18, OdometerVelocity=19, EventA=24, EventB=25, EventC=26, EventD=27, EventE=28,
    DvlBottomTrack=29, DvlWaterTrack=30, Gnss1Raw=31, ShipMotionHighPerformance=32, AirData=36, Usbl=37,
    Gnss2Raw=38, ImuShort=44, EventOutA=45, EventOutB=46, Depth=47, Diagnostic=48, RtcmRaw=49,
    Gnss1Satellites=50, Gnss2Satellites=51, EkfRotationAccelerationBody=52, EkfRotationAccelerationNed=53,
    EkfVelocityBody=54, SessionInfo=55, EkfDebug=56, PtpStatus=57, Velocity1=58, VibrationFft=59,
    VibrationReport=60, EkfAirData=61, Position1=62
}
