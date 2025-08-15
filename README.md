# GitLog Tool
A lightweight C# console application to generate detailed Git repository logs with a user-friendly interface, powered by Spectre.Console and Serilog.

## Overview
GitLog is a tool designed to help developers quickly generate and save detailed Git repository logs, including staged and unstaged changes, with a clean and interactive console interface. It’s perfect for tracking repository changes and creating log files for documentation or analysis.

### Features
- **Interactive Console**: Built with Spectre.Console for a modern, colorful, and user-friendly experience.
- **Git Integration**: Fetches repository status, staged, and unstaged changes using Git commands.
- **Logging**: Uses Serilog for minimal, precise logging to console, file, and debug outputs.
- **File Output**: Generates timestamped log files in a dedicated `Logs` folder with sanitized commit-based naming.
- **Cross-Platform**: Built on .NET 8.0, compatible with Windows, macOS, and Linux.

## Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git installed and accessible from the command line
- A Git repository to analyze

### Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/FarokhiHouman/GitLog.git
   ```
2. Navigate to the project directory:
   ```bash
   cd GitLog
   ```
3. Restore dependencies and build:
   ```bash
   dotnet restore
   dotnet build
   ```

### Usage
1. Run the application:
   ```bash
   dotnet run
   ```
2. Enter the path to a valid Git repository when prompted (or type `exit` to quit).
3. The tool will generate a log file in the `Logs` folder within the repository, named with the latest commit hash and message.
4. Choose to:
   - Open the generated log file.
   - Select another repository.
   - Exit the application.

### Example Output
A generated log file (`GitLog_20250812_123456_{commitHash}_{commitMessage}.txt`) contains:
```
=== Staged Changes ===
M  src/Program.cs
A  README.md

=== Staged Modified Files ===
M  src/Program.cs
A  README.md

=== Unstaged Changes ===
?? docs/notes.txt

=== Unstaged Modified Files ===
No unstaged modifications.
```

## Project Structure
- `Program.cs`: Main application logic, handling user input, Git commands, and log generation.
- `GitLog.csproj`: Project file with dependencies (Serilog, Spectre.Console).
- `ApplicationLog.txt`: Rolling log file for Serilog output.
- `Logs/`: Folder for generated Git log files.

## Dependencies
- [Serilog](https://serilog.net/): For structured logging (Console, File, Debug).
- [Spectre.Console](https://spectreconsole.net/): For enhanced console UI with prompts and spinners.
- .NET 8.0: Core framework for cross-platform compatibility.

## Contributing
Contributions are welcome! Please:
1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/AmazingFeature`).
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4. Push to the branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments
- Built with ❤️ by [Houman Farokhi](https://github.com/FarokhiHouman).
- Thanks to the open-source community for tools like Serilog and Spectre.Console.
