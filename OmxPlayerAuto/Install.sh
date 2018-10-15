#!/bin/bash

AppName="OmxPlayerAuto"
GithubRepo="bp2008/OmxPlayerAuto"
ExeName="OmxPlayerAuto.exe"

################################################################
# Function: Check if a package exists, and if not, install it.
# Argument 1: Package Name
# Argument 2: Pass "update" to update the package list before 
#             installing the package. This ensures that the 
#             latest version gets installed.
################################################################
InstallPackageIfNotAlready () {
	if [ $(dpkg-query -W -f='${Status}' "$1" 2>/dev/null | grep -c "ok installed") -eq 0 ];
	then
		# Update package list if requested via argument 2
		if [ "$2" = "update" ]
		then
			sudo apt-get update
		fi

		# Install mono-complete
		sudo apt-get install "$1" -y
	fi
}
################################################################
# Function: Echo "1" if the package exists, else echo "0".
#           We echo because bash is ridiculous about return values.
# Argument 1: Package Name
################################################################
IsPackageInstalled () {
	if [ $(dpkg-query -W -f='${Status}' "$1" 2>/dev/null | grep -c "ok installed") -eq 0 ];
	then
		echo "0" # Not installed
	else
		echo "1" # Installed
	fi
}

########################################
# Uninstallation
########################################

uninstallApp () {
	echo Uninstalling $AppName
	if [ $(IsPackageInstalled mono-complete) -eq 1 ]
	then
		while true; do
			read -p "Do you wish to remove the mono framework too? (y/n) " -n 1 -r choice
			echo # just to end the line
			case "$choice" in
				y|Y ) sudo apt-get remove mono-complete -y;break;;
				n|N ) echo "The mono framework will remain installed.";break;;
			esac
		done
	fi
	
	cd ~
	sudo rm -r -f "$AppName"
	sudo rm -f ".config/autostart/$AppName.desktop"
}

########################################
# Installation
########################################

installAndRun () {

echo Beginning installation of $AppName
echo To uninstall, run this script with the argument "-u"

########################################
echo Step 1/3: Install mono framework
########################################

InstallPackageIfNotAlready mono-complete update

#########################################################
echo Step 2/3: Download and extract the latest release
#########################################################

# Navigate to the home directory
cd ~

# Set the latest release URL to a variable named "ReleaseUrl"
ReleaseUrl=$(curl -s https://api.github.com/repos/"$GithubRepo"/releases/latest | grep "\"browser_download_url\"" | cut -d : -f 2,3 | tr -d \")

# Set the release file name to the variable "ReleaseFile"
ReleaseFile=${ReleaseUrl##*/}

# Download the latest release
wget -q -O "$ReleaseFile" $ReleaseUrl

# Ensure that the application directory exists
mkdir -p "$AppName"

# Unzip the release
unzip -q -o $ReleaseFile -d "$AppName"

##########################################################
echo Step 3/3: Configure program to start automatically
##########################################################

# Write start.sh script
# Note: $(pwd) is used here to insert the result of the command "pwd" (e.g. "/home/pi")
cat >"$AppName/start.sh" <<EOL
cd "$(pwd)/$AppName"
mono "$ExeName"
EOL

# Make start.sh executable
chmod u+x "$AppName/start.sh"

# Ensure that "~/.config/autostart" directory exists
mkdir -p ".config/autostart"
	
# Create autostart file
cat >".config/autostart/$AppName.desktop" <<EOL
[Desktop Entry]
Type=Application
Name=$AppName
Exec=lxterminal --command="sudo $(pwd)/$AppName/start.sh" --title="$AppName"
Terminal=false
EOL
	
echo Installation Complete
echo Starting $AppName
sudo ./$AppName/start.sh

}

######################
# Decide what to do
######################

if [ "$1" = "-i" ]; then
	installAndRun
elif [ "$1" = "-u" ]; then
	uninstallApp
else
	echo "This is the $AppName installer. Choose an option:"
	echo # line break
	COLUMNS=12 # I hate linux
	select choice in "Install/Update and run $AppName" "Uninstall $AppName" "Cancel";
	do
		case $choice in
			Install* ) installAndRun;break;;
			Uninstall* ) uninstallApp;break;;
			Cancel ) exit;;
		esac
	done
fi
