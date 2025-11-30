# NLAM - Non-Linear Automation Maker

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Windows](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)
![WPF](https://img.shields.io/badge/UI-WPF-512BD4)
![License](https://img.shields.io/badge/License-MIT-green)

**NLAM** is a modern, visual automation tool for creating AutoHotkey v2 scripts using a timeline-based interface similar to video editing software. Design your automation workflows visually with tracks and clips, then export to executable AHK scripts.

## ✨ Features

### 🎬 Timeline-Based Editing
- **Multi-track timeline** - Organize automation actions in parallel tracks
- **Drag & drop clips** - Easily arrange and reorder automation steps
- **Visual timing** - See and adjust the timing of each action
- **Zoom & scroll** - Navigate complex automation sequences

### 🤖 AI-Powered Script Generation
- **Ollama integration** - Connect to local LLM for intelligent script generation
- **Natural language input** - Describe what you want in plain text
- **Code review & fix** - AI analyzes and fixes AHK script errors
- **Smart script parsing** - Import existing scripts and organize into tracks

### 📝 AutoHotkey v2 Support
- **Modern AHK v2 syntax** - Generate clean, idiomatic code
- **Real-time preview** - See generated script as you build
- **Syntax validation** - Catch errors before export
- **Direct export** - Save as executable .ahk files

### 🎨 Modern UI
- **Dark theme** - Easy on the eyes for extended use
- **Intuitive controls** - Familiar video-editor-like interface
- **Responsive design** - Smooth interactions and animations

## 📸 Screenshots

*Coming soon*

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- [AutoHotkey v2](https://www.autohotkey.com/) (for running exported scripts)
- [Ollama](https://ollama.ai/) (optional, for AI features)

### Installation

#### Option 1: Download Release
1. Download the latest release from [Releases](../../releases)
2. Extract and run `NLAM.App.exe`

#### Option 2: Build from Source
```bash
# Clone the repository
git clone https://github.com/yourusername/NLAM.git
cd NLAM

# Build the project
dotnet build -c Release

# Run the application
dotnet run --project NLAM.App
```

### Publish as Portable EXE
```bash
dotnet publish NLAM.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

## 🎮 Usage

### Basic Workflow
1. **Create Tracks** - Add tracks to organize different types of actions
2. **Add Clips** - Add automation clips to each track
3. **Configure Actions** - Set up mouse clicks, keyboard input, delays, etc.
4. **Preview** - Use playback controls to preview timing
5. **Export** - Save as .ahk file and run with AutoHotkey

### AI Features (requires Ollama)
1. Start Ollama: `ollama serve`
2. Pull a model: `ollama pull llama3.2` or `ollama pull codellama`
3. In NLAM, go to Preferences and connect to Ollama
4. Use AI to generate scripts from natural language descriptions

### Supported Actions
- 🖱️ **Mouse** - Click, move, drag
- ⌨️ **Keyboard** - Send keys, type text
- ⏱️ **Timing** - Sleep, wait for window
- 🪟 **Window** - Activate, move, resize
- 📁 **File** - Read, write, operations
- 🔄 **Flow** - Loops, conditions
- 💬 **UI** - Message boxes, input dialogs

## 🏗️ Project Structure

```
NLAM/
├── NLAM.App/                 # Main WPF application
│   ├── Converters/          # XAML value converters
│   ├── Models/              # Data models
│   ├── Services/            # Business logic services
│   │   ├── AIScriptService.cs    # Ollama AI integration
│   │   ├── FileService.cs        # File operations
│   │   └── PlaybackService.cs    # Timeline playback
│   ├── Themes/              # UI themes and styles
│   ├── ViewModels/          # MVVM ViewModels
│   │   ├── MainViewModel.cs
│   │   ├── TimelineViewModel.cs
│   │   ├── TrackViewModel.cs
│   │   └── ClipViewModel.cs
│   └── Views/               # XAML Views
│       ├── MainWindow.xaml
│       ├── TimelineView.xaml
│       └── PreferencesWindow.xaml
└── NLAM.sln                 # Solution file
```

## 🛠️ Technology Stack

- **.NET 8** - Modern .NET runtime
- **WPF** - Windows Presentation Foundation for UI
- **CommunityToolkit.Mvvm** - MVVM framework
- **Microsoft.Extensions.DependencyInjection** - DI container
- **Ollama API** - Local LLM integration

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [AutoHotkey](https://www.autohotkey.com/) - The powerful automation scripting language
- [Ollama](https://ollama.ai/) - Run LLMs locally
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM toolkit

## 📞 Contact

Project Link: [https://github.com/yourusername/NLAM](https://github.com/yourusername/NLAM)

---

Made with ❤️ for the automation community
