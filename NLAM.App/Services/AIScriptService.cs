using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NLAM.App.Services;

/// <summary>
/// AI Script Generation Service using Ollama API
/// </summary>
public class AIScriptService : IDisposable
{
    private readonly HttpClient _httpClient;
    private string _ollamaUrl = "http://localhost:11434";
    private string _selectedModel = "";
    private List<string> _availableModels = new();
    private bool _isConnected = false;

    public bool IsModelLoaded => _isConnected && !string.IsNullOrEmpty(_selectedModel);
    public bool IsLoading => false;
    public bool IsDownloading => false;
    public string SelectedModel => _selectedModel;
    public List<string> AvailableModels => _availableModels;
    public string OllamaUrl
    {
        get => _ollamaUrl;
        set => _ollamaUrl = value?.TrimEnd('/') ?? "http://localhost:11434";
    }

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<List<string>>? ModelsLoaded;

    public AIScriptService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    private void OnStatusChanged(string status)
    {
        StatusChanged?.Invoke(this, status);
    }

    public bool IsModelAvailable() => _isConnected;

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            OnStatusChanged("Connecting to Ollama...");
            
            var response = await _httpClient.GetAsync($"{_ollamaUrl}/api/tags", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                OnStatusChanged("Failed to connect to Ollama");
                return false;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json);
            
