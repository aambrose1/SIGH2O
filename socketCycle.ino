#include "secrets.h"
#include <Arduino.h>
#include <WiFi.h>
#include <ArduinoWebsockets.h>
#include "LittleFS.h"
#include <WiFiClientSecure.h>

using namespace websockets;

// ── Config ────────────────────────────────────────────────────────────────────
const char* WS_PATH        = "/ws";

const int SENSOR_PIN       = 36;
const int DRY_VALUE        = 3200;
const int WET_VALUE        = 1600;

const uint64_t SLEEP_US   = 14400000000ULL;  // sleep 4 hours in btwn readings
const char*    CACHE_FILE = "/cache.txt";
const int      WIFI_TIMEOUT_MS = 10000;

static const char* ROOT_CA_CERT PROGMEM = R"EOF(
-----BEGIN CERTIFICATE-----
MIIFazCCA1OgAwIBAgIRAIIQz7DSQONZRGPgu2OCiwAwDQYJKoZIhvcNAQELBQAw
TzELMAkGA1UEBhMCVVMxKTAnBgNVBAoTIEludGVybmV0IFNlY3VyaXR5IFJlc2Vh
cmNoIEdyb3VwMRUwEwYDVQQDEwxJU1JHIFJvb3QgWDEwHhcNMTUwNjA0MTEwNDM4
WhcNMzUwNjA0MTEwNDM4WjBPMQswCQYDVQQGEwJVUzEpMCcGA1UEChMgSW50ZXJu
ZXQgU2VjdXJpdHkgUmVzZWFyY2ggR3JvdXAxFTATBgNVBAMTDElTUkcgUm9vdCBY
MTCCAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBAK3oJHP0FDfzm54rVygc
h77ct984kIxuPOZXoHj3dcKi/vVqbvYATyjb3miGbESTtrFj/RQSa78f0uoxmyF+
0TM8ukj13Xnfs7j/EvEhmkvBioZxaUpmZmyPfjxwv60pIgbz5MDmgK7iS4+3mX6U
A5/TR5d8mUgjU+g4rk8Kb4Mu0UlXjIB0ttov0DiNewNwIRt18jA8+o+u3dpjq+sW
T8KOEUt+zwvo/7V3LvSye0rgTBIlDHCNAymg4VMk7BPZ7hm/ELNKjD+Jo2FR3qyH
B5T0Y3HsLuJvW5iB4YlcNHlsdu87kGJ55tukmi8mxdAQ4Q7e2RCOFvu396j3x+UC
B5iPNgiV5+I3lg02dZ77DnKxHZu8A/lJBdiB3QW0KtZB6awBdpUKD9jf1b0SHzUv
KBds0pjBqAlkd25HN7rOrFleaJ1/ctaJxQZBKT5ZPt0m9STJEadao0xAH0ahmbWn
OlFuhjuefXKnEgV4We0+UXgVCwOPjdAvBbI+e0ocS3MFEvzG6uBQE3xDk3SzynTn
jh8BCNAw1FtxNrQHusEwMFxIt4I7mKZ9YIqioymCzLq9gwQbooMDQaHWBfEbwrbw
qHyGO0aoSCqI3Haadr8faqU9GY/rOPNk3sgrDQoo//fb4hVC1CLQJ13hef4Y53CI
rU7m2Ys6xt0nUW7/vGT1M0NPAgMBAAGjQjBAMA4GA1UdDwEB/wQEAwIBBjAPBgNV
HRMBAf8EBTADAQH/MB0GA1UdDgQWBBR5tFnme7bl5AFzgAiIyBpY9umbbjANBgkq
hkiG9w0BAQsFAAOCAgEAVR9YqbyyqFDQDLHYGmkgJykIrGF1XIpu+ILlaS/V9lZL
ubhzEFnTIZd+50xx+7LSYK05qAvqFyFWhfFQDlnrzuBZ6brJFe+GnY+EgPbk6ZGQ
3BebYhtF8GaV0nxvwuo77x/Py9auJ/GpsMiu/X1+mvoiBOv/2X/qkSsisRcOj/KK
NFtY2PwByVS5uCbMiogziUwthDyC3+6WVwW6LLv3xLfHTjuCvjHIInNzktHCgKQ5
ORAzI4JMPJ+GslWYHb4phowim57iaztXOoJwTdwJx4nLCgdNbOhdjsnvzqvHu7Ur
TkXWStAmzOVyyghqpZXjFaH3pO3JLF+l+/+sKAIuvtd7u+Nxe5AW0wdeRlN8NwdC
jNPElpzVmbUq4JUagEiuTDkHzsxHpFKVK7q4+63SM1N95R1NbdWhscdCb+ZAJzVc
oyi3B43njTOQ5yOf+1CceWxG1bQVs5ZufpsMljq4Ui0/1lvh+wjChP4kqKOJ2qxq
4RgqsahDYVvTH9w7jXbyLeiNdd8XM2w9U/t7y0Ff/9yi0GE44Za4rF2LN9d11TPA
mRGunUHBcnWEvgJBQl9nJEiU0Zsnvgc/ubhPgXRR4Xq37Z0j4r7g1SgEEzwxA57d
emyPxgcYxn/eR44/KJ4EBs+lVDR3veyJm+kXQ99b21/+jh5Xos1AnX5iItreGCc=
-----END CERTIFICATE-----
)EOF";

