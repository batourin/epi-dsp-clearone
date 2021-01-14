PepperDash Essentials plugin for ClearOne Pro (1st generation) DSP

## License

Provided under MIT license

## Overview

Implementation of the Essential plugin to bridge ClearOne Pro first generation DSPs

Plugin adds some informational Console commands in format <key><command>: INFO

Example of usage in the configuration file:
```
       {
        "key": "dsp",
        "uid": 10,
        "name": "DSP",
        "type": "clearonedsp",
        "group": "audio",
        "properties": {
          "control": {
            "comParams": {
              "hardwareHandshake": "None",
              "parity": "None",
              "protocol": "RS232",
              "baudRate": 57600,
              "dataBits": 8,
              "softwareHandshake": "None",
              "stopBits": 1
            },
            "controlPortNumber": 1,
            "controlPortDevKey": "cp3",
            "method": "Com"
          },
          "levels": {
            "mic": {
              "label": "Mic Volume",
              "DeviceType": "ClearOne880",
              "DeviceId": "0",
              "Group": "Processing",
              "Channel": "A"
            },
            "master": {
              "label": "Master Volume",
              "DeviceType": "ClearOne880",
              "DeviceId": "0",
              "Group": "Processing",
              "Channel": "B"
            }
        }
```

## Cloning Instructions

After forking this repository into your own GitHub space, you can create a new repository using this one as the template.  Then you must install the necessary dependencies as indicated below.

## Dependencies

The [Essentials](https://github.com/PepperDash/Essentials) libraries are required. They referenced via nuget. You must have nuget.exe installed and in the `PATH` environment variable to use the following command. Nuget.exe is available at [nuget.org](https://dist.nuget.org/win-x86-commandline/latest/nuget.exe).

### Installing Dependencies

To install dependencies once nuget.exe is installed, run the following command from the root directory of your repository:
`nuget install .\packages.config -OutputDirectory .\packages -excludeVersion`.
To verify that the packages installed correctly, open the plugin solution in your repo and make sure that all references are found, then try and build it.

### Installing Different versions of PepperDash Core

If you need a different version of PepperDash Core, use the command `nuget install .\packages.config -OutputDirectory .\packages -excludeVersion -Version {versionToGet}`. Omitting the `-Version` option will pull the version indicated in the packages.config file.

### Instructions for Renaming Solution and Files

See the Task List in Visual Studio for a guide on how to start using the templage.  There is extensive inline documentation and examples as well.

For renaming instructions in particular, see the XML `remarks` tags on class definitions
