using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using Serilog;

using Spectre.Console;

using AnsiConsole = Spectre.Console.AnsiConsole;
using Color = Spectre.Console.Color;


Console.OutputEncoding = Encoding.UTF8;

// Minimal logging for maximum insight
Log.Logger = new LoggerConfiguration().MinimumLevel.Information().
									   WriteTo.Console().
									   WriteTo.File("ApplicationLog.txt", rollingInterval: RollingInterval.Day).
									   WriteTo.Debug().
									   CreateLogger();
try {
	PrintHeader();
	await MainLoopAsync();
} catch (Exception ex) {
	Log.Error(ex, "Unexpected error occurred");
	AnsiConsole.MarkupLine("[red]❌ An unexpected error occurred. Please check the logs at ApplicationLog.txt.[/]");
} finally {
	Log.CloseAndFlush();
}
void PrintHeader() {
	// Stunning header with a modern, professional look
	AnsiConsole.Write(new FigletText("Git Log Pro").Centered().
													Color(new Color(red: 0, green: 191, blue: 255)) // DeepSkyBlue
					 );
	Table headerTable = new Table().AddColumn(new TableColumn("[grey]Version 1.2[/]").Centered()).
									AddColumn(new TableColumn("[grey]Crafted by Houman Farokhi[/]").Centered()).
									Border(TableBorder.Rounded).
									BorderStyle(new Style(foreground: new Color(red: 0,
																				green: 255,
																				blue: 255))); // Cyan
	AnsiConsole.Write(headerTable);
	AnsiConsole.MarkupLine("[cyan bold]Your ultimate tool for stylish Git log generation! 🚀[/]");
	AnsiConsole.WriteLine();
}
async Task MainLoopAsync() {
	while (true) {
		string? gitDirectory = await PromptForGitDirectoryAsync();
		if (gitDirectory == null) {
			Log.Information("User exited the application");
			AnsiConsole.MarkupLine("[cyan]👋 Thank you for using Git Log Pro! See you soon![/]");
			return;
		}
		(string filePath, string previewContent) = await GenerateAndPreviewGitLogAsync(gitDirectory);
		DisplayLogPreview(previewContent);
		if (!await ConfirmSaveAsync(filePath))
			continue;
		DisplaySuccessMessage(filePath);
		string choice = ShowMainMenu();
		if (!await HandleMenuChoiceAsync(choice, filePath))
			return;
	}
}
async Task<string?> PromptForGitDirectoryAsync() {
	// Enhanced prompt with validation and user-friendly feedback
	TextPrompt<string> prompt = new TextPrompt<string>("📂 [yellow]Enter Git repository path[/] (or 'exit' to quit):").
								PromptStyle("cyan").
								Validate(path => {
											 if (string.Equals(path, "exit", StringComparison.OrdinalIgnoreCase))
												 return ValidationResult.Success();
											 if (string.IsNullOrWhiteSpace(path) ||
												 !Directory.Exists(path))
												 return ValidationResult.
													 Error("[yellow]⚠ Please enter a valid directory path.[/]");
											 if (!Directory.Exists(Path.Combine(path, ".git")))
												 return ValidationResult.
													 Error("[yellow]⚠ No Git repository found at this path.[/]");
											 return ValidationResult.Success();
										 });
	string path = AnsiConsole.Prompt(prompt);
	if (string.Equals(path, "exit", StringComparison.OrdinalIgnoreCase))
		return null;
	Log.Information("Valid Git repository found at {Path}", path);
	return path;
}
async Task<(string filePath, string previewContent)> GenerateAndPreviewGitLogAsync(string gitDirectory) {
	Log.Information("Initiating Git log generation for {Path}", gitDirectory);
	string originalDirectory = Directory.GetCurrentDirectory();
	Directory.SetCurrentDirectory(gitDirectory);
	try {
		return await FetchAndProcessGitDataAsync(gitDirectory);
	} finally {
		Directory.SetCurrentDirectory(originalDirectory);
	}
}
async Task<(string filePath, string previewContent)> FetchAndProcessGitDataAsync(string gitDirectory) {
	return await AnsiConsole.Progress().
							 Columns(new TaskDescriptionColumn(),
									 new SpinnerColumn(Spinner.Known.Star2),
									 new PercentageColumn(),
									 new ElapsedTimeColumn()).
							 StartAsync(async ctx => {
											ProgressTask task = ctx.AddTask("[green]Processing Git data...[/]",
																			new ProgressTaskSettings {
																										 MaxValue = 100
																									 });
											task.Increment(10);
											string lastCommitInfo =
												await ExecuteGitCommandAsync("log -1 --pretty=%H_%s");
											task.Increment(30);
											(string commitHash, string commitMessage) = ParseCommitInfo(lastCommitInfo);
											task.Increment(20);
											string outputFile =
												BuildOutputFilePath(gitDirectory, commitHash, commitMessage);
											task.Increment(10);
											string statusOutput =
												await ExecuteGitCommandAsync("status --porcelain");
											string stagedDiffOutput =
												await ExecuteGitCommandAsync("diff --cached --name-status");
											string unstagedDiffOutput =
												await ExecuteGitCommandAsync("diff --name-status");
											task.Increment(20);
											string previewContent =
												await BuildLogContentAsync(statusOutput,
																		   stagedDiffOutput,
																		   unstagedDiffOutput);
											task.Increment(10);
											return (outputFile, previewContent);
										});
}
async ValueTask<string> ExecuteGitCommandAsync(string arguments) {
	// Minimal logging for Git command execution
	Log.Information("Running Git command: git {Arguments}", arguments);
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
		Log.Error("Git command timed out: git {Arguments}", arguments);
		throw new TimeoutException($"Git command timed out: git {arguments}");
	}
	if (process.ExitCode != 0) {
		Log.Error("Git command failed: {Error}", error);
		throw new InvalidOperationException($"Git command failed: {error}");
	}
	return output.Trim();
}
(string commitHash, string commitMessage) ParseCommitInfo(string lastCommitInfo) {
	// Simplified commit parsing with LINQ
	string[] commitParts   = lastCommitInfo.Split(separator: '_', count: 2);
	string   commitHash    = commitParts[0];
	string   commitMessage = commitParts.Length > 1 ? SanitizeFileName(commitParts[1]) : "NoMessage";
	if (commitMessage.Length > 50)
		commitMessage = commitMessage[..50] + "...";
	return (commitHash, commitMessage);
}
string BuildOutputFilePath(string gitDirectory, string commitHash, string commitMessage) {
	string timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
	string logsFolder = Path.Combine(gitDirectory, "Logs");
	Directory.CreateDirectory(logsFolder);
	return Path.Combine(logsFolder, $"GitLog_{timestamp}_{commitHash}_{commitMessage}.txt");
}
async ValueTask<string> BuildLogContentAsync(string statusOutput, string stagedDiffOutput, string unstagedDiffOutput) {
	// Use LINQ for cleaner status processing
	string[]      statusLines   = statusOutput.Split(separator: '\n', StringSplitOptions.RemoveEmptyEntries);
	List<string>  stagedFiles   = statusLines.Where(line => !line.StartsWith("??")).ToList();
	List<string>  unstagedFiles = statusLines.Where(line => line.StartsWith("??")).ToList();
	StringBuilder sb            = new StringBuilder();
	sb.AppendLine("=== Staged Changes ===");
	sb.AppendLine(stagedFiles.Any() ? string.Join(Environment.NewLine, stagedFiles) : "No staged changes.");
	sb.AppendLine("\n=== Staged Modified Files ===");
	sb.AppendLine(!string.IsNullOrWhiteSpace(stagedDiffOutput) ? stagedDiffOutput : "No staged modifications.");
	sb.AppendLine("\n=== Unstaged Changes ===");
	sb.AppendLine(unstagedFiles.Any() ? string.Join(Environment.NewLine, unstagedFiles) : "No unstaged changes.");
	sb.AppendLine("\n=== Unstaged Modified Files ===");
	sb.AppendLine(!string.IsNullOrWhiteSpace(unstagedDiffOutput) ? unstagedDiffOutput : "No unstaged modifications.");
	return sb.ToString();
}
void DisplayLogPreview(string content) {
	// Display a preview of the log content in a styled table
	Table table = new Table().AddColumn(new TableColumn("[cyan]Log Preview[/]").Centered()).
							  AddRow(new Text(content.Length > 500 ? content[..500] + "..." : content)).
							  Border(TableBorder.Rounded).
							  BorderStyle(new Style(foreground: new Color(red: 0, green: 255, blue: 255)));
	AnsiConsole.Write(table);
}
async Task<bool> ConfirmSaveAsync(string filePath) {
	// Ask user to confirm saving the log file
	bool save = AnsiConsole.Confirm("[yellow]Would you like to save the log file?[/]");
	if (save) {
		await File.WriteAllTextAsync(filePath,
									 await BuildLogContentAsync(await ExecuteGitCommandAsync("status --porcelain"),
																await
																	ExecuteGitCommandAsync("diff --cached --name-status"),
																await ExecuteGitCommandAsync("diff --name-status")));
		Log.Information("Git log saved to {OutputFile}", filePath);
	} else {
		Log.Information("User chose not to save the log file");
		AnsiConsole.MarkupLine("[yellow]⚠ Log file was not saved.[/]");
	}
	return save;
}
string ShowMainMenu() {
	// Modern menu with vibrant styling
	Log.Information("Displaying main menu");
	return AnsiConsole.Prompt(new SelectionPrompt<string>().Title("\n[cyan bold]What would you like to do next?[/]").
															HighlightStyle(new Style(new Color(red: 0,
																							   green: 255,
																							   blue: 255),
																					 decoration: Decoration.Bold)).
															AddChoices("📂 Open the generated log file",
																	   "📁 Select another Git repository",
																	   "❌ Exit the application"));
}
async Task<bool> HandleMenuChoiceAsync(string choice, string filePath) {
	if (choice.StartsWith("📂")) {
		try {
			Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
			Log.Information("Opened log file: {FilePath}", filePath);
		} catch (Exception ex) {
			Log.Warning(ex, "Failed to open log file: {FilePath}", filePath);
			AnsiConsole.MarkupLine("[yellow]⚠ Unable to open the file. Please check the path or permissions.[/]");
		}
	} else if (choice.StartsWith("❌")) {
		AnsiConsole.MarkupLine("[cyan]👋 Thank you for using Git Log Pro! See you soon![/]");
		return false;
	}
	return true;
}
void DisplaySuccessMessage(string filePath) {
	// Polished success message with a professional touch
	AnsiConsole.MarkupLine("\n[green bold]✔ Git log generated and saved successfully![/]");
	AnsiConsole.MarkupLine($"[blue]Saved to:[/] [underline]{filePath}[/]");
}
string SanitizeFileName(string input) {
	// Simplified file name sanitization using LINQ
	string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()) + "&");
	string sanitized    = Regex.Replace(input, $"[{invalidChars}]", "_").Replace(" ", "-").Trim();
	return string.IsNullOrWhiteSpace(sanitized) ? "NoCommitMessage" : sanitized;
}