            _availableModels = tagsResponse?.Models?.Select(m => m.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new();
            _isConnected = true;
            
            ModelsLoaded?.Invoke(this, _availableModels);
            OnStatusChanged($"Connected! {_availableModels.Count} models available");
            
            return true;
        }
        catch (HttpRequestException)
        {
            OnStatusChanged("Cannot connect to Ollama. Is it running?");
            _isConnected = false;
            return false;
        }
        catch (Exception ex)
        {
            OnStatusChanged($"Connection error: {ex.Message}");
            _isConnected = false;
            return false;
        }
    }

    public void SelectModel(string modelName)
    {
        _selectedModel = modelName;
        OnStatusChanged($"Selected model: {modelName}");
    }

    // Compatibility methods for existing UI
    public async Task<bool> DownloadModelAsync(Action<string>? onLog = null, CancellationToken cancellationToken = default)
    {
        onLog?.Invoke("=".PadRight(60, '='));
        onLog?.Invoke("  NLAM - Ollama Connection");
        onLog?.Invoke("=".PadRight(60, '='));
        onLog?.Invoke("");
        onLog?.Invoke("[INFO] Connecting to Ollama API...");
        onLog?.Invoke($"[INFO] URL: {_ollamaUrl}");
        onLog?.Invoke("");
        onLog?.Invoke("[TIP] To download models, use terminal:");
        onLog?.Invoke("      ollama pull llama3.2");
        onLog?.Invoke("      ollama pull codellama");
        onLog?.Invoke("      ollama pull qwen2.5-coder");
        onLog?.Invoke("      ollama pull deepseek-coder");
        onLog?.Invoke("");
        
        var result = await ConnectAsync(cancellationToken);
        
        if (result)
        {
            onLog?.Invoke("[SUCCESS] Connected to Ollama!");
            onLog?.Invoke($"[INFO] Available models: {_availableModels.Count}");
            foreach (var model in _availableModels)
            {
                onLog?.Invoke($"       - {model}");
            }
        }
        else
        {
            onLog?.Invoke("[ERROR] Failed to connect to Ollama");
            onLog?.Invoke("[TIP] Make sure Ollama is running: ollama serve");
        }
        
        return result;
    }

    public async Task<bool> LoadModelAsync(CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(cancellationToken);
    }

    public async Task<string> GenerateScriptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || string.IsNullOrEmpty(_selectedModel))
        {
            return GenerateScriptFromTemplate(prompt);
        }

        try
        {
            OnStatusChanged($"Generating with {_selectedModel}...");

            var systemPrompt = GetAhkSystemPrompt();
            var fullPrompt = $"{systemPrompt}\n\nTask: {prompt}\n\nGenerate the AutoHotkey v2 code:";

            var request = new OllamaGenerateRequest
            {
                Model = _selectedModel,
                Prompt = fullPrompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.3,
                    NumPredict = 1024
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_ollamaUrl}/api/generate", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                OnStatusChanged($"API error: {response.StatusCode}");
                return GenerateScriptFromTemplate(prompt);
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var generateResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson);
            
            var generated = CleanupGeneratedScript(generateResponse?.Response ?? "");
            
            OnStatusChanged("Script generated successfully!");
            return generated;
        }
        catch (TaskCanceledException)
        {
            OnStatusChanged("Generation cancelled");
            return GenerateScriptFromTemplate(prompt);
        }
        catch (Exception ex)
        {
            OnStatusChanged($"Generation error: {ex.Message}");
            return GenerateScriptFromTemplate(prompt);
        }
    }

    /// <summary>
    /// Reviews and fixes errors in an AHK script using AI
    /// </summary>
    public async Task<CodeFixResult> FixCodeAsync(string scriptContent, Action<string>? onLog = null, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || string.IsNullOrEmpty(_selectedModel))
        {
            return new CodeFixResult
            {
                Success = false,
                OriginalScript = scriptContent,
                FixedScript = scriptContent,
                Issues = new List<CodeIssue> { new CodeIssue { Description = "AI not connected. Please connect to Ollama first.", Severity = "Error" } }
            };
        }

        try
        {
            onLog?.Invoke($"Analyzing code with {_selectedModel}...");
            OnStatusChanged($"Analyzing code with {_selectedModel}...");

            var systemPrompt = @"You are an AutoHotkey v2 code reviewer and fixer. Analyze the given AHK v2 script for errors and issues.

OUTPUT FORMAT (JSON only, no markdown):
{
  ""hasErrors"": true/false,
  ""issues"": [
    {
      ""line"": 5,
      ""severity"": ""Error"" or ""Warning"" or ""Info"",
      ""description"": ""Brief description of the issue"",
      ""suggestion"": ""How to fix it""
    }
  ],
  ""fixedScript"": ""The corrected script code""
}

COMMON AHK v2 ERRORS TO CHECK:
1. Using = instead of := for assignment
2. Using FileExists() instead of FileExist()
3. Missing #Requires AutoHotkey v2.0
4. Using %var% syntax instead of direct variable reference
5. Using single quotes 'text' instead of double quotes ""text""
6. Incorrect function syntax (v1 vs v2)
7. Missing braces {} for multi-line blocks
8. Incorrect hotkey syntax
9. Using deprecated commands
10. Type mismatches and incorrect parameter counts

If there are NO errors, set hasErrors to false and return the original script unchanged.
If there ARE errors, fix them and explain each issue found.

Output ONLY valid JSON, no explanations outside the JSON.";

            onLog?.Invoke("Preparing prompt for AI...");
            var fullPrompt = $"{systemPrompt}\n\nAHK Script to review:\n```\n{scriptContent}\n```\n\nJSON output:";

            onLog?.Invoke($"Sending request to Ollama ({scriptContent.Length} chars)...");
            var request = new OllamaGenerateRequest
            {
                Model = _selectedModel,
                Prompt = fullPrompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.1,
                    NumPredict = 4096
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            onLog?.Invoke("Waiting for AI response...");
            var response = await _httpClient.PostAsync($"{_ollamaUrl}/api/generate", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                onLog?.Invoke($"❌ API error: {response.StatusCode}");
                OnStatusChanged($"API error: {response.StatusCode}");
                return new CodeFixResult
                {
                    Success = false,
                    OriginalScript = scriptContent,
                    FixedScript = scriptContent,
                    Issues = new List<CodeIssue> { new CodeIssue { Description = $"API error: {response.StatusCode}", Severity = "Error" } }
                };
            }

            onLog?.Invoke("Response received. Parsing results...");
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var generateResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson);
            var aiResponse = generateResponse?.Response ?? "";

            // Parse the AI response
            var result = ParseCodeFixResponse(aiResponse, scriptContent);
            
            if (result.Issues.Count == 0)
            {
                onLog?.Invoke("✅ No issues found in the script!");
                OnStatusChanged("No issues found in the script!");
            }
            else
            {
                onLog?.Invoke($"🔧 Found {result.Issues.Count} issue(s).");
                foreach (var issue in result.Issues)
                {
                    onLog?.Invoke($"   • [{issue.Severity}] {issue.Description}");
                }
                OnStatusChanged($"Found {result.Issues.Count} issue(s). Script fixed.");
            }

            return result;
        }
        catch (TaskCanceledException)
        {
            onLog?.Invoke("⚠️ Code review cancelled by user");
            OnStatusChanged("Code review cancelled");
            return new CodeFixResult
            {
                Success = false,
                OriginalScript = scriptContent,
                FixedScript = scriptContent,
                Issues = new List<CodeIssue> { new CodeIssue { Description = "Operation cancelled", Severity = "Info" } }
            };
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"❌ Error: {ex.Message}");
            OnStatusChanged($"Code review error: {ex.Message}");
            return new CodeFixResult
            {
                Success = false,
                OriginalScript = scriptContent,
                FixedScript = scriptContent,
                Issues = new List<CodeIssue> { new CodeIssue { Description = $"Error: {ex.Message}", Severity = "Error" } }
            };
        }
    }

    private CodeFixResult ParseCodeFixResponse(string aiResponse, string originalScript)
    {
        var result = new CodeFixResult
        {
            Success = true,
            OriginalScript = originalScript,
            FixedScript = originalScript,
            Issues = new List<CodeIssue>()
        };

        try
        {
            // Clean up the response - remove markdown code blocks
            var cleaned = aiResponse.Trim();
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```[\w]*\n?", "");
            cleaned = cleaned.Replace("```", "").Trim();

            // Find JSON object
            var jsonStart = cleaned.IndexOf('{');
            var jsonEnd = cleaned.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                cleaned = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            var parsed = JsonSerializer.Deserialize<AiCodeFixResponse>(cleaned);

            if (parsed != null)
            {
                result.HasErrors = parsed.HasErrors;

                if (parsed.Issues != null)
                {
                    foreach (var issue in parsed.Issues)
                    {
                        result.Issues.Add(new CodeIssue
                        {
                            Line = issue.Line,
                            Severity = issue.Severity ?? "Warning",
                            Description = issue.Description ?? "",
                            Suggestion = issue.Suggestion ?? ""
                        });
                    }
                }

                if (!string.IsNullOrWhiteSpace(parsed.FixedScript))
                {
                    result.FixedScript = CleanupGeneratedScript(parsed.FixedScript);
                }
            }
        }
        catch (JsonException)
        {
            // If JSON parsing fails, try to extract any useful information
            result.Issues.Add(new CodeIssue
            {
                Severity = "Warning",
                Description = "Could not parse AI response. Manual review recommended."
            });
        }

        return result;
    }

    /// <summary>
    /// Parse an AHK script and organize it into logical tracks with appropriate names using AI
    /// </summary>
    public async Task<List<ParsedTrack>> ParseScriptToTracksAsync(string scriptContent, Action<string>? onLog = null, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || string.IsNullOrEmpty(_selectedModel))
        {
            onLog?.Invoke("AI not connected. Using simple parser...");
            // Fallback to simple parsing without AI
            return ParseScriptToTracksSimple(scriptContent);
        }

        try
        {
            onLog?.Invoke($"Analyzing script with {_selectedModel}...");
            OnStatusChanged($"Analyzing script with {_selectedModel}...");

            var systemPrompt = @"You are an AutoHotkey script analyzer. Analyze the given AHK script and organize it into logical groups (tracks).

OUTPUT FORMAT (JSON only, no markdown):
{
  ""tracks"": [
    {
      ""name"": ""Short descriptive name (2-4 words)"",
      ""clips"": [
        {
          ""name"": ""Action name (2-4 words)"",
          ""script"": ""The actual AHK code for this action"",
          ""duration"": 1.0
        }
      ]
    }
  ]
}

RULES:
1. Group related actions into the same track (e.g., all mouse actions, all keyboard inputs, all file operations)
2. Each clip should be a logical unit of automation (single action or tightly coupled actions)
3. Track names should describe the category: ""Mouse Actions"", ""Keyboard Input"", ""File Operations"", ""Window Control"", ""System Commands""
4. Clip names should describe the specific action: ""Click Button"", ""Type Username"", ""Open Notepad"", ""Wait 2 Seconds""
5. Estimate duration based on Sleep commands (1000ms = 1.0s) or complexity (default 1.0s for simple actions)
6. Keep #Requires and other directives in the first clip of the first track
7. Preserve the exact script content, don't modify the code
8. If there's only a few simple commands, put them all in one track called ""Main Actions""

Output ONLY valid JSON, no explanations.";

            onLog?.Invoke("Preparing prompt for AI...");
            onLog?.Invoke($"Script size: {scriptContent.Length} characters");
            var fullPrompt = $"{systemPrompt}\n\nAHK Script to analyze:\n```\n{scriptContent}\n```\n\nJSON output:";

            onLog?.Invoke("Sending request to Ollama...");
            var request = new OllamaGenerateRequest
            {
                Model = _selectedModel,
                Prompt = fullPrompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.2,
                    NumPredict = 2048
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            onLog?.Invoke("Waiting for AI response...");
            var response = await _httpClient.PostAsync($"{_ollamaUrl}/api/generate", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                onLog?.Invoke($"❌ API error: {response.StatusCode}. Using simple parser...");
                OnStatusChanged($"API error: {response.StatusCode}");
                return ParseScriptToTracksSimple(scriptContent);
            }

            onLog?.Invoke("Response received. Parsing track structure...");
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var generateResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson);
            var aiResponse = generateResponse?.Response ?? "";

            // Extract JSON from response
            var tracks = ParseAiTrackResponse(aiResponse);
            
            if (tracks.Count == 0)
            {
                onLog?.Invoke("⚠️ AI parsing failed, using simple parser...");
                OnStatusChanged("AI parsing failed, using simple parser");
                return ParseScriptToTracksSimple(scriptContent);
            }

            onLog?.Invoke($"✅ Script analyzed: {tracks.Count} tracks, {tracks.Sum(t => t.Clips.Count)} clips");
            foreach (var track in tracks)
            {
                onLog?.Invoke($"   • {track.Name}: {track.Clips.Count} clips");
            }
            OnStatusChanged($"Script analyzed: {tracks.Count} tracks, {tracks.Sum(t => t.Clips.Count)} clips");
            return tracks;
        }
        catch (TaskCanceledException)
        {
            onLog?.Invoke("⚠️ Analysis cancelled by user");
            OnStatusChanged("Analysis cancelled");
            return new List<ParsedTrack>();
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"❌ Analysis error: {ex.Message}. Using simple parser...");
            OnStatusChanged($"Analysis error: {ex.Message}");
            return ParseScriptToTracksSimple(scriptContent);
        }
    }

    private List<ParsedTrack> ParseAiTrackResponse(string aiResponse)
    {
        var result = new List<ParsedTrack>();
        
        try
        {
            // Clean up the response - remove markdown code blocks
            var cleaned = aiResponse.Trim();
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"```[\w]*\n?", "");
            cleaned = cleaned.Replace("```", "").Trim();
            
            // Find JSON object
            var jsonStart = cleaned.IndexOf('{');
            var jsonEnd = cleaned.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                cleaned = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            var parsed = JsonSerializer.Deserialize<AiTrackParseResponse>(cleaned);
            
            if (parsed?.Tracks != null)
            {
                foreach (var track in parsed.Tracks)
                {
                    var parsedTrack = new ParsedTrack
                    {
                        Name = track.Name ?? "Unnamed Track"
                    };
                    
                    if (track.Clips != null)
                    {
                        foreach (var clip in track.Clips)
                        {
                            parsedTrack.Clips.Add(new ParsedClip
                            {
                                Name = clip.Name ?? "Unnamed Clip",
                                Script = clip.Script ?? "",
                                Duration = clip.Duration > 0 ? clip.Duration : 1.0
                            });
                        }
                    }
                    
                    if (parsedTrack.Clips.Count > 0)
                    {
                        result.Add(parsedTrack);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // JSON parsing failed
        }

        return result;
    }

    private List<ParsedTrack> ParseScriptToTracksSimple(string scriptContent)
    {
        var result = new List<ParsedTrack>();
        var lines = scriptContent.Split('\n');
        
        var currentTrack = new ParsedTrack { Name = "Main Actions" };
        var currentClipLines = new List<string>();
        int clipIndex = 1;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            // Skip empty lines and directives for clip separation
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (currentClipLines.Count > 0)
                {
                    currentTrack.Clips.Add(new ParsedClip
                    {
                        Name = GetClipNameFromCode(currentClipLines, clipIndex),
                        Script = string.Join("\n", currentClipLines),
                        Duration = EstimateDuration(currentClipLines)
                    });
                    currentClipLines.Clear();
                    clipIndex++;
                }
                continue;
            }

            // Handle directives - keep them with the first clip
            if (trimmed.StartsWith("#"))
            {
                currentClipLines.Add(line);
                continue;
            }

            // Handle comments that might indicate sections
            if (trimmed.StartsWith(";"))
            {
                // Check if it's a section header
                if (trimmed.Contains("===") || trimmed.Contains("---") || 
                    (trimmed.Length > 3 && trimmed.Substring(1).Trim().All(c => char.IsLetter(c) || c == ' ')))
                {
                    // Save current clip if any
                    if (currentClipLines.Count > 0)
                    {
                        currentTrack.Clips.Add(new ParsedClip
                        {
                            Name = GetClipNameFromCode(currentClipLines, clipIndex),
                            Script = string.Join("\n", currentClipLines),
                            Duration = EstimateDuration(currentClipLines)
                        });
                        currentClipLines.Clear();
                        clipIndex++;
                    }
                }
                currentClipLines.Add(line);
                continue;
            }

            currentClipLines.Add(line);
        }

        // Add remaining lines as final clip
        if (currentClipLines.Count > 0)
        {
            currentTrack.Clips.Add(new ParsedClip
            {
                Name = GetClipNameFromCode(currentClipLines, clipIndex),
                Script = string.Join("\n", currentClipLines),
                Duration = EstimateDuration(currentClipLines)
            });
        }

        if (currentTrack.Clips.Count > 0)
        {
            result.Add(currentTrack);
        }

        // If no clips were created, create one with all content
        if (result.Count == 0)
        {
            result.Add(new ParsedTrack
            {
                Name = "Imported Script",
                Clips = new List<ParsedClip>
                {
                    new ParsedClip
                    {
                        Name = "Script Content",
                        Script = scriptContent,
                        Duration = 2.0
                    }
                }
            });
        }

        return result;
    }

    private string GetClipNameFromCode(List<string> lines, int index)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Check for descriptive comment
            if (trimmed.StartsWith(";") && trimmed.Length > 2)
            {
                var comment = trimmed.Substring(1).Trim();
                if (comment.Length > 2 && comment.Length < 40 && !comment.Contains("==="))
                {
                    return comment;
                }
            }

            // Check for known commands
            if (trimmed.StartsWith("Click", StringComparison.OrdinalIgnoreCase))
                return "Mouse Click";
            if (trimmed.StartsWith("Send ", StringComparison.OrdinalIgnoreCase) || 
                trimmed.StartsWith("SendText", StringComparison.OrdinalIgnoreCase))
                return "Send Keys";
            if (trimmed.StartsWith("Sleep", StringComparison.OrdinalIgnoreCase))
                return "Wait/Delay";
            if (trimmed.StartsWith("Run ", StringComparison.OrdinalIgnoreCase))
                return "Run Program";
            if (trimmed.StartsWith("WinActivate", StringComparison.OrdinalIgnoreCase))
                return "Activate Window";
            if (trimmed.StartsWith("WinWait", StringComparison.OrdinalIgnoreCase))
                return "Wait Window";
            if (trimmed.StartsWith("MsgBox", StringComparison.OrdinalIgnoreCase))
                return "Message Box";
            if (trimmed.StartsWith("MouseMove", StringComparison.OrdinalIgnoreCase))
                return "Move Mouse";
            if (trimmed.StartsWith("FileRead", StringComparison.OrdinalIgnoreCase) || 
                trimmed.StartsWith("FileAppend", StringComparison.OrdinalIgnoreCase))
                return "File Operation";
            if (trimmed.StartsWith("Loop", StringComparison.OrdinalIgnoreCase))
                return "Loop";
            if (trimmed.Contains("::"))
                return "Hotkey";
        }

        return $"Action {index}";
    }

    private double EstimateDuration(List<string> lines)
    {
        double totalMs = 0;
        int actionCount = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim().ToLowerInvariant();
            
            // Parse Sleep commands
            if (trimmed.StartsWith("sleep"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"sleep\s+(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int ms))
                {
                    totalMs += ms;
                }
            }
            // Count actions for minimum duration
            else if (!trimmed.StartsWith(";") && !trimmed.StartsWith("#") && !string.IsNullOrWhiteSpace(trimmed))
            {
                actionCount++;
            }
        }

        // Convert to seconds, minimum 0.5s per action, max from sleep
        double durationFromSleep = totalMs / 1000.0;
        double durationFromActions = actionCount * 0.3; // 300ms per action estimate

        return Math.Max(0.5, Math.Max(durationFromSleep, durationFromActions));
    }

    private string GetAhkSystemPrompt()
    {
        return @"You are an AutoHotkey v2 code generator. Generate ONLY valid, working AHK v2 code.

CRITICAL RULES:
- Output ONLY code, NO markdown, NO ```, NO explanations, NO comments about what the code does
- Use ONLY real AHK v2 functions
- Always start with: #Requires AutoHotkey v2.0
- Use := for assignment (NOT =)
- Strings use double quotes ""text""
- Use A_ built-in variables for paths

BUILT-IN VARIABLES:
A_Desktop, A_MyDocuments, A_AppData, A_Temp, A_ScriptDir, A_WorkingDir
A_ScreenWidth, A_ScreenHeight, A_Clipboard, A_Now, A_YYYY, A_MM, A_DD
A_Hour, A_Min, A_Sec, A_LoopFilePath, A_LoopFileName, A_Index

KEY SYNTAX:
- Hotkey: ^j:: { MsgBox ""text"" }
- Loop: Loop 10 { }
- Loop Files: Loop Files ""path\*.*"" { A_LoopFilePath }
- If: if (condition) { }
- Function: MyFunc() { return value }
- Variables: myVar := ""value""
- Send keys: Send ""{Enter}"", Send ""^c""
- Send text: SendText ""literal text""
- Click: Click, Click 100, 200, Click ""Right""
- Mouse: MouseMove x, y / MouseGetPos &x, &y
- Message: MsgBox ""text"", ""title""
- Run: Run ""program.exe"" / Run ""https://url""
- Window: WinActivate ""title"" / WinWait ""title"" / WinExist(""title"")
- File: FileRead(path) / FileAppend text, path / FileExist(path) / FileDelete path
- Sleep: Sleep 1000
- Clipboard: A_Clipboard := ""text"" / text := A_Clipboard
- Timer: SetTimer MyFunc, 1000
- Hotstring: ::btw::by the way

EXAMPLES:
#Requires AutoHotkey v2.0
^j:: {
    MsgBox ""Hello""
}

#Requires AutoHotkey v2.0
SendText ""Hello World""
Send ""{Enter}""

#Requires AutoHotkey v2.0
Loop Files A_Desktop ""\*.txt"" {
    MsgBox A_LoopFileName
}

#Requires AutoHotkey v2.0
Run ""notepad.exe""
WinWait ""Notepad""
WinActivate
SendText ""Hello""

NEVER USE (INVALID):
- FileExists() -> use FileExist()
- = for assignment -> use :=
- %var% syntax -> use var directly
- Single quotes 'text' -> use double quotes ""text""

Output ONLY the raw code. Start with #Requires AutoHotkey v2.0";
    }

    private string CleanupGeneratedScript(string script)
    {
        var result = script.Trim();
        
        // Remove markdown code blocks
        result = System.Text.RegularExpressions.Regex.Replace(result, @"```[\w]*\n?", "");
        result = result.Replace("```", "");
        
        // Remove common AI prefixes
        result = System.Text.RegularExpressions.Regex.Replace(result, @"^(Here'?s?|This is|The following|Below is)[^\n]*\n", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Fix common AI mistakes
        result = result.Replace("FileExists(", "FileExist(");
        
        // Remove excessive blank lines
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\n{3,}", "\n\n");
        
        // Ensure it starts with #Requires
        if (!result.TrimStart().StartsWith("#Requires"))
        {
            result = "#Requires AutoHotkey v2.0\n" + result;
        }
        
        // Remove duplicate #Requires lines
        var lines = result.Split('\n').ToList();
        var seenRequires = false;
        var cleanedLines = new List<string>();
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("#Requires"))
            {
                if (!seenRequires)
                {
                    cleanedLines.Add(line);
                    seenRequires = true;
                }
            }
            else
            {
                cleanedLines.Add(line);
            }
        }
        
        return string.Join("\n", cleanedLines).Trim();
    }

    private string GenerateScriptFromTemplate(string prompt)
    {
        OnStatusChanged("Using template-based generation...");
        
        var sb = new StringBuilder();
        sb.AppendLine("#Requires AutoHotkey v2.0");
        sb.AppendLine("; Generated script based on: " + prompt);
        sb.AppendLine();

        var lowerPrompt = prompt.ToLowerInvariant();

        if (lowerPrompt.Contains("click") || lowerPrompt.Contains("mouse"))
        {
            sb.AppendLine("; Mouse click automation");
            sb.AppendLine("Click");
            sb.AppendLine("Sleep 100");
        }
        else if (lowerPrompt.Contains("type") || lowerPrompt.Contains("text") || lowerPrompt.Contains("write"))
        {
            sb.AppendLine("; Text input automation");
            sb.AppendLine("SendText \"Hello World\"");
            sb.AppendLine("Sleep 100");
        }
        else if (lowerPrompt.Contains("key") || lowerPrompt.Contains("press") || lowerPrompt.Contains("enter"))
        {
            sb.AppendLine("; Keyboard automation");
            sb.AppendLine("Send \"{Enter}\"");
            sb.AppendLine("Sleep 100");
        }
        else if (lowerPrompt.Contains("wait") || lowerPrompt.Contains("delay") || lowerPrompt.Contains("sleep"))
        {
            sb.AppendLine("; Wait/Delay");
            sb.AppendLine("Sleep 1000");
        }
        else if (lowerPrompt.Contains("hotkey") || lowerPrompt.Contains("shortcut"))
        {
            sb.AppendLine("; Hotkey definition");
            sb.AppendLine("^j:: {");
            sb.AppendLine("    MsgBox \"Hotkey pressed!\"");
            sb.AppendLine("}");
        }
        else if (lowerPrompt.Contains("message") || lowerPrompt.Contains("msgbox") || lowerPrompt.Contains("alert"))
        {
            sb.AppendLine("; Message box");
            sb.AppendLine("MsgBox \"Hello from AutoHotkey!\"");
        }
        else if (lowerPrompt.Contains("loop") || lowerPrompt.Contains("repeat"))
        {
            sb.AppendLine("; Loop automation");
            sb.AppendLine("Loop 5 {");
            sb.AppendLine("    ; Add your actions here");
            sb.AppendLine("    Sleep 500");
            sb.AppendLine("}");
        }
        else
        {
            sb.AppendLine("; Basic automation template");
            sb.AppendLine("MsgBox \"Script started!\"");
            sb.AppendLine("Sleep 1000");
            sb.AppendLine("; Add your automation here");
            sb.AppendLine("MsgBox \"Script completed!\"");
        }

        return sb.ToString();
    }

    public List<string> GetSuggestions()
    {
        return new List<string>
        {
            "Click mouse at current position",
            "Type 'Hello World' text",
            "Press Enter key",
            "Wait for 1 second",
            "Create Ctrl+J hotkey",
            "Show message box",
            "Loop 5 times with delay",
            "Open Notepad and type text",
            "Copy and paste clipboard",
            "Move mouse to coordinates"
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

// Parsed track/clip models for import
public class ParsedTrack
{
    public string Name { get; set; } = "";
    public List<ParsedClip> Clips { get; set; } = new();
}

public class ParsedClip
{
    public string Name { get; set; } = "";
    public string Script { get; set; } = "";
    public double Duration { get; set; } = 1.0;
}

// AI response models for script parsing
public class AiTrackParseResponse
{
    [JsonPropertyName("tracks")]
    public List<AiTrackData>? Tracks { get; set; }
}

public class AiTrackData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("clips")]
    public List<AiClipData>? Clips { get; set; }
}

