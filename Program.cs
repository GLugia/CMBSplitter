using System;
using System.IO;
using System.Text;

namespace CMBSplitter
{
	public static class Program
	{
		public static void Main(string[] args)
		{
			// if no arguments were provided, explain we need a path to a .CMB file and stop
			if (args.Length < 2)
			{
				Console.WriteLine("Invalid arguments.");
				DisplayArguments();
				return;
			}
			switch (args[0])
			{
				case "/d":
					{
						if (args.Length < 2)
						{
							Console.WriteLine("Invalid syntax.");
							DisplayArguments();
							return;
						}
						string cmb_path = args[1];
						if (File.Exists(cmb_path))
						{
							// decompress the .CMB file
							Decompress(cmb_path);
						}
						else
						{
							Console.WriteLine("The .CMB file at the given path does not exist.");
							return;
						}
						break;
					}
				case "/c":
					{
						if (args.Length < 2)
						{
							Console.WriteLine("Invalid syntax.");
							DisplayArguments();
							return;
						}
						string txd_paths = args[1];
						if (Directory.Exists(txd_paths))
						{
							Compress(txd_paths);
						}
						else
						{
							Console.WriteLine("The txd directory given does not exist.");
						}
						break;
					}
				case "/?":
					{
						DisplayArguments();
						return;
					}
				default:
					{
						Console.WriteLine("Invalid arguments.");
						DisplayArguments();
						return;
					}
			}
			Console.WriteLine("Done!");
		}

		static void DisplayArguments()
		{
			Console.WriteLine(
				"\t/? - Help\n" +
				"\t/d - Decompress the given .CMB file\n" +
					"\t\tRequires a path to a .CMB file and, optionally, a folder to output the files to.\n" +
				"\t/c - Compress files to a .CMB file\n" +
					"\t\tRequires a path to a folder containing .txd files and, optionally, a folder to output the .CMB to\n");
		}

		// all path lengths in a .CMB file appear to be 0x50 bytes long
		// if anyone finds otherwise, let me know
		const int CMB_PATH_LENGTH = 0x50;

		struct FileData
		{
			public char[] path;
			public byte[] data;
		}

		static void Compress(string txd_paths)
		{
			string output_name;
			int last_index = txd_paths.LastIndexOf(Path.DirectorySeparatorChar);
			if (last_index == -1)
			{
				output_name = "unk.cmb";
			}
			else
			{
				output_name = txd_paths[++last_index..];
			}
			using BinaryWriter writer = new(File.Create($"{output_name}.cmb"));
			List<FileData> file_data = new();

			writer.Write(0);
			writer.Write(0);

			GetFileData(output_name, txd_paths, file_data, writer);
			string[] directories = Directory.GetDirectories(txd_paths);
			for (int i = 0; i < directories.Length; i++)
			{
				GetFileData(output_name, directories[i], file_data, writer);
			}

			long data_position = writer.BaseStream.Position;
			writer.BaseStream.Position = 0;
			writer.Write((int)data_position);
			writer.Write(file_data.Count);
			long next_data_position = data_position;
			for (int i = 0; i < file_data.Count; i++)
			{
				writer.Write(file_data[i].path);
				writer.Write((int)(writer.BaseStream.Length - data_position));
				writer.Write(file_data[i].data.Length);

				long header_position = writer.BaseStream.Position;
				writer.BaseStream.Position = next_data_position;
				writer.Write(file_data[i].data);
				next_data_position = writer.BaseStream.Position;
				writer.BaseStream.Position = header_position;
			}
			writer.Flush();
			writer.Close();
		}

		static void GetFileData(string root, string directory, List<FileData> file_data, BinaryWriter writer)
		{
			byte[] buffer = new byte[CMB_PATH_LENGTH + 0x8];
			string[] files = Directory.GetFiles(directory);
			for (int i = 0; i < files.Length; i++)
			{
				int index_of_root = files[i].IndexOf(root);
				string ignore_root_path = files[i][index_of_root..];
				char[] aligned_path = ignore_root_path.ToCharArray();
				Array.Resize(ref aligned_path, CMB_PATH_LENGTH);
				file_data.Add(new()
				{
					path = aligned_path,
					data = File.ReadAllBytes(files[i])
				});
				writer.Write(buffer);
			}
		}

		static void Decompress(string cmb_path)
		{
			using BinaryReader reader = new(File.OpenRead(cmb_path));

			int data_start_pos = reader.ReadInt32();
			int num_files = reader.ReadInt32();
			for (int i = 0; i < num_files; i++)
			{
				char[] path_chars = reader.ReadChars(CMB_PATH_LENGTH);
				string file_path = string.Join("", path_chars).Trim('\0');

				int offset_from_data = reader.ReadInt32();
				int length_of_data = reader.ReadInt32();

				// store the current position before jumping to and reading the data
				long old_pos = reader.BaseStream.Position;
				reader.BaseStream.Position = data_start_pos + offset_from_data;
				byte[] data = reader.ReadBytes(length_of_data);
				// jump back to the original position
				reader.BaseStream.Position = old_pos;

				// we want to keep the full path referenced by the .CMB
				string build_path = "";
				int last_index = 0;
				int index;
				while ((index = file_path.IndexOf('\\', last_index + 1)) != -1)
				{
					string directory = file_path[last_index..index];
					Directory.CreateDirectory($"{build_path}{directory}");
					build_path += $"{directory}{Path.DirectorySeparatorChar}";
					last_index = index;
				}

				build_path += file_path[last_index..];

				// dump the file to the output directory
				using BinaryWriter writer = new(File.Create(build_path));
				writer.Write(data);
				writer.Flush();
				writer.Close();
			}
		}
	}
}