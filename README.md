# Anime Studio

## Asset extraction tool for unity games !

![image](https://github.com/user-attachments/assets/fc1decdc-a589-43a2-b965-2d8151d0975f)

Anime Studio opens Unity bundles and lets you browse, preview and export what's inside : textures, meshes, animations, audio, shaders, text assets and more. It handles the encrypted bundles used by Genshin Impact, Star Rail and Zenless Zone Zero, all of their others upcoming titles. Also works on a long list of other encrypted games and works fine on regular Unity games too.

It comes as a GUI and a CLI, both in the same download. The GUI is what you want if you're just looking for assets, the CLI is there for scripting and batch exports.

---

# How do I download this ?

- **[.NET 10 Build (Recommended - Latest)](https://nightly.link/Escartem/AnimeStudio/workflows/build/master/AnimeStudio-net10.zip)** ✨
- **[.NET 9 Build (Stable)](https://nightly.link/Escartem/AnimeStudio/workflows/build/master/AnimeStudio-net9.zip)**

Both builds are Windows x64 only and need the matching [.NET Desktop Runtime](https://dotnet.microsoft.com/download/dotnet) installed. Unzip anywhere and run `AnimeStudio.GUI.exe` or `AnimeStudio.CLI.exe`.

---

# How do I use this ?

Pick your game in the game selector first, then load a file or a folder. This matters because each game has its own decryption, loading with the wrong one selected will either fail or give you garbage. There are over 50 entries in that list, including separate ones for closed beta versions, so take the one that matches the build your files come from.

From there, load a file or a folder, browse the list, and export what you want. Assets can be filtered by type, by name or by container path, and previewed before exporting for most types.

For big dumps there's the Asset Browser, it builds a map of everything in a game install so you can search it without loading the whole thing every time. Maps can be saved as message pack or json, relocated to a different install folder, and two of them can be loaded side by side to see what changed between versions.

The CLI takes the same options as flags, with an input and an output path :

```
AnimeStudio.CLI.exe <input> <output> --game GI --types Texture2D,Sprite --export_type convert
```

Useful flags are `--game`, `--types`, `--names` and `--containers` for filtering, `--group_assets` for the output folder layout, `--map_op` and `--map_type` for building asset maps, and `--silent` to keep it quiet. Run it without arguments for the full list.

The [official wiki](https://github.com/Escartem/AnimeStudio/wiki) goes into detail on all of this. If something isn't covered there, look at the [original tutorial by Modder4869](https://gist.github.com/Modder4869/0f5371f8879607eb95b8e63badca227e) or the [original readme](https://github.com/RazTools/Studio/blob/main/README.md). Otherwise [join the discord](https://discord.gg/fzRdtVh) and ask there !

---

# Building

You need Visual Studio 2022 with the C++ desktop workload and the .NET 9 and 10 SDKs.

`build.ps1` builds the GUI and the CLI for both frameworks and packages them into `dist`. That's the same script the CI runs, so if it works there it works locally. For day to day work `dotnet build AnimeStudio.GUI` is enough, the packaging step is only needed for a release.

The native libraries are not built by that script, they sit prebuilt in `AnimeStudio.Libraries` and only need rebuilding when you touch their sources. Each one is its own project in the solution and outputs straight into that folder :

| Project | Output | What it does |
| --- | --- | --- |
| `AnimeStudio.Ooz` | `AnimeStudio.Ooz.dll` | Kraken / Mermaid / Leviathan decompression |
| `AnimeStudio.FBXNative` | `AnimeStudio.FBXNative.dll` | FBX export, needs the Autodesk FBX SDK at exactly version 2020.3.7 installed |
| `AnimeStudio.HLSLDecompiler` | `AnimeStudio.HLSLDecompiler.dll` | Shader decompilation |
| `AnimeStudio.ACL.DB` / `.ZZZ` / `.SR` | `AnimeStudio.ACL.*.dll` | Animation decompression, one per ACL version |

They are all x64 only and excluded from the solution build, build them by hand when needed.

Contributions are welcome, whether it's a new game, a fix for an existing one or anything else. Open a PR and it'll get looked at.

---

# Contributors ✨

Thanks goes to these wonderful people :

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tbody>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/hrothgar234567"><img src="https://avatars.githubusercontent.com/u/215089974?v=4?s=100" width="100px;" alt="hrothgar234567"/><br /><sub><b>hrothgar234567</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=hrothgar234567" title="Code">💻</a> <a href="https://github.com/Escartem/AnimeStudio/pulls?q=is%3Apr+reviewed-by%3Ahrothgar234567" title="Reviewed Pull Requests">👀</a> <a href="#ideas-hrothgar234567" title="Ideas, Planning, & Feedback">🤔</a> <a href="#question-hrothgar234567" title="Answering Questions">💬</a> <a href="#platform-hrothgar234567" title="Packaging/porting to new platform">📦</a> <a href="#security-hrothgar234567" title="Security">🛡️</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://soundcloud.com/eleiyas/"><img src="https://avatars.githubusercontent.com/u/16349939?v=4?s=100" width="100px;" alt="Elliot Bastiani"/><br /><sub><b>Elliot Bastiani</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=Eleiyas" title="Code">💻</a> <a href="#ideas-Eleiyas" title="Ideas, Planning, & Feedback">🤔</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/yarik0chka"><img src="https://avatars.githubusercontent.com/u/64433879?v=4?s=100" width="100px;" alt="yarik0chka"/><br /><sub><b>yarik0chka</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=yarik0chka" title="Code">💻</a> <a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3Ayarik0chka" title="Bug reports">🐛</a> <a href="#question-yarik0chka" title="Answering Questions">💬</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://www.youtube.com/c/Manashiku"><img src="https://avatars.githubusercontent.com/u/46613923?v=4?s=100" width="100px;" alt="manashiku"/><br /><sub><b>manashiku</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=Manashiku" title="Code">💻</a> <a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3AManashiku" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Razmoth"><img src="https://avatars.githubusercontent.com/u/32140579?v=4?s=100" width="100px;" alt="Razmoth"/><br /><sub><b>Razmoth</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=Razmoth" title="Code">💻</a> <a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3ARazmoth" title="Bug reports">🐛</a> <a href="#ideas-Razmoth" title="Ideas, Planning, & Feedback">🤔</a> <a href="#research-Razmoth" title="Research">🔬</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Dimbreath"><img src="https://avatars.githubusercontent.com/u/1474840?v=4?s=100" width="100px;" alt="Dimbreath"/><br /><sub><b>Dimbreath</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3ADimbreath" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/LukeFZ"><img src="https://avatars.githubusercontent.com/u/17146677?v=4?s=100" width="100px;" alt="Luke"/><br /><sub><b>Luke</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3ALukeFZ" title="Bug reports">🐛</a> <a href="#security-LukeFZ" title="Security">🛡️</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/aelurum"><img src="https://avatars.githubusercontent.com/u/6244109?v=4?s=100" width="100px;" alt="VaDiM"/><br /><sub><b>VaDiM</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=aelurum" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://festivity.moe/"><img src="https://avatars.githubusercontent.com/u/77230051?v=4?s=100" width="100px;" alt="festivity"/><br /><sub><b>festivity</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=festivities" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/MiemieMethod"><img src="https://avatars.githubusercontent.com/u/40489495?v=4?s=100" width="100px;" alt="方法放寒假"/><br /><sub><b>方法放寒假</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=MiemieMethod" title="Code">💻</a> <a href="#platform-MiemieMethod" title="Packaging/porting to new platform">📦</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/jokelbaf"><img src="https://avatars.githubusercontent.com/u/60827680?v=4?s=100" width="100px;" alt="JokelBaf"/><br /><sub><b>JokelBaf</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=jokelbaf" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/formagGinoo"><img src="https://avatars.githubusercontent.com/u/67542068?v=4?s=100" width="100px;" alt="formagGino"/><br /><sub><b>formagGino</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=formagGinoo" title="Code">💻</a> <a href="#platform-formagGinoo" title="Packaging/porting to new platform">📦</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/hashblen"><img src="https://avatars.githubusercontent.com/u/62646051?v=4?s=100" width="100px;" alt="Hashblen"/><br /><sub><b>Hashblen</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3Ahashblen" title="Bug reports">🐛</a> <a href="https://github.com/Escartem/AnimeStudio/commits?author=hashblen" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Sieluna"><img src="https://avatars.githubusercontent.com/u/88884784?v=4?s=100" width="100px;" alt="Sieluna"/><br /><sub><b>Sieluna</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=Sieluna" title="Code">💻</a> <a href="#infra-Sieluna" title="Infrastructure (Hosting, Build-Tools, etc)">🚇</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/1004452714"><img src="https://avatars.githubusercontent.com/u/28773469?v=4?s=100" width="100px;" alt="DarkFlameMaster"/><br /><sub><b>DarkFlameMaster</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3A1004452714" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/SherkeyXD"><img src="https://avatars.githubusercontent.com/u/57581480?v=4?s=100" width="100px;" alt="SherkeyXD"/><br /><sub><b>SherkeyXD</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=SherkeyXD" title="Code">💻</a> <a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3ASherkeyXD" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/djpadbit"><img src="https://avatars.githubusercontent.com/u/9431263?v=4?s=100" width="100px;" alt="djpadbit"/><br /><sub><b>djpadbit</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=djpadbit" title="Code">💻</a> <a href="#platform-djpadbit" title="Packaging/porting to new platform">📦</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/tserj"><img src="https://avatars.githubusercontent.com/u/17748861?v=4?s=100" width="100px;" alt="tserj"/><br /><sub><b>tserj</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3Atserj" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://momokko.moe"><img src="https://avatars.githubusercontent.com/u/78632509?v=4?s=100" width="100px;" alt="綾瀬桃桃"/><br /><sub><b>綾瀬桃桃</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3AMomoko-Ayase" title="Bug reports">🐛</a> <a href="https://github.com/Escartem/AnimeStudio/commits?author=Momoko-Ayase" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/bbdd2729"><img src="https://avatars.githubusercontent.com/u/179790579?v=4?s=100" width="100px;" alt="bbdd"/><br /><sub><b>bbdd</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/commits?author=bbdd2729" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/ihzgniqgnem"><img src="https://avatars.githubusercontent.com/u/161725897?v=4?s=100" width="100px;" alt="ihzgniqgnem"/><br /><sub><b>ihzgniqgnem</b></sub></a><br /><a href="https://github.com/Escartem/AnimeStudio/issues?q=author%3Aihzgniqgnem" title="Bug reports">🐛</a> <a href="https://github.com/Escartem/AnimeStudio/commits?author=ihzgniqgnem" title="Code">💻</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

Contributions of any kind welcome!

---

# Credits

Anime Studio is a fork of [Studio](https://github.com/RazTools/Studio) by Razmoth, itself a fork of [AssetStudio](https://github.com/Perfare/AssetStudio) by Perfare. After Razmoth's repo was discontinued things started breaking as games evolved, and several forks appeared fixing different bits without ever merging back into each other. This one aims at being the place where those fixes live together. Most of what's here started in those two projects.

The native side is built on other people's work :

- [ooz](https://github.com/powzix/ooz) by powzix, through [zao's fork](https://github.com/zao/ooz) - Kraken decompression
- [FBX SDK](https://aps.autodesk.com/developer/overview/fbx-sdk) by Autodesk, wrapper based on Perfare's and hozuki's - FBX export
- [3Dmigoto](https://github.com/bo3b/3Dmigoto) and [HLSLcc](https://github.com/Unity-Technologies/HLSLcc) by Unity - shader decompilation
- [ACL](https://github.com/nfrechette/acl) and [RTM](https://github.com/nfrechette/rtm) by Nicholas Frechette, wrappers by Razmoth - animation decompression
- [FMOD Engine](https://www.fmod.com/) by Firelight Technologies - audio preview and export

FMOD is used under Firelight's free license for non commercial use, and the FBX SDK under Autodesk's terms. Everything else is MIT or similar, see each project for details.