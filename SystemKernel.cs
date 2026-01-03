// Kernel.cs — полностью рабочая версия с Roslyn-компиляцией .cs файлов

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.ComponentModel;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ISTerminal
{
    public class SystemKernel
    {
        private bool _isRunning = true;
        private bool _isRoot = false;

        // VFS
        private DirectoryNode _root;
        private DirectoryNode _currentDir;

        // HOST FS
        private bool _hostMode = false;
        private string _hostPath = "";

        public SystemKernel() => InitVfs();

        public void Run()
        {
           
              Console.Clear();
                Console.WriteLine("IsTerminal Kernel loaded.");
            Console.WriteLine("Type 'help' for commands.\n");

            while (_isRunning)
            {
                Console.ForegroundColor = _isRoot ? ConsoleColor.Red : ConsoleColor.Green;

                string location = _hostMode ? _hostPath : _currentDir.Path;
                Console.Write($"{(_isRoot ? "root" : "user")}:{location} $ ");

                Console.ForegroundColor = ConsoleColor.White;

                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                Execute(input.Trim());
            }
        }

        // ================= VFS INIT =================

        private void InitVfs()
        {
            _root = new DirectoryNode("/", null, true);

            var system = new DirectoryNode("system", _root, true);
            var kernel = new DirectoryNode("kernel", _root, true);
            var home = new DirectoryNode("home", _root, false);
            var user = new DirectoryNode("user", home, false);

            _root.Add(system);
            _root.Add(kernel);
            _root.Add(home);
            home.Add(user);

            _currentDir = user;
        }

        // ================= COMMAND EXEC =================

        private void Execute(string input)
        {
            var parts = SplitArgs(input);
            if (parts.Length == 0) return;

            var cmd = parts[0];
            var args = parts.Skip(1).ToArray();

            switch (cmd)
            {
                case "help": Help(); break;
                case "about": About(); break;
                case "ls": Ls(); break;
                case "cd": Cd(args); break;
                case "create": Create(args); break;
                case "nano": Open(args); break;
                case "clear": Console.Clear(); break;
                case "exit": _isRunning = false; break;
                case "sudo": Sudo(args); break;
                case "fastfetch": Fastfetch(); break;
                case "su": Su(args); break;
                case "start": Start(args); break;
                case "mount": Mount(args); break;     // Обычный mount внутри системы
                case "unmount": Unmount(args); break; // Ломаем систему
                case "bootloader": Bootloader(args); break; // Загрузка ядра
                default: Error($"command not found: {cmd}"); break;
            }
        }

        private string[] SplitArgs(string input)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = "";

            foreach (char c in input)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (current.Length > 0)
                result.Add(current);

            return result.ToArray();
        }

        // ================= COMMANDS =================

        private void Help()
        {
            Console.WriteLine(@"
about              system info
ls                 list directory
cd <path>          change directory (VFS or HOST)
create <file>      create file (or create ""<path>"" <file>)
nano <file>        nano editor (or nano ""<path>"" <file>)
sudo <command>     run as root
clear              clear screen
exit               shutdown
fastfetch          system info with logo
su - root          switch to root user
start <file>       launch file (or start ""<path>"" <file>) [host mode only]
");
        }

        private void About()
        {
            Console.WriteLine("IsTerminal32 — hybrid terminal OS");
        }

        private void Fastfetch()
        {
            string asciiArt = @"
                   ___           ___     
      ___        /\  \         /\  \    
     /\  \      /::\  \        \:\  \   
     \:\  \    /:/\ \  \        \:\  \  
     /::\__\  _\:\~\ \  \       /::\  \ 
  __/:/\/__/ /\ \:\ \ \__\     /:/\:\__\
 /\/:/  /    \:\ \:\ \/__/    /:/  \/__/
 \::/__/      \:\ \:\__\     /:/  /     
  \:\__\       \:\/:/  /     \/__/      
   \/__/        \::/  /                 
                 \/__/                  
";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(asciiArt);
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine("OS: IsTerminal32");
            Console.WriteLine($"User: {(_isRoot ? "root" : "user")}");
            Console.WriteLine($"Host: {Environment.MachineName}");
            Console.WriteLine($"Kernel: {Environment.OSVersion.VersionString}");
            Console.WriteLine($"Uptime: {Environment.TickCount / 1000 / 60} minutes");
            Console.WriteLine($"Shell: IsTerminal");
        }

        private void Unmount(string[] args)
        {
            if (!_isRoot) { Error("permission denied (are you root?)"); return; }
            if (args.Length == 0) { Error("usage: unmount <path>"); return; }

            string path = args[0];

            // 1. Unmount Boot
            if (path == "system/boot")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("WARNING: Unmounting BOOT partition will cause system instability.");
                Console.Write("Are you sure? (y/n): ");
                if (Console.ReadLine() == "y")
                {
                    SystemState.IsBootMounted = false;
                    Console.WriteLine("Successfully unmounted system/boot.");
                }
                return;
            }

            // 2. Unmount LifeBoot Warning
            if (path == "system/lifeboot/warning")
            {
                bool suppressWarning = args.Length > 1 && args[1] == "-W";
                SystemState.IsLifeBootWarningActive = false;

                if (!suppressWarning)
                    Console.WriteLine("LifeBoot monitoring disabled.");
                else
                    Console.WriteLine("Done."); // Тихий режим как ты просил
                return;
            }

            Error("target is busy or not found");
        }

        // Обновленный метод SU
        private void Su(string[] args)
        {
            // Обычный вход
            if (args.Length >= 2 && args[0] == "-" && args[1] == "root" && args.Length == 2)
            {
                // Проверка целостности для обычного входа
                if (!SystemState.IsLifeBootWarningActive)
                {
                    // Если LifeBoot отключен, обычный вход крашится (симуляция защиты)
                    Error("SECURITY ALERT: LifeBoot integrity check failed. Access denied.");
                    return;
                }
                _isRoot = true;
                Console.WriteLine("Switched to root user.");
                return;
            }

            // Вход для "хакеров" (обход защиты)
            if (args.Length >= 3 && args[0] == "-" && args[1] == "root" && args[2] == "-kernelROOT")
            {
                Console.Write("Password: ");
                // В реальном терминале пароль скрывают, тут пока просто ввод
                string pass = Console.ReadLine();
                if (pass == SystemState.RootPassword || pass == "root") // Пароль дефолтный или 1234
                {
                    _isRoot = true;
                    Console.WriteLine("ROOT ACCESS GRANTED (UNSAFE MODE).");
                }
                else
                {
                    Error("Authentication failure");
                }
                return;
            }

            Error("usage: su - root");
        }

        private void Bootloader(string[] args)
        {
            // bootloader <путь_к_твоему_файлу>
            // Пример: bootloader C:\Download\my_super_kernel.cs

            if (args.Length < 1) { Error("usage: bootloader <source_file_path>"); return; }

            string sourcePath = args[0];

            // Проверяем, существует ли файл, который юзер хочет прошить
            if (!File.Exists(sourcePath))
            {
                Error($"Error: Source kernel file '{sourcePath}' not found.");
                return;
            }

            if (!SystemState.IsBootMounted)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[BOOTLOADER] Flashing kernel from {Path.GetFileName(sourcePath)}...");

                // --- ПРОЦЕСС ПРОШИВКИ ---
                try
                {
                    // 1. Считываем код нового ядра
                    string newKernelCode = File.ReadAllText(sourcePath);

                    // 2. Записываем его как ГЛАВНОЕ системное ядро
                    // Теперь LifeBoot будет грузить именно этот код
                    File.WriteAllText("SystemKernel.cs", newKernelCode);

                    for (int i = 0; i <= 100; i += 20)
                    {
                        Console.Write($"\rWriting blocks: {i}%");
                        Thread.Sleep(300);
                    }
                    Console.WriteLine("\n[SUCCESS] New kernel installed to System/Boot.");
                }
                catch (Exception ex)
                {
                    Error($"Flash failed: {ex.Message}");
                    return;
                }

                // --- СИМУЛЯЦИЯ КРАША ---
                Console.WriteLine("System verify... MISMATCH DETECTED.");
                Thread.Sleep(1000);

                // Ломаем систему, чтобы попасть в Safe Mode
                SystemState.CriticalBootError = true;
                _isRunning = false; // Останавливаем текущее ядро
            }
            else
            {
                Error("Error: Boot partition LOCKED. Please 'sudo unmount system/boot' first.");
            }
        }

        private void Mount(string[] args)
        {
            // Это заглушка, в обычной системе mount не нужен для восстановления
            Console.WriteLine("Use 'mount' in Safe Mode to restore partitions.");
        }

        private void Cd(string[] args)
        {
            if (args.Length == 0) return;
            string path = args[0];

            if (Path.IsPathRooted(path))
            {
                if (!Directory.Exists(path))
                {
                    Error("host directory not found");
                    return;
                }

                _hostMode = true;
                _hostPath = Path.GetFullPath(path);
                return;
            }

            if (path == "/" && _hostMode)
            {
                _hostMode = false;
                _currentDir = _root;
                return;
            }

            if (_hostMode)
            {
                Error("use absolute path or '/' to exit host mode");
                return;
            }

            if (path == ".." && _currentDir.Parent != null)
            {
                _currentDir = _currentDir.Parent;
                return;
            }

            var dir = _currentDir.Children
                .FirstOrDefault(x => x.IsDirectory && x.Name == path);

            if (dir == null)
            {
                Error("directory not found");
                return;
            }

            _currentDir = (DirectoryNode)dir;
        }

        private void Ls()
        {
            if (_hostMode)
            {
                foreach (var d in Directory.GetDirectories(_hostPath))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(Path.GetFileName(d));
                }

                foreach (var f in Directory.GetFiles(_hostPath))
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(Path.GetFileName(f));
                }

                Console.ForegroundColor = ConsoleColor.White;
                return;
            }

            foreach (var node in _currentDir.Children)
            {
                Console.ForegroundColor = node.IsDirectory ? ConsoleColor.Cyan : ConsoleColor.White;
                Console.WriteLine(node.Name);
            }

            Console.ForegroundColor = ConsoleColor.White;
        }

        private void Create(string[] args)
        {
            if (args.Length == 0)
            {
                Error("usage: create <filename> or create \"<path>\" <filename>");
                return;
            }

            string targetPath = args.Length == 1 ? (_hostMode ? _hostPath : _currentDir.Path) : args[0];
            string fileName = args.Length == 1 ? args[0] : args[1];

            if (_hostMode || Path.IsPathRooted(targetPath))
            {
                string fullPath = Path.Combine(targetPath, fileName);

                if (fullPath.StartsWith(@"C:\Windows", StringComparison.OrdinalIgnoreCase))
                {
                    Error("access denied (protected host directory)");
                    return;
                }

                File.WriteAllText(fullPath, "");
                Console.WriteLine($"host file created: {fileName}");
                return;
            }

            var targetDir = ResolveVfsPath(targetPath);
            if (targetDir == null)
            {
                Error("directory not found");
                return;
            }

            if (targetDir.IsProtected && !_isRoot)
            {
                Error("permission denied (use sudo)");
                return;
            }

            targetDir.Add(new FileNode(fileName, targetDir));
            Console.WriteLine($"file '{fileName}' created");
        }

        private void Open(string[] args)
        {
            if (args.Length == 0)
            {
                Error("usage: nano <filename> or nano \"<path>\" <filename>");
                return;
            }

            string targetPath = args.Length == 1 ? (_hostMode ? _hostPath : _currentDir.Path) : args[0];
            string fileName = args.Length == 1 ? args[0] : args[1];

            if (_hostMode || Path.IsPathRooted(targetPath))
            {
                string fullPath = Path.Combine(targetPath, fileName);

                if (!File.Exists(fullPath))
                {
                    Error("host file not found");
                    return;
                }

                var temp = new FileNode(fileName, null)
                {
                    Content = File.ReadAllText(fullPath)
                };

                NanoEditor(temp);
                File.WriteAllText(fullPath, temp.Content);
                return;
            }

            var targetDir = ResolveVfsPath(targetPath);
            if (targetDir == null)
            {
                Error("directory not found");
                return;
            }

            var file = targetDir.Children
                .FirstOrDefault(x => !x.IsDirectory && x.Name == fileName) as FileNode;

            if (file == null)
            {
                Error("file not found");
                return;
            }

            NanoEditor(file);
        }

        private void Start(string[] args)
        {
            if (!_hostMode)
            {
                Error("start command only available in host mode");
                return;
            }

            if (args.Length == 0)
            {
                Error("usage: start <filename> or start \"<path>\" <filename>");
                return;
            }

            string targetPath = _hostPath;
            string fileName = args[0];

            if (args.Length >= 2)
            {
                targetPath = args[0];
                fileName = args[1];
            }

            string fullPath = Path.Combine(targetPath, fileName);

            if (!File.Exists(fullPath))
            {
                Error("file not found");
                return;
            }

            string ext = Path.GetExtension(fileName).ToLowerInvariant();

            try
            {
                switch (ext)
                {
                    case ".bat":
                        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{fullPath}\"") { UseShellExecute = true });
                        Console.WriteLine("Batch file launched.");
                        break;

                    case ".java":
                        string className = Path.GetFileNameWithoutExtension(fileName);
                        var javac = Process.Start(new ProcessStartInfo("javac", $"\"{fullPath}\"") { RedirectStandardOutput = true, UseShellExecute = false });
                        javac.WaitForExit();

                        if (javac.ExitCode != 0)
                        {
                            Error("Java compilation failed.");
                            return;
                        }

                        Process.Start(new ProcessStartInfo("java", $"-cp \"{targetPath}\" {className}") { UseShellExecute = true });
                        Console.WriteLine("Java file compiled and launched.");
                        break;

                    case ".cs":
                        CompileAndRunCs(fullPath, targetPath);
                        break;

                    default:
                        Error("Unsupported file type. Supported: .bat, .java, .cs");
                        break;
                }
            }
            catch (Exception ex)
            {
                Error($"Launch failed: {ex.Message}");
            }
        }

        private void CompileAndRunCs(string sourcePath, string outputDir)
        {
            string sourceCode = File.ReadAllText(sourcePath);

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            string assemblyName = Path.GetFileNameWithoutExtension(sourcePath);
            string exePath = Path.Combine(outputDir, $"{assemblyName}.exe");

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            };

            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
            );

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = string.Join("\n", emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage()));
                Error($"C# compilation failed:\n{errors}");
                return;
            }

            ms.Seek(0, SeekOrigin.Begin);
            File.WriteAllBytes(exePath, ms.ToArray());

            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            Console.WriteLine("C# file compiled and launched.");
        }

        private void Sudo(string[] args)
        {
            if (args.Length == 0) return;

            _isRoot = true;
            Execute(string.Join(" ", args));
            _isRoot = false;
        }

        // ================= NANO EDITOR =================

        private void NanoEditor(FileNode file)
        {
            Console.Clear();
            Console.CursorVisible = true;

            var lines = new List<string>((file.Content ?? "").Split('\n'));
            if (lines.Count == 0) lines.Add("");

            int cursorX = 0;
            int cursorY = 0;

            void Render()
            {
                Console.SetCursorPosition(0, 0);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"--- nano {file.Name} ---  CTRL+X = save & exit");
                Console.ForegroundColor = ConsoleColor.White;

                foreach (var line in lines)
                    Console.WriteLine(line);

                Console.SetCursorPosition(cursorX, cursorY + 1);
            }

            Render();

            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.X && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    break;

                switch (key.Key)
                {
                    case ConsoleKey.LeftArrow: if (cursorX > 0) cursorX--; break;
                    case ConsoleKey.RightArrow: if (cursorX < lines[cursorY].Length) cursorX++; break;
                    case ConsoleKey.UpArrow: if (cursorY > 0) { cursorY--; cursorX = Math.Min(cursorX, lines[cursorY].Length); } break;
                    case ConsoleKey.DownArrow: if (cursorY < lines.Count - 1) { cursorY++; cursorX = Math.Min(cursorX, lines[cursorY].Length); } break;

                    case ConsoleKey.Enter:
                        {
                            string left = lines[cursorY].Substring(0, cursorX);
                            string right = lines[cursorY].Substring(cursorX);
                            string indent = new string(left.TakeWhile(c => c == ' ').ToArray());

                            lines[cursorY] = left;
                            lines.Insert(cursorY + 1, indent + right);
                            cursorY++;
                            cursorX = indent.Length;
                        }
                        break;

                    case ConsoleKey.Backspace:
                        if (cursorX > 0)
                        {
                            lines[cursorY] = lines[cursorY].Remove(cursorX - 1, 1);
                            cursorX--;
                        }
                        else if (cursorY > 0)
                        {
                            int prevLen = lines[cursorY - 1].Length;
                            lines[cursorY - 1] += lines[cursorY];
                            lines.RemoveAt(cursorY);
                            cursorY--;
                            cursorX = prevLen;
                        }
                        break;

                    case ConsoleKey.Tab:
                        lines[cursorY] = lines[cursorY].Insert(cursorX, "    ");
                        cursorX += 4;
                        break;

                    default:
                        if (!char.IsControl(key.KeyChar))
                        {
                            lines[cursorY] = lines[cursorY].Insert(cursorX, key.KeyChar.ToString());
                            cursorX++;
                        }
                        break;
                }

                Console.Clear();
                Render();
            }

            file.Content = string.Join("\n", lines);
            Console.Clear();
            Console.WriteLine("file saved.");
            Console.CursorVisible = false;
        }

        // ================= UTILS =================

        private void Error(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private DirectoryNode ResolveVfsPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return _root;

            DirectoryNode current = path.StartsWith("/") ? _root : _currentDir;

            var segments = path.Trim('/').Split('/');

            foreach (var seg in segments)
            {
                if (string.IsNullOrEmpty(seg)) continue;

                if (seg == "..")
                {
                    if (current.Parent != null) current = current.Parent;
                    continue;
                }

                var child = current.Children.FirstOrDefault(c => c.IsDirectory && c.Name == seg) as DirectoryNode;
                if (child == null) return null;

                current = child;
            }

            return current;
        }
    }

    // ================= FILE SYSTEM =================

    abstract class FsNode
    {
        public string Name;
        public DirectoryNode Parent;
        public bool IsDirectory;
        public bool IsProtected;

        public string Path => Parent == null ? "/" : Parent.Path + Name + "/";

        protected FsNode(string name, DirectoryNode parent, bool isDir, bool isProtected)
        {
            Name = name;
            Parent = parent;
            IsDirectory = isDir;
            IsProtected = isProtected;
        }
    }

    class DirectoryNode : FsNode
    {
        public List<FsNode> Children = new();

        public DirectoryNode(string name, DirectoryNode parent, bool isProtected)
            : base(name, parent, true, isProtected) { }

        public void Add(FsNode node) => Children.Add(node);
    }

    class FileNode : FsNode
    {
        public string Content = "";

        public FileNode(string name, DirectoryNode parent)
            : base(name, parent, false, false) { }
    }
}