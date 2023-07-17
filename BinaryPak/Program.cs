using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.GZip;
using Newtonsoft.Json;

namespace BinaryPak
{
	public static class BinaryPak
	{
		static ProjFile project;
		private static char sep = OperatingSystem.IsWindows() ? '\\' : '/';

		public static void Main(String[] args)
		{
			if (args.Length == 0)
			{
				try
				{
					project = JsonConvert.DeserializeObject<ProjFile>(
						          File.ReadAllText(FindProject(new DirectoryInfo(Directory.GetCurrentDirectory())))) ??
					          throw new InvalidDataException();
				}
				catch (FileNotFoundException e)
				{
					Console.WriteLine("Unable to locate project file");
					return;
				}
				catch (InvalidDataException e)
				{
					Console.WriteLine("BinaryPak project file is malformed!");
					return;
				}

				GeneratePak();
				return;
			}

			if (args[0].ToLower() == "create")
			{
				Console.Write("Enter an output path (pakdata): ");
				var op = Console.ReadLine() ?? "pakdata";
				if (string.IsNullOrWhiteSpace(op)) op = "pakdata";
				Console.Write("Enable Gzip? (y/n): ");
				var gzip = Console.ReadKey().KeyChar == 'y';
				Console.WriteLine();

				project = new ProjFile()
					{ FilesToPak = new List<FileData>(), OutputFolder = new DirectoryInfo(op).FullName, Gzip = gzip };
				File.WriteAllText("binarypak.json", JsonConvert.SerializeObject(project, Formatting.Indented));
				Console.WriteLine("BinaryPak project file written to binarypak.json");
				return;
			}

			try
			{
				project = JsonConvert.DeserializeObject<ProjFile>(
					          File.ReadAllText(FindProject(new DirectoryInfo(Directory.GetCurrentDirectory())))) ??
				          throw new InvalidDataException();
			}
			catch (FileNotFoundException e)
			{
				Console.WriteLine("Unable to locate project file");
				return;
			}
			catch (InvalidDataException e)
			{
				Console.WriteLine("BinaryPak project file is malformed!");
				return;
			}

			if (args[0].ToLower() == "add")
			{

				bool recursive = args[1].ToLower() == "-r";
				if (args.Length == 2)
				{
					var name = new FileInfo(args[1]).Name.Replace(".", "__").Replace(" ", "_");
					Console.Write($"Enter name ({name}): ");
					var newname = Console.ReadLine();
					if (!string.IsNullOrWhiteSpace(newname))
						name = newname;

					project.FilesToPak.Add(new FileData() { AbsPath = new FileInfo(args[1]).FullName, Name = name });

					File.WriteAllText("binarypak.json", JsonConvert.SerializeObject(project, Formatting.Indented));
					return;
				}

				if (recursive)
				{
					foreach (var s in args[2..])
					{
						if (Directory.Exists(s))
						{
							AddDir(s, recursive);
						}
						else
						{
							project.FilesToPak.Add(new FileData()
							{
								AbsPath = new FileInfo(s).FullName,
								Name = GetFileName(new FileInfo(s).FullName)
							});
						}

					}
				}
				else
				{
					foreach (var s in args[1..])
					{
						if (Directory.Exists(s))
						{
						}
						else
						{
							project.FilesToPak.Add(new FileData()
							{
								AbsPath = new FileInfo(s).FullName,
								Name = GetFileName(new FileInfo(s).FullName)
							});
						}
					}
				}

				File.WriteAllText("binarypak.json", JsonConvert.SerializeObject(project, Formatting.Indented));
			}
		}

		public static void AddDir(string dir, bool recursive)
		{
			if (recursive)
			{
				var dirs = Directory.GetDirectories(dir);
				foreach (var s in dirs)
				{
					AddDir(s, recursive);
				}

				var files = Directory.GetFiles(dir);
				foreach (var file in files)
				{
					project.FilesToPak.Add(new FileData()
					{
						AbsPath = new FileInfo(file).FullName,
						Name = GetFileName(new FileInfo(file).FullName)
					});
				}
			}
		}

