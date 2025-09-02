# UnshieldSharp

[![Build and Test](https://github.com/mnadareski/UnshieldSharp/actions/workflows/build_and_test.yml/badge.svg)](https://github.com/mnadareski/UnshieldSharp/actions/workflows/build_and_test.yml)

This program is a wrapper around a C# port of [Unshield](https://github.com/twogood/unshield/), an InstallShield CAB information and extractor. The library code has had changes to structure to make it more object-oriented.

This code used to compile to a library, but all functionality included is now in [SabreTools.Serialization](https://github.com/SabreTools/SabreTools.Serialization). Do not use old versions of the package as there are critical issues found and fixed since it was integrated.

## Releases

For the most recent stable build, download the latest release here: [Releases Page](https://github.com/mnadareski/UnshieldSharp/releases)

For the latest WIP build here: [Rolling Release](https://github.com/mnadareski/UnshieldSharp/releases/tag/rolling)

## Abilities

This code can currently list and extract the contents of all supported files that the base projects can. As more things are added to the source libraries, they will be ported to this as well so the code should be relatively up to date.

## Contributions

Contributions to the project are welcome. Please follow the current coding styles and do not add any proprietary or legally dubious things to the code. Thank you to all of the testers, particularly from the MPF project who helped get this rolling.
