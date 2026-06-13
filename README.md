# Anime Studio

## Asset extraction tool for unity games !

![image](https://github.com/user-attachments/assets/fc1decdc-a589-43a2-b965-2d8151d0975f)

---

# How do I use this ?

You should look at the [official wiki](https://github.com/Escartem/AnimeStudio/wiki), if required look at the [original tutorial by Modder4869](https://gist.github.com/Modder4869/0f5371f8879607eb95b8e63badca227e) or the [original readme](https://github.com/RazTools/Studio/blob/main/README.md). Otherwise [join the discord](https://discord.gg/fzRdtVh) and ask there !

---

# How do I download this ?

## Download Studio

- **[.NET 10 Build (Recommended - Latest)](https://nightly.link/Escartem/AnimeStudio/workflows/build/master/AnimeStudio-net10.zip)** ✨
- **[.NET 9 Build (Stable)](https://nightly.link/Escartem/AnimeStudio/workflows/build/master/AnimeStudio-net9.zip)**

---

# What is this ?

It's an up-to-date fork of Razmoth's one. After his repo was discontinued, bugs started to arise as games evolved, and people started making forks to fix some of them, but each one would not support the fixes by the others and so on. This version aims at being the new start base for AssetStudio, renamed as AnimeStudio, it supports all 3 main hoyo games, and is open to any contribution !

---

# What changed ?

This is a non-exhaustive list of modifications :
- Removed usage of a [certain dll for a certain decryption](https://github.com/Escartem/AnimeStudio/commit/1fcfa9041e07cd0a98b4d23f1578e910256fa1f8) 👀
- Merged fixes for Genshin, Star Rail and ZZZ suport with improvements
- Dark mode
- Reorganised menu bar for easier usage
- Addes SHA256 hash for assets
- New game selector merged with UnityCN keys list and updated UnityCN keys manager
- Asset Browser improvements
    - It is now possible to use json files instead of only message pack
    - You can now relocate the sources files of a map instead of having to build a new one to adjust them, making maps no longer game install dir dependant
    - Only selected assets are displayed in the main window when loading instead of the full blocks
    - You can load 2 asset maps at once and view the difference between both

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
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

Contributions of any kind welcome!