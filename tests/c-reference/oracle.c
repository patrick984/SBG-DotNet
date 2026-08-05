#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <crc/sbgCrc.h>
#include <streamBuffer/sbgStreamBuffer.h>
#include <logs/sbgEComLogImu.h>

static size_t hex_decode(const char *text, uint8_t *data) {
    size_t n = strlen(text) / 2;
    for (size_t i=0; i<n; i++) { unsigned v; sscanf(text + i*2, "%2x", &v); data[i]=(uint8_t)v; }
    return n;
}
static void hex_print(const uint8_t *data, size_t n) { for(size_t i=0;i<n;i++) printf("%02x", data[i]); putchar('\n'); }

int main(int argc, char **argv) {
    if (argc < 3) return 2;
    uint8_t data[4096]; size_t n=hex_decode(argv[2], data);
    if (!strcmp(argv[1], "crc")) { printf("%04x\n", sbgCrc16Compute(data,n)); return 0; }
    if (!strcmp(argv[1], "frame")) {
        if (argc != 5 || n > 4086) return 2;
        unsigned cls=(unsigned)strtoul(argv[3],0,0), id=(unsigned)strtoul(argv[4],0,0);
        uint8_t out[4096]={0xff,0x5a,(uint8_t)id,(uint8_t)cls,(uint8_t)n,(uint8_t)(n>>8)};
        memcpy(out+6,data,n); uint16_t crc=sbgCrc16Compute(out+2,n+4);
        out[6+n]=(uint8_t)crc; out[7+n]=(uint8_t)(crc>>8); out[8+n]=0x33;
        hex_print(out,n+9); return 0;
    }
    if (!strcmp(argv[1], "imu")) {
        SbgStreamBuffer stream; SbgEComLogImuLegacy log;
        sbgStreamBufferInitForRead(&stream,data,n);
        if (sbgEComLogImuLegacyReadFromStream(&log,&stream) != SBG_NO_ERROR) return 3;
        printf("%u,%u,%.9g,%.9g,%.9g\n",log.timeStamp,log.status,log.accelerometers[0],log.temperature,log.deltaAngle[2]);
        return 0;
    }
    return 2;
}
