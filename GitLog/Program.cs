using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using Serilog;

using Spectre.Console;

using AnsiConsole = Spectre.Console.AnsiConsole;
using Color = Spectre.Console.Color;


Console.OutputEncoding = Encoding.UTF8;

// Logger setup
Log.Logger = new LoggerConfiguration().MinimumLevel.Information().
									   WriteTo.Console().
									   WriteTo.File("ApplicationLog.txt", rollingInterval: RollingInterval.Day).
									   WriteTo.Debug().
									   CreateLogger();
try {
	PrintHeader();
	await MainLoopAsync();
} catch (Exception ex) {
	Log.Error(ex, "Unexpected error");
	AnsiConsole.MarkupLine("[red]❌ Unexpected error occurred. Check logs for details.[/]");
} finally {
	Log.CloseAndFlush();
}
return;
void PrintHeader() {
	AnsiConsole.Write(new FigletText("Git Log Tool").LeftJustified().
													 Color(new Color(red: 0, green: 255, blue: 255)) // Cyan RGB
					 );
	AnsiConsole.MarkupLine("[grey]Version 1.0 - Developed by Houman[/]");
	AnsiConsole.WriteLine();
}
async Task MainLoopAsync() {
	while (true) {
		string? gitDirectory = await GetValidGitDirectoryAsync();
		if (gitDirectory == null)
			return;
		string filePath = await GenerateGitLogAsync(gitDirectory);
		AnsiConsole.MarkupLine("\n[green]\u2714 Git log generated successfully![/]");
		AnsiConsole.MarkupLine($"[blue]Saved to:[/] {filePath}");

		// Menu
		string choice = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("\nWhat do you want to do next?").
																		 AddChoices("📂 Open the generated log file",
																					"📁 Select another Git repository",
																					"❌ Exit"));
		if (choice.StartsWith("📂")) {
			try {
				Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
			} catch {
				AnsiConsole.MarkupLine("[yellow]⚠ Could not open the file.[/]");
			}
		} else if (choice.StartsWith("❌")) {
			AnsiConsole.MarkupLine("[cyan]👋 Goodbye![/]");
			return;
		}
	}
}
static async Task<string?> GetValidGitDirectoryAsync() {
	while (true) {
		string path =
			AnsiConsole.Ask<string>("📂 [yellow]Enter the path to a Git repository[/] (or type 'exit' to quit):");
		if (string.Equals(path, "exit", StringComparison.OrdinalIgnoreCase))
			return null;
		if (string.IsNullOrWhiteSpace(path) ||
			!Directory.Exists(path)) {
			AnsiConsole.MarkupLine("[yellow]⚠ Invalid path. Please enter a valid directory.[/]");
			continue;
		}
		if (!Directory.Exists(Path.Combine(path, ".git"))) {
			AnsiConsole.MarkupLine("[yellow]⚠ No Git repository found in the specified directory.[/]");
			continue;
		}
		Log.Information("Valid Git repository found at {Path}", path);
		return path;
	}
}
async Task<string> GenerateGitLogAsync(string gitDirectory) {
	Log.Information("Generating git log for {Path}", gitDirectory);
	string originalDirectory = Directory.GetCurrentDirectory();
	Directory.SetCurrentDirectory(gitDirectory);
	try {
		AnsiConsole.Status().
					Spinner(Spinner.Known.Dots).
					Start("Fetching Git data...",
						  action: ctx => {
									  Task.Delay(1500).Wait(); // Just visual effect
								  });
		string   lastCommitInfo = await ExecuteGitCommandAsync("log -1 --pretty=%H_%s");
		string[] commitParts    = lastCommitInfo.Split(separator: '_', count: 2);
		string   commitHash     = commitParts[0];
		string   commitMessage  = SanitizeFileName(commitParts.Length > 1 ? commitParts[1] : "NoMessage");
		if (commitMessage.Length > 50)
			commitMessage = commitMessage.Substring(startIndex: 0, length: 50) + "...";
		string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		string logsFolder = Path.Combine(gitDirectory, "Logs");
		Directory.CreateDirectory(logsFolder);
		string outputFile         = Path.Combine(logsFolder, $"GitLog_{timestamp}_{commitHash}_{commitMessage}.txt");
		string statusOutput       = await ExecuteGitCommandAsync("status --porcelain");
		string stagedDiffOutput   = await ExecuteGitCommandAsync("diff --cached --name-status");
		string unstagedDiffOutput = await ExecuteGitCommandAsync("diff --name-status");
		await WriteChangesToFileAsync(statusOutput.Split(separator: '\n', StringSplitOptions.RemoveEmptyEntries),
									  stagedDiffOutput,
									  unstagedDiffOutput,
									  outputFile);
		return outputFile;
	} finally {
		Directory.SetCurrentDirectory(originalDirectory);
	}
}
static string SanitizeFileName(string input) {
	string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()) + "&");
	string sanitized    = Regex.Replace(input, $"[{invalidChars}]", "_").Replace(" ", "-").Trim();
	return string.IsNullOrWhiteSpace(sanitized) ? "NoCommitMessage" : sanitized;
}
static async Task<string> ExecuteGitCommandAsync(string arguments) {
	using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
	ProcessStartInfo processInfo = new ProcessStartInfo {
															FileName               = "git",
															Arguments              = arguments,
															RedirectStandardOutput = true,
															RedirectStandardError  = true,
															UseShellExecute        = false,
															CreateNoWindow         = true
														};
	using Process process = new Process { StartInfo = processInfo };
	process.Start();
	string output = await process.StandardOutput.ReadToEndAsync();
	string error  = await process.StandardError.ReadToEndAsync();
	if (!process.WaitForExit(10000)) {
		process.Kill();
		throw new TimeoutException($"Git command timed out: git {arguments}");
	}
	if (process.ExitCode != 0)
		throw new InvalidOperationException($"Git command failed: {error}");
	return output.Trim();
}
static async Task WriteChangesToFileAsync(string[] statusLines,
										  string   stagedDiffOutput,
										  string   unstagedDiffOutput,
										  string   outputFile) {
	StringBuilder sb = new StringBuilder();
	sb.AppendLine("=== Staged Changes ===");
	IEnumerable<string> stagedFiles = statusLines.Where(line => !line.StartsWith("??"));
	sb.AppendLine(stagedFiles.Any() ? string.Join(Environment.NewLine, stagedFiles) : "No staged changes.");
	sb.AppendLine("\n=== Staged Modified Files ===");
	sb.AppendLine(!string.IsNullOrWhiteSpace(stagedDiffOutput) ? stagedDiffOutput : "No staged modifications.");
	sb.AppendLine("\n=== Unstaged Changes ===");
	IEnumerable<string> unstagedFiles = statusLines.Where(line => line.StartsWith("??"));
	sb.AppendLine(unstagedFiles.Any() ? string.Join(Environment.NewLine, unstagedFiles) : "No unstaged changes.");
	sb.AppendLine("\n=== Unstaged Modified Files ===");
	sb.AppendLine(!string.IsNullOrWhiteSpace(unstagedDiffOutput) ? unstagedDiffOutput : "No unstaged modifications.");
	await File.WriteAllTextAsync(outputFile, sb.ToString());
}