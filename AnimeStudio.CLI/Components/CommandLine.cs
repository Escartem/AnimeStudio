using System;
using System.IO;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace AnimeStudio.CLI
{
    public static class CommandLine
    {
        public static void Init(string[] args)
        {
            var rootCommand = RegisterOptions();
            var parseResult = CommandLineParser.Parse(rootCommand, args, new ParserConfiguration());
            parseResult.Invoke(new InvocationConfiguration());
        }
        public static RootCommand RegisterOptions()
        {
            var optionsBinder = new OptionsBinder();
            var rootCommand = new RootCommand();

            rootCommand.Options.Add(optionsBinder.Silent);
            rootCommand.Options.Add(optionsBinder.LoggerFlags);
            rootCommand.Options.Add(optionsBinder.TypeFilter);
            rootCommand.Options.Add(optionsBinder.NameFilter);
            rootCommand.Options.Add(optionsBinder.ContainerFilter);
            rootCommand.Options.Add(optionsBinder.GameName);
            rootCommand.Options.Add(optionsBinder.MapOp);
            rootCommand.Options.Add(optionsBinder.MapType);
            rootCommand.Options.Add(optionsBinder.MapName);
            rootCommand.Options.Add(optionsBinder.UnityVersion);
            rootCommand.Options.Add(optionsBinder.GroupAssetsType);
            rootCommand.Options.Add(optionsBinder.AssetExportType);
            rootCommand.Options.Add(optionsBinder.Key);
            rootCommand.Options.Add(optionsBinder.AIFile);
            rootCommand.Options.Add(optionsBinder.DummyDllFolder);
            rootCommand.Arguments.Add(optionsBinder.Input);
            rootCommand.Arguments.Add(optionsBinder.Output);

            rootCommand.SetAction(parseResult => Program.Run(optionsBinder.GetOptions(parseResult)));

            return rootCommand;
        }
    }
    public class Options
    {
        public bool Silent { get; set; }
        public LoggerEvent[] LoggerFlags { get; set; }
        public string[] TypeFilter { get; set; }
        public Regex[] NameFilter { get; set; }
        public Regex[] ContainerFilter { get; set; }
        public string GameName { get; set; }
        public MapOpType MapOp { get; set; }
        public ExportListType MapType { get; set; }
        public string MapName { get; set; }
        public string UnityVersion { get; set; }
        public AssetGroupOption GroupAssetsType { get; set; }
        public ExportType AssetExportType { get; set; }
        public byte Key { get; set; }
        public FileInfo AIFile { get; set; }
        public DirectoryInfo DummyDllFolder { get; set; }
        public FileInfo Input { get; set; }
        public DirectoryInfo Output { get; set; }
    }

    public class OptionsBinder
    {
        public readonly Option<bool> Silent;
        public readonly Option<LoggerEvent[]> LoggerFlags;
        public readonly Option<string[]> TypeFilter;
        public readonly Option<Regex[]> NameFilter;
        public readonly Option<Regex[]> ContainerFilter;
        public readonly Option<string> GameName;
        public readonly Option<MapOpType> MapOp;
        public readonly Option<ExportListType> MapType;
        public readonly Option<string> MapName;
        public readonly Option<string> UnityVersion;
        public readonly Option<AssetGroupOption> GroupAssetsType;
        public readonly Option<ExportType> AssetExportType;
        public readonly Option<byte> Key;
        public readonly Option<FileInfo> AIFile;
        public readonly Option<DirectoryInfo> DummyDllFolder;
        public readonly Argument<FileInfo> Input;
        public readonly Argument<DirectoryInfo> Output;

        public OptionsBinder()
        {
            Silent = new Option<bool>("--silent")
            {
                Description = "Hide log messages."
            };
            LoggerFlags = new Option<LoggerEvent[]>("--logger_flags")
            {
                Description = "Flags to control toggle log events.",
                AllowMultipleArgumentsPerToken = true,
                HelpName = "Verbose|Debug|Info|etc.."
            };
            TypeFilter = new Option<string[]>("--types")
            {
                Description = "Specify unity class type(s)",
                AllowMultipleArgumentsPerToken = true,
                HelpName = "Texture2D|Shader:Parse|Sprite:Both|etc.."
            };
            NameFilter = new Option<Regex[]>("--names")
            {
                Description = "Specify name regex filter(s).",
                AllowMultipleArgumentsPerToken = true,
                CustomParser = ParseRegexFilter
            };
            ContainerFilter = new Option<Regex[]>("--containers")
            {
                Description = "Specify container regex filter(s).",
                AllowMultipleArgumentsPerToken = true,
                CustomParser = ParseRegexFilter
            };
            GameName = new Option<string>("--game")
            {
                Description = "Specify Game.",
                Required = true
            };
            MapOp = new Option<MapOpType>("--map_op")
            {
                Description = "Specify which map to build."
            };
            MapType = new Option<ExportListType>("--map_type")
            {
                Description = "AssetMap output type."
            };
            MapName = new Option<string>("--map_name")
            {
                Description = "Specify AssetMap file name.",
                DefaultValueFactory = _ => "assets_map"
            };
            UnityVersion = new Option<string>("--unity_version")
            {
                Description = "Specify Unity version."
            };
            GroupAssetsType = new Option<AssetGroupOption>("--group_assets")
            {
                Description = "Specify how exported assets should be grouped."
            };
            AssetExportType = new Option<ExportType>("--export_type")
            {
                Description = "Specify how assets should be exported."
            };
            AIFile = new Option<FileInfo>("--ai_file")
            {
                Description = "Specify asset_index json file path (to recover GI containers)."
            };
            DummyDllFolder = new Option<DirectoryInfo>("--dummy_dlls")
            {
                Description = "Specify DummyDll path."
            };
            Input = new Argument<FileInfo>("input_path")
            {
                Description = "Input file/folder."
            }.AcceptLegalFilePathsOnly();
            Output = new Argument<DirectoryInfo>("output_path")
            {
                Description = "Output folder."
            }.AcceptLegalFilePathsOnly();

            Key = new Option<byte>("--key")
            {
                Description = "XOR key to decrypt MiHoYoBinData.",
                CustomParser = result => ParseKey(result.Tokens.Single().Value)
            };

            LoggerFlags.Validators.Add(FilterValidator);
            TypeFilter.Validators.Add(FilterValidator);
            NameFilter.Validators.Add(FilterValidator);
            ContainerFilter.Validators.Add(FilterValidator);
            AIFile.Validators.Add(LegalPathValidator);
            DummyDllFolder.Validators.Add(LegalPathValidator);
            Key.Validators.Add(result =>
            {
                if (result.Tokens.Count == 0)
                {
                    return;
                }

                var value = result.Tokens.Single().Value;
                try
                {
                    ParseKey(value);
                }
                catch (Exception e)
                {
                    result.AddError("Invalid byte value.\n" + e.Message);
                }
            });
            GameName.Validators.Add(result =>
            {
                var value = result.GetValueOrDefault<string>();
                if (!GameManager.GetGameNames().Contains(value))
                {
                    result.AddError($"Invalid game. Supported games: {GameManager.SupportedGames()}");
                }
            });

            GameName.CompletionSources.Add(GameManager.GetGameNames());

            LoggerFlags.DefaultValueFactory = _ => new LoggerEvent[] { LoggerEvent.Debug, LoggerEvent.Info, LoggerEvent.Warning, LoggerEvent.Error };
            GroupAssetsType.DefaultValueFactory = _ => AssetGroupOption.ByType;
            AssetExportType.DefaultValueFactory = _ => ExportType.Convert;
            MapOp.DefaultValueFactory = _ => MapOpType.None;
            MapType.DefaultValueFactory = _ => ExportListType.XML;
        }
        
        public byte ParseKey(string value)
        {
            if (value.StartsWith("0x"))
            {
                value = value[2..];
                return Convert.ToByte(value, 0x10);
            }
            else
            {
                return byte.Parse(value);
            }
        }

        public void FilterValidator(OptionResult result)
        {
            var values = result.Tokens.Select(x => x.Value).ToArray();
            if (values.Length == 1 && File.Exists(values[0]))
            {
                return;
            }

            foreach (var val in values)
            {
                if (string.IsNullOrWhiteSpace(val))
                {
                    result.AddError("Empty string.");
                    return;
                }

                try
                {
                    Regex.Match("", val, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException e)
                {
                    result.AddError("Invalid Regex.\n" + e.Message);
                    return;
                }
            }
        }

        public void LegalPathValidator(OptionResult result)
        {
            foreach (var value in result.Tokens.Select(x => x.Value))
            {
                try
                {
                    Path.GetFullPath(value);
                }
                catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
                {
                    result.AddError("Invalid path.\n" + e.Message);
                    return;
                }
            }
        }

        private Regex[] ParseRegexFilter(ArgumentResult result)
        {
            var items = new List<Regex>();
            var values = result.Tokens.Select(x => x.Value).ToArray();
            if (values.Length == 1 && File.Exists(values[0]))
            {
                foreach (var line in File.ReadLines(values[0]))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        items.Add(new Regex(line, RegexOptions.IgnoreCase));
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }
            else
            {
                foreach (var value in values)
                {
                    try
                    {
                        items.Add(new Regex(value, RegexOptions.IgnoreCase));
                    }
                    catch (ArgumentException e)
                    {
                        result.AddError("Invalid Regex.\n" + e.Message);
                    }
                }
            }

            return items.ToArray();
        }

        public Options GetOptions(ParseResult parseResult) =>
        new()
        {
            Silent = parseResult.GetValue(Silent),
            LoggerFlags = parseResult.GetValue(LoggerFlags),
            TypeFilter = parseResult.GetValue(TypeFilter),
            NameFilter = parseResult.GetValue(NameFilter),
            ContainerFilter = parseResult.GetValue(ContainerFilter),
            GameName = parseResult.GetRequiredValue(GameName),
            MapOp = parseResult.GetValue(MapOp),
            MapType = parseResult.GetValue(MapType),
            MapName = parseResult.GetValue(MapName),
            UnityVersion = parseResult.GetValue(UnityVersion),
            GroupAssetsType = parseResult.GetValue(GroupAssetsType),
            AssetExportType = parseResult.GetValue(AssetExportType),
            Key = parseResult.GetValue(Key),
            AIFile = parseResult.GetValue(AIFile),
            DummyDllFolder = parseResult.GetValue(DummyDllFolder),
            Input = parseResult.GetRequiredValue(Input),
            Output = parseResult.GetRequiredValue(Output)
        };
    }
}