		public static string GetFileName(string file)
		{
			Console.Write($"Enter name for file \"{file}\": ");
			var name = Console.ReadLine() ?? GetFileName(file);

			if (!IsValidIdentifier(name))
			{
				Console.WriteLine("Invalid Identifier!");
				return GetFileName(file);
			}

			return name;
		}

		private static char[] NonDigit =
		{
			'_', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
			'u', 'v', 'w', 'x', 'y', 'z',
			'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U',
			'V', 'W', 'X', 'Y', 'Z'
		};
	
	
		static char[] Digit = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
		
		
		public static bool IsValidIdentifier(string str)
		{
			if (str.Length == 0)
				return false;
			if (NonDigit.Contains(str[0]))
			{
				for (var i = 1; i < str.ToCharArray().Length; i++)
				{
					if (!NonDigit.Contains(str[i]) && !Digit.Contains(str[i]))
					{
						return false;
					}
				}

				return true;
			}

			return false;
		}
		
		public static string FindProject(DirectoryInfo dir)
		{
			if (dir.GetFiles().Any(t => t.Name == "binarypak.json"))
			{
				return dir.GetFiles().First(t => t.Name == "binarypak.json").FullName;
			}
			else
			{
				return FindProject(dir.Parent ?? throw new FileNotFoundException("Unable to locate project file"));
			}
		}

		public static void GeneratePak()
		{
			if (Directory.Exists(project.OutputFolder))
			{
				Directory.Delete(project.OutputFolder, true);
			}
			Directory.CreateDirectory(project.OutputFolder);
			
			var oldCWD = Directory.GetCurrentDirectory();
			Directory.SetCurrentDirectory(project.OutputFolder);

			Directory.CreateDirectory("src");
			Directory.CreateDirectory("include");
			Directory.CreateDirectory("res");
			//create cmakelists.txt (optional)
			//create src/bpaklib.c
			//create include/bpaklib.h

			if (project.Gzip) Parallel.ForEach(project.FilesToPak, GzipFile);

			var embedList = new List<string>();
			var enumList = new List<string>();
			var caseList = new List<string>();
			foreach (var file in project.FilesToPak)
			{
				embedList.Add($"static const unsigned char {file.Name}_[] = {{ \n#embed \"{Directory.GetCurrentDirectory() + $"{sep}res{sep}" + file.Name + ".bp"}\"\n}};\n");
				enumList.Add($"{file.Name},\n");
				caseList.Add($"case {file.Name}: return {file.Name}_;\n"); //TODO fix
			}

			if (project.Gzip)
			{
				//TODO implement
			}
			else
			{


				File.WriteAllText("include/bpaklib.h", $"enum FilesInPak {{\n" +
				                                       $"{string.Concat(enumList)}" +
				                                       $"}};\n" +
				                                       $"const unsigned char* BP_GetResource(enum FilesInPak file);");

				File.WriteAllText("src/bpaklib.c", $"#include \"..{sep}include{sep}bpaklib.h\"\n" +
				                                   $"{string.Concat(embedList)}" +
				                                   $"unsigned char* BP_GetResource(enum FilesInPak file) {{\n" +
				                                   $"switch (file){{\n" +
				                                   $"{string.Concat(caseList)}" +
				                                   $"default: return nullptr;\n" +
				                                   $"}}" +
				                                   $"}}");
			}
			
			Directory.SetCurrentDirectory(oldCWD);

		}

		public static void GzipFile(FileData file)
		{
			GZip.Compress(File.OpenRead(file.AbsPath), File.OpenWrite("res/" + file.Name + ".gz"), true);
		}
	}

	

	public class FileData
	{
		public string AbsPath;
		public string Name;
	}
	
	public class ProjFile
	{
		public bool Gzip;
		public string OutputFolder;
		public List<FileData> FilesToPak;
	}
}