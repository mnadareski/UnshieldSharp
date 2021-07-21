# UnshieldSharp

[![Build status](https://ci.appveyor.com/api/projects/status/lk7iopwecojr5ejm?svg=true)](https://ci.appveyor.com/project/mnadareski/unshieldsharp)

This library is a C# port of the following projects:
- [Unshield](https://github.com/twogood/unshield/) - InstallShield CAB information and extractor 
- [unshieldv3](https://github.com/wfr/unshieldv3) - InstallShield v3 (Z) extractor

Both of the above library code has had changes to structure to make them more object-oriented.
For an example of usage, please see [BurnOutSharp](https://github.com/mnadareski/BurnOutSharp).

## Abilities

This code can currently list and extract the contents of all supported files that the base projects can. As more things are added to the source libraries, they will be ported to this as well so the code should be relatively up to date.

## Contributions

Contributions to the project are welcome. Please follow the current coding styles and do not add any proprietary or legally dubious things to the code. Thank you to all of the testers, particularly from the MPF project who helped get this rolling.

## External Libraries

UnshieldSharp uses the following libraries:

- **zlib.net** - [GitHub](https://github.com/cinderblocks/zlib.net)