public class AiClipData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("script")]
    public string? Script { get; set; }
    
    [JsonPropertyName("duration")]
    public double Duration { get; set; } = 1.0;
}

// Ollama API Models
public class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModel>? Models { get; set; }
}

public class OllamaModel
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
    
    [JsonPropertyName("modified_at")]
    public string? ModifiedAt { get; set; }
}

public class OllamaGenerateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";
    
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";
    
    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;
    
    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

public class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;
    
    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; } = 512;
}

public class OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; set; }
    
    [JsonPropertyName("done")]
    public bool Done { get; set; }
    
    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
}

// Code Fixer Models
public class CodeFixResult
{
    public bool Success { get; set; }
    public bool HasErrors { get; set; }
    public string OriginalScript { get; set; } = "";
    public string FixedScript { get; set; } = "";
    public List<CodeIssue> Issues { get; set; } = new();
}

public class CodeIssue
{
    public int Line { get; set; }
    public string Severity { get; set; } = "Warning";
    public string Description { get; set; } = "";
    public string Suggestion { get; set; } = "";
}

public class AiCodeFixResponse
{
    [JsonPropertyName("hasErrors")]
    public bool HasErrors { get; set; }
    
    [JsonPropertyName("issues")]
    public List<AiCodeIssue>? Issues { get; set; }
    
    [JsonPropertyName("fixedScript")]
    public string? FixedScript { get; set; }
}

public class AiCodeIssue
{
    [JsonPropertyName("line")]
    public int Line { get; set; }
    
    [JsonPropertyName("severity")]
    public string? Severity { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }
}
