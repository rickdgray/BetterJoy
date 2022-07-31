<p align="center">
  <img src="title.png">
</p>

The famous [BetterJoy](https://github.com/Davidobot/BetterJoy), but even better!

## What this project is:
* _Nintendo Switch_ controller support without the hassle!
* Cross platform/OS agnostic!
* Modernized on .NET 6!
* Refactored/despaghettied to clean, extensible architecture!
* N64 controller support!

## What this project is not:
* Prettier
  * Going OS agnostic required doing away with Winforms. I have some ideas for a replacement for the UI such as MAUI, but for the time being, this will be a terminal-only application.
* Feature parity with the original _BetterJoy_
  * 3rd-party controller support doesn't feel like a very useful addition to me, especially given the complexity added. This project is for _Nintendo Switch_ controllers only.
* Supported for 32-bit systems
  * No one needs this anymore
* Supported for USB direct connection
  * I may come back to this one if there's desire, but my guess is that everyone will just use bluetooth as it is better.

## Installation
To be documented
<!--1. Install [ViGEmBus](https://github.com/ViGEm/ViGEmBus/releases)
2. Install HIDGuard
3. Run EvenBetterJoy
4. Connect your controllers.-->

## Connecting a Controller
* To pair:
  1. Hold down the small sync button on the top of the controller for 5 seconds
  2. Search for it in your bluetooth settings and pair normally.
* To disconnect:
  * Single press the sync button
* To reconnect:
  * Press any button on your controller

## Acknowledgements
[David](https://davidobot.net/)'s work on BetterJoy was a huge stepping stone; I would not have been able to put this project in motion if not for his work.

For posterity, I will also place his acknowledgements below as their contributions were also enormous and I would be remiss to have them forgotten!

A massive thanks goes out to [rajkosto](https://github.com/rajkosto/) for putting up with 17 emails and replying very quickly to my silly queries. The UDP server is also mostly taken from his [ScpToolkit](https://github.com/rajkosto/ScpToolkit) repo.

Also I am very grateful to [mfosse](https://github.com/mfosse/JoyCon-Driver) for pointing me in the right direction and to [Looking-Glass](https://github.com/Looking-Glass/JoyconLib) without whom I would not be able to figure anything out. (being honest here - the joycon code is his)

Many thanks to [nefarius](https://github.com/ViGEm/ViGEmBus) for his ViGEm project! Apologies and appreciation go out to [epigramx](https://github.com/epigramx), creator of *WiimoteHook*, for giving me the driver idea and for letting me keep using his installation batch script even though I took it without permission. Thanks go out to [MTCKC](https://github.com/MTCKC/ProconXInput) for inspiration and batch files.

A last thanks goes out to [dekuNukem](https://github.com/dekuNukem/Nintendo_Switch_Reverse_Engineering) for his documentation, especially on the SPI calibration data and the IMU sensor notes!

Massive *thank you* to **all** code contributors!