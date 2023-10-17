## Rain World "Personalizer"
This is a Rain World mod that personalizes your Rain World experience using state-of-the-art A.I.
The personalization algorithm enhances your game using your personal data which includes:
- Your operating system
- Your computer's username
- Installed mods
- The date you installed Rain World
- Devices connected to your computer
- Your hostname
- Your MAC address
- Your IP address
- Your full name
- Your favorite food
- The time you slept today
- Your birthdate
- Your social security number
- People you have murdered
- e.t.c.

## FAQ
**Q: Where does my personal information come from?**

The personal information we use to enhance your Rain World experience comes from our benefactors.

**Q: Who are your benefactors?**

Our benefactors are people who support this project and are generous enough to provide us money.

**Q: Who specifically are your benefactors?**

Our benefactors are people who support this project and are generous enough to provide us money.

**Q: What are the names of your benefactors?**

Our benefactors are people who support this project and are generous enough to provide us money.

**Q: you're not answering my question**

Server error

## Features
No, this mod doesn't actually personalize your Rain World experience using state-of-the-art A.I... yet.
I'm just having too much fun writing this readme. In actuality, it just does
various miscellaneous things. Current features include:
- Blue Lizards, Caramel Lizards, and Grapple Worms have a chance to be "shiny" (i.e., differently colored)
- You may encounter "Larry" (configurable through Spawn Chance in the Remix config)

In the future I may add more funny miscellaneous things.

## Building
First, this project will reference assemblies directly from the Rain World install directory.
I'm too ignorant to figure out if there's anything that could potentially go wrong with that.
You must set an environment variable "RainWorldDir" to the Rain World install directory before building.

PowerShell (Windows):
```powershell
# if installed through Steam
$env:RainWorldDir = "C:\Program Files (x86)\Steam\steamapps\common\Rain World"
```
Bash (Linux):
```bash
# i don't have steam on linux i just searched this up
export RainWorldDir="~/.steam/root/steamapps/common/Rain World"
```

Then to build the project, run these commands:
```bash
# install cake build tool for this repository
# (only needs to be run once)
dotnet tool restore

# build the project
dotnet cake
```