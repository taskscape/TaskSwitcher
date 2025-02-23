# TaskSwitcher Solution

## Overview
The TaskSwitcher solution is designed to facilitate efficient switching between tasks on a Windows environment. This solution includes several projects that work together to provide a comprehensive task switching application.

## Projects in the Solution
1. **TaskSwitcher** - The main application that provides the user interface and integration logic for task switching.
2. **ManagedWinapi** - A library project that wraps Windows API calls needed for managing windows and tasks.
3. **Core** - Contains core functionalities and business logic used across the application.
4. **Core.UnitTests** - Contains unit tests for the Core project to ensure functionality works as expected.
5. **.build** - A project dedicated to build scripts and tasks, including MSBuild community tasks.

## Prerequisites
- Microsoft Visual Studio 2013 or later.
- .NET Framework as specified in each project file.

## Building the Solution
1. Open `TaskSwitcher.sln` with Visual Studio.
2. Ensure all project dependencies are restored (e.g., NuGet packages).
3. Build the solution by selecting `Build -> Build Solution` from the menu.

## Running the Application
- After building, run the TaskSwitcher project by setting it as the startup project and pressing `F5` or selecting `Debug -> Start Debugging`.

## Configuration
- The solution configurations for Debug and Release modes are defined under the GlobalSection of the solution file. Each project has specific configurations for building in these modes.

## Contributing
- Contributions are welcome. Please fork the repository, make your changes, and submit a pull request.

## License
This software is available under dual licensing options:

Open Source License: GNU Affero General Public License (AGPL) You can use, modify, and distribute the software for free under the terms of the GNU Affero General Public License (AGPL), which is included in the LICENSE file of this repository. This option is ideal for developers who wish to use the software in other open source projects or for personal use.

Commercial License: If you want to use this software in a commercial application or require additional features and support not available under the open source license, you must obtain a commercial license. The commercial license allows for private modifications and grants you access to premium features and support services.

Obtaining a Commercial License
To obtain a commercial license or to inquire about pricing and terms, please contact us at taskscape.com.

Why Dual Licensing?
Dual licensing allows us to support the open source community while also providing a commercial offering that meets the needs of businesses requiring advanced features and dedicated support. This model helps fund the continued development and maintenance of the software.

## Contact
- For more information or to report issues, please contact the repository maintainer or submit an issue in the project's issue tracker.
