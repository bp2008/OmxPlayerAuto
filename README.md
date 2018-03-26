# OmxPlayerAuto
A .net/mono project designed for Raspberry Pi which enables basic remote management of omxplayer instances for the creation of a video wall.

##Installation instructions

###Prerequisites

I recommend you use a recent version of the Raspbian operating system, and have it configured to boot directly to the desktop without requiring authentication.

You need the mono runtime to be installed. You can install it by opening a terminal window and entering the following command:
```
sudo apt-get install mono-complete
```
###Installation

1) Create a new folder to contain the program files. e.g. "/home/pi/omxplayerauto"
2) Download or copy an OmxPlayerAuto release to this new folder.
3) Open a terminal in the folder (one way to do this is through the menu: Tools > Open Current Folder in Terminal)
4) Run the unzip command:
```
unzip OmxPlayerAuto_1.0.zip
```
5) Start OmxPlayerAuto.exe with this command:
```
sudo mono OmxPlayerAuto.exe
```
At this point, the program should start its embedded web server and wait for you to configure it.
6) Enter your Raspberry Pi's IP address into any web browser on your network. If you don't know how to find your Pi's IP address, google it.

If all goes well you should get an ugly web page that looks like this:
![screenshot](http://i.imgur.com/NMlSeFim.jpg)

###Starting the program automatically when the pi boots up

There are many ways to do this. I'll show you how I do it on my system.

1) Create a new empty file called "start.sh". This can go anywhere, in your home directory perhaps. For this example, we'll put it in "/home/pi/omxplayerauto".

2) Open start.sh in a basic text editor (Right click the start.sh file and choose Text Editor or LeafPad or whatever you've got). Enter the following content, then close and save the file.
```
cd /home/pi/omxplayerauto
mono OmxPlayerAuto.exe
```
3) Give yourself Execute permission for start.sh. One way to do this is to right click the file and go to Properties, Permissions tab, and allow Anyone to Execute the file.

4) Next, we create an autostart directive in "/home/pi/.config/autostart/". In Linux, file and folder names starting with a period are hidden, so we need to show hidden objects first. In a file explorer window, click on the View menu, then click Show Hidden.

5) Now navigate to "/home/pi/.config/autostart/". If the autostart folder does not exist, you must create it yourself.

6) Create a new empty file here and name it "OPA.desktop". (any file name should be fine, but I think it needs to end in ".desktop")

7) Open the file in a basic text editor, and enter the content:
```
[Desktop Entry]
Type=Application
Name=OmxPlayerAuto
Exec=lxterminal --command "sudo /home/pi/omxplayerauto/start.sh"
Terminal=false
```
Close and save the file.

8) Reboot the pi, and shortly after the desktop appears, a terminal window should open and start OmxPlayerAuto.
