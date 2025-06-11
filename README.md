# Anime Studio
## Asset exploration tool for hoyo games and more !

![image](https://github.com/user-attachments/assets/173757f6-8dce-48fc-9525-821bb1244280)

---

# What is this ?

After the official AssetStudio by Razmoth was discontinued, bugs started to arise as games evolved, and people started making forks to fix some of them, but each one would not support the fixes by the others and so on. This version aims at being the new start base for AssetStudio, renamed as AnimeStudio, it supports all 3 main hoyo games, and is open to any contribution !

---

# Download

| **.NET Version** | **GUI** | **CLI** |
| - | - | - |
| **9.0** | [Download](https://nightly.link/Escartem/AnimeStudio/workflows/build/master/AnimeStudio-GUI-net9.0.zip) | [Download](https://nightly.link/Escartem/AnimeStudio/workflows/build/master/AnimeStudio-CLI-net9.0.zip) |
| **8.0** | [Download](https://nightly.link/Escartem/AnimeStudio/workflows/build/master/AnimeStudio-GUI-net8.0.zip) | [Download](https://nightly.link/Escartem/AnimeStudio/workflows/build/master/AnimeStudio-CLI-net8.0.zip) |

---

# What's changed ?

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

# How to use ?

Compared to the original studio, the UI was slightly adjusted, but if you are familiar with AssetStudio, you should get the hang of it very quickly. For new people, there is not a tutorial for this one specifically yet, but I recommend looking at the original tutorial to help [here](https://gist.github.com/Modder4869/0f5371f8879607eb95b8e63badca227e) (Tutorial by Modder4869) and look at the [original readme](https://github.com/RazTools/Studio/blob/main/README.md), otherwise you can ask in the [Discord](https://discord.gg/fzRdtVh).

---

Special thanks to:
- [hrothgar](https://github.com/hrothgar234567): Help in ZZZ fixes & some dll RE
- Perfare: The real original AssetStudio - [[project](https://github.com/perfare/AssetStudio)]
- Razmoth: Original AssetStudio for anime games support - [[project](https://github.com/RazTools/Studio)]
- hashblen: ZZZ fixes fork - [[project](https://github.com/hashblen/ZZZ_Studio)]
- yarik0chka: Genshin and Star Rail fixes fork - [[project](https://github.com/yarik0chka/YarikStudio)]
- All of the others contributor of Razmoth's Studio
