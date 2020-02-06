# OmxPlayerAuto
A .net/mono project designed for Raspberry Pi which enables basic remote management of omxplayer instances for the creation of a video wall.

## Installation instructions

### Prerequisites

I recommend you use a recent version of the Raspbian operating system, and have it configured to boot directly to the desktop without requiring authentication.

### Installation Script

Run these commands on your pi:

```
wget https://raw.githubusercontent.com/bp2008/OmxPlayerAuto/master/OmxPlayerAuto/OmxPlayerAuto_Install.sh
chmod u+x OmxPlayerAuto_Install.sh
./OmxPlayerAuto_Install.sh
```

The installation script will ask if you wish to install or uninstall.  Once installed, OmxPlayerAuto will start automatically upon booting to the desktop.

### Configuration

Access the configuration interface at `http://raspberry-pi-ip/` *<-- insert your pi's IP address*
