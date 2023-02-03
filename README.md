# OmxPlayerAuto
A .net/mono project designed for Raspberry Pi which enables basic remote management of omxplayer instances for the creation of a video wall.

Also Windows compatible since version 1.3.




# Raspberry pi Installation instructions

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




# Windows installation instructions

Download the latest release from [the releases page](https://github.com/bp2008/OmxPlayerAuto/releases), extract wherever you like.

To start it automatically, put a shortcut to `OmxPlayerAuto.exe` in your `Startup` folder and optionally [configure Windows to not require a password upon startup](https://gist.github.com/bp2008/ced5615c6718e35e075d7cabdcdaa7ca).

To start it manually, run `OmxPlayerAuto.exe`.

### Configuration
Access the configuration interface at `http://computer-ip/` *<-- insert your computer's IP address*




# Sample Configurations

### omxplayer
```
omxplayer --lavfdopts probesize:25000 --no-keys --live --timeout 30 --aspect-mode stretch --layer 1 --nohdmiclocksync --avdict rtsp_transport:tcp --win "960 0 1920 540" --crop "400 0 1360 536" "rtsp://user:pass@192.168.0.100/cam/realmonitor?channel=1&subtype=1"
omxplayer --lavfdopts probesize:25000 --no-keys --live --timeout 30 --aspect-mode stretch --layer 1 --nohdmiclocksync --avdict rtsp_transport:tcp --win "960 540 1920 1080" "rtsp://user:pass@192.168.0.101/cam/realmonitor?channel=1&subtype=2"
```

### mpv

The first command uses a cropping function. It is not well documented but the arguments are `w:h:x:y`.

```
"C:\mpv\mpv.exe" --ontop --no-border --no-input-cursor --no-input-default-bindings --no-keepaspect --geometry=960x540+960+0 --vf=crop=960:536:400:0 "rtsp://user:pass@192.168.0.100/cam/realmonitor?channel=1&subtype=1"
"C:\mpv\mpv.exe" --ontop --no-border --no-input-cursor --no-input-default-bindings --no-keepaspect --geometry=960x540+960+540 "rtsp://user:pass@192.168.0.101/cam/realmonitor?channel=1&subtype=2"
```
