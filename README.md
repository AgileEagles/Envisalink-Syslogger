# Envisalink-Syslogger
Server and Logging program to receive SysLog Messages from the EyesOn EnvisaLink 4 (EVL4) Module

# Description 
* This is a simple server that listens for SysLog messages from the EyesOn EnvisaLink 4 (EVL4) Module\
	* Tested in the format that comes from the Honeywell Vista 20P and 21iP panels. 
* It logs the messages to a file 
	* After 10 minutes it will upload an event to a google drive account as an HTML file. 
	* The server is written in C# and uses sockets to handle incoming messages and log them to a file.
* Most of this was written from Copilot suggestions. 
* To run the program, you need visual studio and to build it and to connect to the google drive account
	* Some notes I took from CoPilot and while I set it up in my g-drive account are in doc\notes.txt.
	* Create a shortcut for startup with this command in it to run it silently:
		* C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -WindowStyle Hidden -Command "Start-Process -FilePath 'C:\Users\ ... \ ... \EnvisalinkSysLogger.exe' -WindowStyle Hidden"

sample of the Zones.csv file, the colors are for the html report. 

Zone,Label,BackgroundColor,ForegroundColor
1,FIRE!,#FF3333,#FFFFFF
2,Front Door,#CCFFCC,#000000
etc...