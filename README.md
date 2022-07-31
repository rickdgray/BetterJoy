<p align="center">
  <img src="title.png">
</p>

## What this project is:
* _Nintendo Switch_ controller support without the hassle!
* Cross platform/OS agnostic!
* Modernized on .NET 6!
* Refactored/despaghettied to clean, extensible architecture!
* N64 controller support!

## What this project is not:
* Working
  * I'm working towards a minimum viable product right now. I hope to have my first release soon!
* Prettier
  * Going OS agnostic required doing away with Winforms. I have some ideas for a replacement for the UI such as MAUI, but for the time being, this will be a background application only.
* Feature parity with the original _BetterJoy_
  * 3rd-party controller support doesn't feel like a very useful addition to me, especially given the complexity added. This project is for _Nintendo Switch_ controllers only.
  * Virtual Dualshock controller support also seems like unnecessary overhead when Xbox controllers are so widely supported and are the de facto standard.
  * USB direct connection support is something I can imagine would be useful for those without a bluetooth adapter. This will not be supported for the time being, however.
* Supported for 32-bit systems
  * No one needs this anymore

## Installation
1. Install the latest version of [ViGEmBus](https://github.com/ViGEm/ViGEmBus/releases)
2. Install the latest version of [HidHide](https://github.com/ViGEm/HidHide/releases)
3. Run EvenBetterJoy
4. Connect your controllers.

## Connecting a Controller
* To pair:
  1. Hold down the small sync button on the top of the controller for 5 seconds
  2. Search for it in your bluetooth settings and pair normally.
* To disconnect:
  * Single press the sync button
* To reconnect:
  * Press any button on your controller

## Acknowledgements
[Benjamin Höglinger-Stelzer](https://github.com/nefarius)'s products, on which much of _EvenBetterJoy_ is built on, is incredible. He deserves all the praise for his fantastic libraries.

[David Khachaturov](https://davidobot.net/)'s work on [BetterJoy](https://github.com/Davidobot/BetterJoy) was a huge stepping stone; I would not have been able to put this project in motion if not for his work.
