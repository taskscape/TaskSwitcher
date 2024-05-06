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
- Specify the license under which this software is released, if applicable.

## Contact
- For more information or to report issues, please contact the repository maintainer or submit an issue in the project's issue tracker.