// ── Sensor ────────────────────────────────────────────────────────────────────
float readSensor() {
    int raw = analogRead(SENSOR_PIN);
    float moisture = map(raw, DRY_VALUE, WET_VALUE, 0, 100);
    return constrain(moisture, 0.0, 100.0);
}

// ── WiFi ──────────────────────────────────────────────────────────────────────
bool connectWifi() {
    const int MAX_RETRIES = 3;
    const int RETRY_DELAY_MS = 3000;

    for (int attempt = 1; attempt <= MAX_RETRIES; attempt++) {
        Serial.printf("WiFi attempt %d of %d...\n", attempt, MAX_RETRIES);

        WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

        unsigned long start = millis();
        while (WiFi.status() != WL_CONNECTED) {
            if (millis() - start > WIFI_TIMEOUT_MS) {
                Serial.printf("Attempt %d timed out.\n", attempt);
                WiFi.disconnect();
                break;
            }
            delay(500);
            Serial.print(".");
        }

        if (WiFi.status() == WL_CONNECTED) {
            Serial.printf("\nWiFi connected — IP: %s\n", WiFi.localIP().toString().c_str());
            delay(500);
            return true;
        }

        if (attempt < MAX_RETRIES) {
            Serial.printf("Retrying in %dms...\n", RETRY_DELAY_MS);
            delay(RETRY_DELAY_MS);
        }
    }

    Serial.println("All WiFi attempts failed.");
    return false;
}

// ── Websocket ─────────────────────────────────────────────────────────────────────
bool connectWebSocket(WebsocketsClient& client) {
    const int MAX_RETRIES = 3;
    const int RETRY_DELAY_MS = 2000;

    for (int attempt = 1; attempt <= MAX_RETRIES; attempt++) {
        Serial.printf("WebSocket attempt %d of %d...\n", attempt, MAX_RETRIES);
        bool connected = client.connect(WS_HOST, WS_PORT, WS_PATH);
        delay(1000);
        if (connected) {
            Serial.println("WebSocket connected.");
            return true;
        }

        if (attempt < MAX_RETRIES) {
            Serial.printf("Retrying in %dms...\n", RETRY_DELAY_MS);
            delay(RETRY_DELAY_MS);
        }
    }

    Serial.println("All WebSocket attempts failed.");
    return false;
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

    WebsocketsClient client;
    client.setCACert(ROOT_CA_CERT);

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
        Serial.println("WiFi failed — caching reading.");
        cacheReading(moisture);
        goToSleep();
        return;
    }

    // Attempt WebSocket
    bool connected = connectWebSocket(client);
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
