#!/bin/bash

# This file gets run to launch Dynaframe from the autostart command on Linux/Raspberry pi systems
# This is needed to get the 'working directory' synced up. I'm keeping it because I realize it can be used
# to also shim in other fixes without affecting the main codebase which are linux specific before execution, such as turning off
# sleep, or possibly syncing files.
cd /home/pi/Dynaframe
echo "starting Dynaframe" >> /home/pi/Dynaframe/logs/run.sh.log
./Dynaframe > /home/pi/Dynaframe/logs/dynaframe.log 2>&1