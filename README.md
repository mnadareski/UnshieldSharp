# UnshieldSharp

[![Build status](https://ci.appveyor.com/api/projects/status/lk7iopwecojr5ejm?svg=true)](https://ci.appveyor.com/project/mnadareski/unshieldsharp)

C# port of the InstallShield CAB information and extractor [Unshield](https://github.com/twogood/unshield/) with changes to structure to make it more object-oriented. This currently compiles as a library. For an example of usage, please see [BurnOutSharp](https://github.com/mnadareski/BurnOutSharp).

## Abilities

This code can currently list and extract the contents of all InstalShield CAB files that the base project can. As more things are added to the C library, they will be ported to this as well so the code should be relatively up to date.

## Contributions

Contributions to the project are welcome. Please follow the current coding styles and do not add any proprietary or legally dubious things to the code. Thank you to all of the testers, particularly from the MPF project who helped get this rolling.

## External Libraries

UnshieldSharp uses imports the following libraries:

- **zlib.net** - [GitHub](https://github.com/cinderblocks/zlib.net)