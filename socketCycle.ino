#include "secrets.h"
#include <Arduino.h>
#include <WiFi.h>
#include <ArduinoWebsockets.h>
#include "LittleFS.h"

using namespace websockets;

// ── Config ────────────────────────────────────────────────────────────────────
const char* WS_PATH        = "/ws";

const int SENSOR_PIN       = 36;
const int DRY_VALUE        = 3200;
const int WET_VALUE        = 1600;  

const uint64_t SLEEP_US   = 14400000000ULL;  // sleep 4 hours in btwn readings
const char*    CACHE_FILE = "/cache.txt";
const int      WIFI_TIMEOUT_MS = 10000;

// ── Sensor ────────────────────────────────────────────────────────────────────
float readSensor() {
    int raw = analogRead(SENSOR_PIN);
    float moisture = map(raw, DRY_VALUE, WET_VALUE, 0, 100);
    return constrain(moisture, 0.0, 100.0);
}

// ── WiFi ──────────────────────────────────────────────────────────────────────
bool connectWifi() {
    Serial.printf("Connecting to %s", WIFI_SSID);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    unsigned long start = millis();
    while (WiFi.status() != WL_CONNECTED) {
        if (millis() - start > WIFI_TIMEOUT_MS) {
            Serial.println("\nWiFi timed out.");
            return false;
        }
        delay(500);
        Serial.print(".");
    }

    Serial.printf("\nWiFi connected — IP: %s\n", WiFi.localIP().toString().c_str());
    return true;
}

// ── Cache ─────────────────────────────────────────────────────────────────────
void cacheReading(float moisture) {
    File file = LittleFS.open(CACHE_FILE, FILE_APPEND);
    if (!file) {
        Serial.println("Failed to open cache file.");
        return;
    }
    file.printf("%.1f\n", moisture);
    file.close();
    Serial.printf("Cached reading: %.1f%%\n", moisture);
}

void sendCacheIfExists(WebsocketsClient& client) {
    if (!LittleFS.exists(CACHE_FILE)) return;

    File file = LittleFS.open(CACHE_FILE, FILE_READ);
    if (!file) return;

    Serial.println("Sending cached readings...");
    while (file.available()) {
        String line = file.readStringUntil('\n');
        line.trim();
        if (line.length() == 0) continue;

        char payload[32];
        snprintf(payload, sizeof(payload), "{\"moisture\":%s}", line.c_str());
        Serial.printf("Sending cached: %s\n", payload);
        client.send(payload);
        client.poll();
        delay(100);
    }
    file.close();

    LittleFS.remove(CACHE_FILE);
    Serial.println("Cache cleared.");
}

// ── Sleep ─────────────────────────────────────────────────────────────────────
void goToSleep() {
    Serial.println("Going to sleep.");
    Serial.flush();
    WiFi.disconnect(true);
    WiFi.mode(WIFI_OFF);
    esp_sleep_enable_timer_wakeup(SLEEP_US);
    esp_deep_sleep_start();
}

// ── Setup ─────────────────────────────────────────────────────────────────────
void setup() {
    Serial.begin(115200);
    delay(3000);


    // Mount LittleFS
    if (!LittleFS.begin(true)) {
        Serial.println("LittleFS mount failed.");
    }

    // Read sensor first — before any network activity
    float moisture = readSensor();
    Serial.printf("Moisture: %.1f%%\n", moisture);

    // Attempt WiFi
    if (!connectWifi()) {
        // No connection — cache and sleep
        cacheReading(moisture);
        goToSleep();
        return;
    }

    // Attempt WebSocket
    WebsocketsClient client;
    bool connected = client.connect(WS_HOST, WS_PORT, WS_PATH);

    if (!connected) {
        // WiFi up but server unreachable — cache and sleep
        Serial.println("WebSocket failed — caching reading.");
        cacheReading(moisture);
        goToSleep();
        return;
    }

    Serial.println("WebSocket connected.");

    // Drain any cached readings first
    sendCacheIfExists(client);

    // Send current reading
    char payload[32];
    snprintf(payload, sizeof(payload), "{\"moisture\":%.1f}", moisture);
    Serial.printf("Sending: %s\n", payload);
    client.send(payload);
    client.poll();

    delay(200);  // let the send complete before cutting power
    goToSleep();
}

// ── Loop ──────────────────────────────────────────────────────────────────────
void loop() {
    // Never reached — deep sleep restarts from setup()
}