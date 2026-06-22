# SIGH2O 🌱
A small IoT project for tracking soil moisture in plants. Built using an ESP32, ASP.NET Core + Blazor, and PostgreSQL.

## what you'll need 🐞

### hardware 🪵
- ESP32 Dev Board
- Capacitive Soil Moisture Sensor
- Jumper wires
- USB data cable + 5V power brick

### software & services 🌺
- [Docker](https://docs.docker.com/compose/install/) (to run app + database)
- [Arduino IDE](https://www.arduino.cc/en/software) (for ESP32)
- [Pushover](https://pushover.net/) account (for notifications)

## getting started 🌞

### backend & database ☄️
1. Create a `.env` file with your credentials (see below)
2. Run `docker compose up --build` in the project root
3. The app will be available at `http://localhost` and WebSocket at `ws://localhost:8080`

### ESP32 firmware 🫧
1. Install the `ArduinoWebsockets` library in Arduino IDE
2. Create a `secrets.h` file with your WiFi and server details (see below)
3. Flash `socketCycle.ino` to your board

### configuration 🐝

**.env** (project root)
```bash
PUSHOVER_USER=your_pushover_user_key
PUSHOVER_TOKEN=your_pushover_app_token
DB_PASSWORD=your_postgres_password
```

**secrets.h** (ESP32 sketch)
```cpp
const char* WIFI_SSID      = "your_wifi_ssid";
const char* WIFI_PASSWORD  = "your_wifi_password";
const char* WS_HOST        = "your_server_ip";  // server IP address
const int   WS_PORT        = 8080;              // container's exposed port
```

## future improvements !! 🌈 
- setup nginx (reverse proxy) to hide the container's ports from others on the network **(done)**
- whitelist ESP32 address (**in progress**)
- deploy app using free DigitalPlat domain registration! **(*paused* but done)**
- filterable timeline in chart by date
